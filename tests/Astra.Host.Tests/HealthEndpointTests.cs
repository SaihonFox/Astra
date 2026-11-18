using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Astra.Host.Tests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
	private readonly WebApplicationFactory<Program> _factory;

	public HealthEndpointTests(WebApplicationFactory<Program> factory)
	{
		_factory = factory;
	}

	[Fact]
	public async Task HealthEndpointReturnsOk()
	{
		using var client = _factory.CreateClient();
		using var response = await client.GetAsync("/health");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}
}
