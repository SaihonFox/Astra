using System.Globalization;
using System.IO.Pipes;
using Astra.Core.Inference;
using Astra.Infrastructure;
using Astra.ModelHost.Contracts;
using Astra.ModelHost.Services;
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

builder.Services.AddSingleton(new ModelHostRuntimeInfo("echo"));
builder.Services.AddSingleton<ILocalModelRuntime, EchoModelRuntime>();
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

Program.LogEchoRuntime(app.Logger);

app.MapGrpcService<ModelHostService>();

await app.RunAsync();

public partial class Program
{
	[LoggerMessage(
		EventId = 1,
		Level = LogLevel.Warning,
		Message = "ModelHost is using the echo runtime. It does not load a real model; " +
			"the LLamaSharp adapter is not implemented yet.")]
	public static partial void LogEchoRuntime(Microsoft.Extensions.Logging.ILogger logger);
}
