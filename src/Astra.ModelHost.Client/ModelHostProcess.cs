using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Astra.Infrastructure;
using Astra.ModelHost.Contracts;
using Grpc.Core;

namespace Astra.ModelHost.Client;

/// <summary>
/// Owns the lifetime of a local ModelHost child process and its client.
/// </summary>
public sealed class ModelHostProcess : IAsyncDisposable
{
	private readonly Process _process;
	private bool _disposed;

	private ModelHostProcess(Process process, ModelHostEndpoint endpoint, ModelHostClient client)
	{
		_process = process;
		Endpoint = endpoint;
		Client = client;
	}

	/// <summary>Gets the local endpoint used by the process.</summary>
	public ModelHostEndpoint Endpoint { get; }

	/// <summary>Gets the connected client.</summary>
	public ModelHostClient Client { get; }

	/// <summary>Gets the handshake information.</summary>
	public ModelHostInfo Info => Client.Info;

	/// <summary>Gets the operating-system process identifier.</summary>
	public int ProcessId => _process.Id;

	/// <summary>Gets whether the child process is still running.</summary>
	public bool IsRunning => !_process.HasExited;

	/// <summary>
	/// Starts a ModelHost process and waits until it completes the handshake.
	/// </summary>
	/// <param name="paths">Resolved Astra paths; used for the IPC endpoint and logs.</param>
	/// <param name="modelHostAssemblyPath">Absolute path to Astra.ModelHost.dll or the app host.</param>
	/// <param name="instanceName">Logical instance name; isolates endpoints per data root.</param>
	/// <param name="startupTimeout">Maximum startup time; defaults to 20 seconds.</param>
	/// <param name="runtimeName">
	/// Runtime to start: <c>llama</c> (default) or <c>echo</c>. Tests use <c>echo</c> so CI does
	/// not need model files or native inference.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <exception cref="ArgumentException">An argument is invalid.</exception>
	/// <exception cref="InvalidOperationException">
	/// The executable is missing, another instance is running, or the process exited early.
	/// </exception>
	/// <exception cref="TimeoutException">The process did not become ready in time.</exception>
	public static async Task<ModelHostProcess> StartAsync(
		AstraPaths paths,
		string modelHostAssemblyPath,
		string instanceName = "default",
		TimeSpan? startupTimeout = null,
		string? runtimeName = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(paths);
		ArgumentNullException.ThrowIfNull(modelHostAssemblyPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(modelHostAssemblyPath);
		ValidateInstanceName(instanceName);

		if (!Path.IsPathFullyQualified(modelHostAssemblyPath))
		{
			throw new ArgumentException(
				"The ModelHost assembly path must be absolute.",
				nameof(modelHostAssemblyPath));
		}

		if (!File.Exists(modelHostAssemblyPath))
		{
			throw new InvalidOperationException(
				$"ModelHost executable was not found at '{modelHostAssemblyPath}'.");
		}

		paths.EnsureDirectories();

		var endpoint = ResolveEndpoint(paths, instanceName);

		if (endpoint.Transport == ModelHostTransport.UnixSocket && File.Exists(endpoint.Address))
		{
			if (await IsLiveAsync(endpoint, cancellationToken).ConfigureAwait(false))
			{
				throw new InvalidOperationException(
					$"A ModelHost instance is already listening at '{endpoint.Address}'.");
			}

			File.Delete(endpoint.Address);
		}

		var startInfo = BuildStartInfo(modelHostAssemblyPath, endpoint, paths.Root, runtimeName);
		var process = new Process { StartInfo = startInfo };

		try
		{
			process.Start();
		}
		catch
		{
			process.Dispose();
			throw;
		}

		try
		{
			return await WaitForReadyAsync(process, endpoint, startupTimeout, cancellationToken)
				.ConfigureAwait(false);
		}
		catch
		{
			await KillAsync(process).ConfigureAwait(false);
			process.Dispose();
			throw;
		}
	}

	private static async Task<ModelHostProcess> WaitForReadyAsync(
		Process process,
		ModelHostEndpoint endpoint,
		TimeSpan? startupTimeout,
		CancellationToken cancellationToken)
	{
		var timeout = startupTimeout ?? TimeSpan.FromSeconds(20);
		var deadline = DateTime.UtcNow + timeout;
		Exception? lastError = null;

		while (DateTime.UtcNow < deadline)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (process.HasExited)
			{
				var exitCode = process.ExitCode;
				throw new InvalidOperationException(
					$"ModelHost exited with code {exitCode} before completing startup.");
			}

			try
			{
				var client = await ModelHostClient
					.ConnectAsync(endpoint, TimeSpan.FromSeconds(2), cancellationToken)
					.ConfigureAwait(false);

				if (client.Info.ProcessId != process.Id)
				{
					await client.DisposeAsync().ConfigureAwait(false);
					throw new InvalidOperationException(
						"Connected to a different ModelHost process; the endpoint is already in use.");
				}

				return new ModelHostProcess(process, endpoint, client);
			}
			catch (Exception exception) when (IsTransient(exception))
			{
				lastError = exception;
			}

			await Task.Delay(150, cancellationToken).ConfigureAwait(false);
		}

		throw new TimeoutException(
			$"ModelHost did not become ready within {timeout}.",
			lastError);
	}

	/// <summary>Requests graceful shutdown and waits for the process to exit.</summary>
	public async Task StopAsync(CancellationToken cancellationToken = default)
	{
		if (_process.HasExited)
		{
			return;
		}

		try
		{
			await Client
				.ShutdownAsync(TimeSpan.FromSeconds(3), cancellationToken)
				.ConfigureAwait(false);
		}
		catch (Exception exception) when (
			exception is InvalidOperationException or TimeoutException or OperationCanceledException)
		{
			// The process may already be gone; fall through to the bounded wait.
		}

		using var waitSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));

		try
		{
			await _process.WaitForExitAsync(waitSource.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			await KillAsync(_process).ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		try
		{
			await StopAsync().ConfigureAwait(false);
		}
		finally
		{
			await Client.DisposeAsync().ConfigureAwait(false);
			_process.Dispose();
		}
	}

	private static ModelHostEndpoint ResolveEndpoint(AstraPaths paths, string instanceName)
	{
		var suffix = ComputeInstanceHash(paths.Root);

		if (OperatingSystem.IsWindows())
		{
			return ModelHostEndpoint.ForNamedPipe(
				$"astra-modelhost-{instanceName}-{suffix}");
		}

		return ModelHostEndpoint.ForUnixSocket(
			Path.Combine(
				paths.IpcDirectory,
				$"modelhost-{instanceName}-{suffix}.sock"));
	}

	private static ProcessStartInfo BuildStartInfo(
		string modelHostAssemblyPath,
		ModelHostEndpoint endpoint,
		string dataRoot,
		string? runtimeName)
	{
		var directory = Path.GetDirectoryName(modelHostAssemblyPath)
			?? throw new ArgumentException(
				"The ModelHost assembly path has no directory.",
				nameof(modelHostAssemblyPath));

		var appHost = Path.Combine(
			directory,
			OperatingSystem.IsWindows() ? "Astra.ModelHost.exe" : "Astra.ModelHost");

		ProcessStartInfo startInfo;

		if (File.Exists(appHost) && (OperatingSystem.IsWindows() || IsExecutable(appHost)))
		{
			startInfo = new ProcessStartInfo(appHost);
		}
		else
		{
			startInfo = new ProcessStartInfo("dotnet");
			startInfo.ArgumentList.Add(modelHostAssemblyPath);
		}

		startInfo.UseShellExecute = false;
		startInfo.WorkingDirectory = directory;
		startInfo.ArgumentList.Add("--endpoint");
		startInfo.ArgumentList.Add(endpoint.ToUriString());
		startInfo.ArgumentList.Add("--data-root");
		startInfo.ArgumentList.Add(dataRoot);

		if (runtimeName is not null)
		{
			startInfo.ArgumentList.Add("--runtime");
			startInfo.ArgumentList.Add(runtimeName);
		}

		return startInfo;
	}

	private static async Task<bool> IsLiveAsync(
		ModelHostEndpoint endpoint,
		CancellationToken cancellationToken)
	{
		try
		{
			var client = await ModelHostClient
				.ConnectAsync(endpoint, TimeSpan.FromMilliseconds(500), cancellationToken)
				.ConfigureAwait(false);
			await client.DisposeAsync().ConfigureAwait(false);
			return true;
		}
		catch (Exception exception) when (IsTransient(exception))
		{
			return false;
		}
	}

	private static bool IsTransient(Exception exception) =>
		exception is TimeoutException or IOException or SocketException or HttpRequestException ||
		(exception is RpcException rpc &&
			rpc.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded);

	private static async Task KillAsync(Process process)
	{
		if (process.HasExited)
		{
			return;
		}

		process.Kill(entireProcessTree: true);
		await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
	}

	private static string ComputeInstanceHash(string root)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(root));

		return Convert
			.ToHexString(hash.AsSpan(0, 4))
			.ToLowerInvariant();
	}

	private static bool IsExecutable(string path)
	{
		if (OperatingSystem.IsWindows())
		{
			return true;
		}

		return (File.GetUnixFileMode(path) & UnixFileMode.UserExecute) != 0;
	}

	private static void ValidateInstanceName(string instanceName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);

		if (instanceName.Length > 32)
		{
			throw new ArgumentException(
				"Instance names must not exceed 32 characters.",
				nameof(instanceName));
		}

		foreach (var character in instanceName)
		{
			if (!char.IsAsciiLetterLower(character) &&
				!char.IsAsciiDigit(character) &&
				character != '-')
			{
				throw new ArgumentException(
					"Instance names may contain only lowercase ASCII letters, digits and '-'.",
					nameof(instanceName));
			}
		}
	}
}
