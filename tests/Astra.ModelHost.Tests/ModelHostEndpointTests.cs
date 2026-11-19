using Astra.ModelHost.Contracts;

namespace Astra.ModelHost.Tests;

public sealed class ModelHostEndpointTests
{
	[Fact]
	public void NamedPipeRoundTrips()
	{
		var endpoint = ModelHostEndpoint.ForNamedPipe("astra-modelhost-test");

		Assert.Equal(ModelHostTransport.NamedPipe, endpoint.Transport);
		Assert.Equal("pipe:astra-modelhost-test", endpoint.ToUriString());

		var parsed = ModelHostEndpoint.Parse(endpoint.ToUriString());
		Assert.Equal(endpoint, parsed);
	}

	[Fact]
	public void UnixSocketRoundTrips()
	{
		var path = Path.Combine(Path.GetTempPath(), "astra-test.sock");
		var endpoint = ModelHostEndpoint.ForUnixSocket(path);

		Assert.Equal(ModelHostTransport.UnixSocket, endpoint.Transport);
		Assert.Equal($"unix:{path}", endpoint.ToUriString());
		Assert.Equal(endpoint, ModelHostEndpoint.Parse(endpoint.ToUriString()));
	}

	[Theory]
	[InlineData("")]
	[InlineData("http://localhost:5000")]
	[InlineData("unix:relative.sock")]
	[InlineData("pipe:bad name")]
	public void ParseRejectsInvalidValues(string value)
	{
		Assert.Throws<ArgumentException>(() => ModelHostEndpoint.Parse(value));
	}

	[Fact]
	public void UnixSocketRejectsTooLongPaths()
	{
		var path = Path.Combine(
			Path.GetTempPath(),
			new string('a', ModelHostEndpoint.MaxUnixSocketPathBytes),
			"model.sock");

		Assert.Throws<ArgumentException>(() => ModelHostEndpoint.ForUnixSocket(path));
	}
}
