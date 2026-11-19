using System.Globalization;
using System.IO.Pipes;
using Astra.Core.Inference;
using Astra.Infrastructure;
using Astra.ModelHost.Contracts;
using Astra.ModelHost.Services;
using LLama.Native;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateSlimBuilder(args);

var endpointValue = builder.Configuration["endpoint"];
if (string.IsNullOrWhiteSpace(endpointValue))
{
	throw new InvalidOperationException(
		"ModelHost requires --endpoint pipe:<name> or --endpoint unix:<absolute path>.");
}

var endpoint = ModelHostEndpoint.Parse(endpointValue);

var dataRoot = builder.Configuration["data-root"];
var paths = AstraPaths.Resolve(dataRoot);
paths.EnsureDirectories();

if (endpoint.Transport == ModelHostTransport.UnixSocket &&
	OperatingSystem.IsWindows())
{
	throw new InvalidOperationException(
		"Unix socket endpoints are not used for ModelHost on Windows; use a named pipe.");
}

if (endpoint.Transport == ModelHostTransport.NamedPipe &&
	!OperatingSystem.IsWindows())
{
	throw new InvalidOperationException(
		"Named pipe endpoints are supported only on Windows.");
}

builder.Host.UseSerilog((context, _, configuration) => configuration
	.ReadFrom.Configuration(context.Configuration)
	.Enrich.FromLogContext()
	.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
	.WriteTo.File(
		new CompactJsonFormatter(),
		Path.Combine(paths.LogsDirectory, "modelhost-.jsonl"),
		rollingInterval: RollingInterval.Day,
		retainedFileCountLimit: 14,
		fileSizeLimitBytes: 64 * 1024 * 1024,
		rollOnFileSizeLimit: true));

var runtimeName = builder.Configuration["runtime"] ?? "llama";

if (string.Equals(runtimeName, "llama", StringComparison.Ordinal))
{
	builder.Services.AddSingleton(new ModelHostRuntimeInfo("llamasharp"));
	builder.Services.AddSingleton<ILocalModelRuntime, LlamaSharpModelRuntime>();
}
else if (string.Equals(runtimeName, "echo", StringComparison.Ordinal))
{
	builder.Services.AddSingleton(new ModelHostRuntimeInfo("echo"));
	builder.Services.AddSingleton<ILocalModelRuntime, EchoModelRuntime>();
}
else
{
	throw new InvalidOperationException(
		$"Unknown runtime '{runtimeName}'. Supported values: llama, echo.");
}
builder.Services.AddGrpc(options =>
{
	const int maxMessageSize = 8 * 1024 * 1024;
	options.MaxReceiveMessageSize = maxMessageSize;
	options.MaxSendMessageSize = maxMessageSize;
});

if (endpoint.Transport == ModelHostTransport.NamedPipe)
{
	// Restrict the pipe to the current user instead of relying on the machine-wide default.
	builder.Services.Configure<NamedPipeTransportOptions>(options =>
	{
		options.CreateNamedPipeServerStream = static context =>
			new NamedPipeServerStream(
				context.NamedPipeEndPoint.PipeName,
				PipeDirection.InOut,
				NamedPipeServerStream.MaxAllowedServerInstances,
				PipeTransmissionMode.Byte,
				context.PipeOptions | PipeOptions.CurrentUserOnly);
	});
}

builder.WebHost.ConfigureKestrel(kestrel =>
{
	if (endpoint.Transport == ModelHostTransport.NamedPipe)
	{
		kestrel.ListenNamedPipe(
			endpoint.Address,
			listen => listen.Protocols = HttpProtocols.Http2);
	}
	else
	{
		kestrel.ListenUnixSocket(
			endpoint.Address,
			listen => listen.Protocols = HttpProtocols.Http2);
	}
});

var app = builder.Build();

if (string.Equals(runtimeName, "llama", StringComparison.Ordinal))
{
	// llama.cpp writes to stderr by default; route warnings and errors through Serilog and
	// drop the very verbose per-tensor information messages.
	NativeLogConfig.llama_log_set((level, message) =>
	{
		switch (level)
		{
			case LLamaLogLevel.Error:
				Program.LogNativeError(app.Logger, message);
				break;
			case LLamaLogLevel.Warning:
				Program.LogNativeWarning(app.Logger, message);
				break;
			default:
				break;
		}
	});
}

Program.LogRuntime(app.Logger, runtimeName);

if (string.Equals(runtimeName, "echo", StringComparison.Ordinal))
{
	Program.LogEchoRuntime(app.Logger);
}

app.MapGrpcService<ModelHostService>();

await app.RunAsync();

public partial class Program
{
	[LoggerMessage(
		EventId = 1,
		Level = LogLevel.Information,
		Message = "ModelHost started with runtime '{RuntimeName}'.")]
	public static partial void LogRuntime(Microsoft.Extensions.Logging.ILogger logger, string runtimeName);

	[LoggerMessage(
		EventId = 2,
		Level = LogLevel.Warning,
		Message = "ModelHost is using the echo runtime. It does not load a real model; " +
			"this mode exists for tests and diagnostics only.")]
	public static partial void LogEchoRuntime(Microsoft.Extensions.Logging.ILogger logger);

	[LoggerMessage(
		EventId = 3,
		Level = LogLevel.Warning,
		Message = "llama.cpp: {Message}")]
	public static partial void LogNativeWarning(Microsoft.Extensions.Logging.ILogger logger, string message);

	[LoggerMessage(
		EventId = 4,
		Level = LogLevel.Error,
		Message = "llama.cpp: {Message}")]
	public static partial void LogNativeError(Microsoft.Extensions.Logging.ILogger logger, string message);
}
