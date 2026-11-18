using System.Net;

namespace Astra.Host.Tests;

public sealed class HealthEndpointTests : IClassFixture<AstraWebApplicationFactory>
{
	private readonly AstraWebApplicationFactory _factory;

	public HealthEndpointTests(AstraWebApplicationFactory factory)
	{
		_factory = factory;
	}

	[Fact]
	public async Task HealthEndpointReturnsHealthyAndCreatesDatabase()
	{
		using var client = _factory.CreateClient();
		using var response = await client.GetAsync("/health");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
		Assert.True(File.Exists(Path.Combine(_factory.DataDirectory, "database", "astra.db")));
	}
}
