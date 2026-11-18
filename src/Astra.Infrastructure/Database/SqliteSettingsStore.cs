using System.Globalization;
using System.Text;
using Astra.Core;

namespace Astra.Infrastructure.Database;

/// <summary>
/// SQLite-backed settings store for small, non-secret values.
/// </summary>
public sealed class SqliteSettingsStore : ISettingsStore
{
	private const int MaxKeyLength = 200;
	private const int MaxValueBytes = 64 * 1024;

	private readonly SqliteConnectionFactory _connectionFactory;

	/// <summary>Initializes a settings store.</summary>
	public SqliteSettingsStore(SqliteConnectionFactory connectionFactory)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
	}

	/// <inheritdoc />
	public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
	{
		ValidateKey(key);

		await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = "SELECT value FROM settings WHERE key = $key LIMIT 1;";
		command.Parameters.AddWithValue("$key", key);

		var result = await command.ExecuteScalarAsync(cancellationToken);
		return result as string;
	}

	/// <inheritdoc />
	public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
	{
		ValidateKey(key);
		ValidateValue(value);

		await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText =
			"""
			INSERT INTO settings (key, value, updated_at)
			VALUES ($key, $value, $updatedAt)
			ON CONFLICT(key) DO UPDATE SET
				value = excluded.value,
				updated_at = excluded.updated_at;
			""";
		command.Parameters.AddWithValue("$key", key);
		command.Parameters.AddWithValue("$value", value);
		command.Parameters.AddWithValue(
			"$updatedAt",
			DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));

		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	/// <inheritdoc />
	public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
	{
		ValidateKey(key);

		await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = "DELETE FROM settings WHERE key = $key;";
		command.Parameters.AddWithValue("$key", key);

		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	private static void ValidateKey(string key)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);

		if (key.Length > MaxKeyLength)
		{
			throw new ArgumentException(
				$"Setting keys must not exceed {MaxKeyLength} characters.",
				nameof(key));
		}

		if (key.Any(char.IsControl))
		{
			throw new ArgumentException("Setting keys must not contain control characters.", nameof(key));
		}
	}

	private static void ValidateValue(string value)
	{
		ArgumentNullException.ThrowIfNull(value);

		if (Encoding.UTF8.GetByteCount(value) > MaxValueBytes)
		{
			throw new ArgumentException(
				$"Setting values must not exceed {MaxValueBytes} UTF-8 bytes.",
				nameof(value));
		}
	}
}
