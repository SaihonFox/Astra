using Astra.Infrastructure.Database;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Astra.Host.Health;

/// <summary>
/// Verifies that the primary database accepts a query.
/// </summary>
internal sealed class DatabaseHealthCheck : IHealthCheck
{
	private readonly SqliteConnectionFactory _connectionFactory;

	public DatabaseHealthCheck(SqliteConnectionFactory connectionFactory)
	{
		_connectionFactory = connectionFactory;
	}

	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		try
		{
			await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT 1;";
			await command.ExecuteScalarAsync(cancellationToken);

			return HealthCheckResult.Healthy();
		}
#pragma warning disable CA1031 // Health checks must convert any probe failure into an unhealthy result.
		catch (Exception exception)
#pragma warning restore CA1031
		{
			return HealthCheckResult.Unhealthy("The database is not available.", exception);
		}
	}
}
