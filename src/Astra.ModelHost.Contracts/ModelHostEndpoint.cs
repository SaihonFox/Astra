using System.Text;

namespace Astra.ModelHost.Contracts;

/// <summary>
/// Local endpoint of a ModelHost process.
/// </summary>
public sealed record ModelHostEndpoint
{
	/// <summary>Conservatively small Unix socket path limit in UTF-8 bytes.</summary>
	public const int MaxUnixSocketPathBytes = 100;

	/// <summary>Maximum named pipe name length.</summary>
	public const int MaxPipeNameLength = 200;

	private ModelHostEndpoint(ModelHostTransport transport, string address)
	{
		Transport = transport;
		Address = address;
	}

	/// <summary>Gets the transport kind.</summary>
	public ModelHostTransport Transport { get; }

	/// <summary>Gets the pipe name or the socket path.</summary>
	public string Address { get; }

	/// <summary>Creates a named pipe endpoint.</summary>
	/// <exception cref="ArgumentException">The pipe name is invalid.</exception>
	public static ModelHostEndpoint ForNamedPipe(string pipeName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

		if (pipeName.Length > MaxPipeNameLength)
		{
			throw new ArgumentException(
				$"Named pipe names must not exceed {MaxPipeNameLength} characters.",
				nameof(pipeName));
		}

		foreach (var character in pipeName)
		{
			if (!char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-')
			{
				throw new ArgumentException(
					"Named pipe names may contain only ASCII letters, digits, '.', '_' and '-'.",
					nameof(pipeName));
			}
		}

		return new ModelHostEndpoint(ModelHostTransport.NamedPipe, pipeName);
	}

	/// <summary>Creates a Unix domain socket endpoint.</summary>
	/// <exception cref="ArgumentException">The socket path is invalid.</exception>
	public static ModelHostEndpoint ForUnixSocket(string socketPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(socketPath);

		if (!Path.IsPathFullyQualified(socketPath))
		{
			throw new ArgumentException("Unix socket paths must be absolute.", nameof(socketPath));
		}

		if (Encoding.UTF8.GetByteCount(socketPath) > MaxUnixSocketPathBytes)
		{
			throw new ArgumentException(
				$"Unix socket paths must not exceed {MaxUnixSocketPathBytes} UTF-8 bytes.",
				nameof(socketPath));
		}

		if (socketPath.Any(char.IsControl))
		{
			throw new ArgumentException(
				"Unix socket paths must not contain control characters.",
				nameof(socketPath));
		}

		return new ModelHostEndpoint(ModelHostTransport.UnixSocket, socketPath);
	}

	/// <summary>Formats the endpoint for command-line transport.</summary>
	public string ToUriString() => Transport switch
	{
		ModelHostTransport.NamedPipe => $"pipe:{Address}",
		ModelHostTransport.UnixSocket => $"unix:{Address}",
		_ => throw new InvalidOperationException($"Unsupported transport '{Transport}'."),
	};

	/// <summary>Parses an endpoint produced by <see cref="ToUriString"/>.</summary>
	/// <exception cref="ArgumentException">The value is not a valid endpoint.</exception>
	public static ModelHostEndpoint Parse(string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value);

		const string pipePrefix = "pipe:";
		const string unixPrefix = "unix:";

		if (value.StartsWith(pipePrefix, StringComparison.OrdinalIgnoreCase))
		{
			return ForNamedPipe(value[pipePrefix.Length..]);
		}

		if (value.StartsWith(unixPrefix, StringComparison.OrdinalIgnoreCase))
		{
			return ForUnixSocket(value[unixPrefix.Length..]);
		}

		throw new ArgumentException(
			"Endpoints must start with 'pipe:' or 'unix:'.",
			nameof(value));
	}

	/// <inheritdoc />
	public override string ToString() => ToUriString();
}
