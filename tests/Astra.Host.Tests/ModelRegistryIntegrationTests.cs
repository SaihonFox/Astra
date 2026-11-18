using Astra.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Astra.Host.Tests;

public sealed class ModelRegistryIntegrationTests : IClassFixture<AstraWebApplicationFactory>
{
	private readonly AstraWebApplicationFactory _factory;

	public ModelRegistryIntegrationTests(AstraWebApplicationFactory factory)
	{
		_factory = factory;
	}

	[Fact]
	public async Task ModelRegistryIsWiredAndPersistsMetadata()
	{
		var registry = _factory.Services.GetRequiredService<IModelRegistry>();

		var descriptor = new ModelDescriptor
		{
			Id = "integration-test-model",
			Name = "Integration Test Model",
			Path = Path.Combine(_factory.DataDirectory, "models", "test.gguf"),
			Quantization = "Q4_K_M",
			SizeBytes = 1024,
			Capabilities = ModelCapabilities.Chat,
		};

		var stored = await registry.AddAsync(descriptor);
		var loaded = await registry.GetAsync(descriptor.Id);

		Assert.Equal(stored, loaded);
		Assert.True(stored.CreatedAt > DateTimeOffset.UnixEpoch);
	}
}
