using System.Globalization;
using Astra.Core;
using Astra.Core.Models;
using Microsoft.Data.Sqlite;

namespace Astra.Infrastructure.Database;

/// <summary>
/// SQLite-backed model registry.
/// </summary>
public sealed class SqliteModelRegistry : IModelRegistry
{
	private const string Columns =
		"""
		id, name, path, format, quantization, size_bytes, context_window, capabilities,
		source, license, required_ram_bytes, required_vram_bytes, preferred_tasks,
		model_version, test_status, tested_at, test_notes, created_at, updated_at
		""";

	private readonly SqliteConnectionFactory _connectionFactory;
	private readonly IClock _clock;

	/// <summary>Initializes a registry.</summary>
	/// <param name="connectionFactory">Connection factory.</param>
	/// <param name="clock">Clock used for registry-managed timestamps.</param>
	public SqliteModelRegistry(SqliteConnectionFactory connectionFactory, IClock clock)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		_clock = clock ?? throw new ArgumentNullException(nameof(clock));
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<ModelDescriptor>> ListAsync(
		CancellationToken cancellationToken = default)
	{
		await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText =
			$"""
			SELECT {Columns}
			FROM models
			ORDER BY name COLLATE NOCASE, id;
			""";

		var models = new List<ModelDescriptor>();
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		while (await reader.ReadAsync(cancellationToken))
		{
			models.Add(ReadDescriptor(reader));
		}

		return models;
	}

	/// <inheritdoc />
	public async Task<ModelDescriptor?> GetAsync(
		string id,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);

		await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
		return await FindAsync(connection, id, cancellationToken);
	}

	/// <inheritdoc />
	public async Task<ModelDescriptor> AddAsync(
		ModelDescriptor descriptor,
		CancellationToken cancellationToken = default)
	{
		ModelDescriptorValidator.Validate(descriptor);

		var now = _clock.UtcNow.ToUniversalTime();
		var stored = descriptor with { CreatedAt = now, UpdatedAt = now };

		await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

		await using (var idCheck = connection.CreateCommand())
		{
			idCheck.CommandText = "SELECT 1 FROM models WHERE id = $id LIMIT 1;";
			idCheck.Parameters.AddWithValue("$id", stored.Id);

			if (await idCheck.ExecuteScalarAsync(cancellationToken) is not null)
			{
				throw new InvalidOperationException(
					$"A model with identifier '{stored.Id}' is already registered.");
			}
		}

		await EnsurePathAvailableAsync(connection, stored.Path, excludedId: null, cancellationToken);

		try
		{
			await using var command = connection.CreateCommand();
			command.CommandText =
				$"""
				INSERT INTO models ({Columns})
				VALUES (
					$id, $name, $path, $format, $quantization, $sizeBytes, $contextWindow,
					$capabilities, $source, $license, $requiredRamBytes, $requiredVramBytes,
					$preferredTasks, $modelVersion, $testStatus, $testedAt, $testNotes,
					$createdAt, $updatedAt);
				""";
			AddParameters(command, stored);

			await command.ExecuteNonQueryAsync(cancellationToken);
		}
		catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
		{
			throw new InvalidOperationException(
				$"A model with identifier '{stored.Id}' or path '{stored.Path}' is already registered.",
				exception);
		}

		return stored;
	}

	/// <inheritdoc />
	public async Task<ModelDescriptor> UpdateAsync(
		ModelDescriptor descriptor,
		CancellationToken cancellationToken = default)
	{
		ModelDescriptorValidator.Validate(descriptor);

		await using var connection = await _connectionFactory.OpenAsync(cancellationToken);

		var existing = await FindAsync(connection, descriptor.Id, cancellationToken)
			?? throw new KeyNotFoundException($"Model '{descriptor.Id}' is not registered.");

		await EnsurePathAvailableAsync(
			connection,
			descriptor.Path,
			descriptor.Id,
			cancellationToken);

		var stored = descriptor with
		{
			CreatedAt = existing.CreatedAt,
			UpdatedAt = _clock.UtcNow.ToUniversalTime(),
		};

		await using var command = connection.CreateCommand();
		command.CommandText =
			"""
			UPDATE models SET
				name = $name,
				path = $path,
				format = $format,
				quantization = $quantization,
				size_bytes = $sizeBytes,
				context_window = $contextWindow,
				capabilities = $capabilities,
				source = $source,
				license = $license,
				required_ram_bytes = $requiredRamBytes,
				required_vram_bytes = $requiredVramBytes,
				preferred_tasks = $preferredTasks,
				model_version = $modelVersion,
				test_status = $testStatus,
				tested_at = $testedAt,
				test_notes = $testNotes,
				updated_at = $updatedAt
			WHERE id = $id;
			""";
		AddParameters(command, stored);

		try
		{
			var affected = await command.ExecuteNonQueryAsync(cancellationToken);

			if (affected == 0)
			{
				throw new KeyNotFoundException($"Model '{descriptor.Id}' is not registered.");
			}
		}
		catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
		{
			throw new InvalidOperationException(
				$"Another model already uses path '{stored.Path}'.",
				exception);
		}

		return stored;
	}

	/// <inheritdoc />
	public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);

		await using var connection = await _connectionFactory.OpenAsync(cancellationToken);
		await using var command = connection.CreateCommand();
		command.CommandText = "DELETE FROM models WHERE id = $id;";
		command.Parameters.AddWithValue("$id", id);

		return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
	}

	/// <inheritdoc />
	public async Task<ModelAvailability> CheckAvailabilityAsync(
		string id,
		CancellationToken cancellationToken = default)
	{
		var descriptor = await GetAsync(id, cancellationToken)
			?? throw new KeyNotFoundException($"Model '{id}' is not registered.");

		try
		{
			var file = new FileInfo(descriptor.Path);

			if (!file.Exists)
			{
				return ModelAvailability.Missing;
			}

			using var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

			return ModelAvailability.Available;
		}
		catch (FileNotFoundException)
		{
			return ModelAvailability.Missing;
		}
		catch (DirectoryNotFoundException)
		{
			return ModelAvailability.Missing;
		}
		catch (UnauthorizedAccessException)
		{
			return ModelAvailability.Unreadable;
		}
		catch (IOException)
		{
			return ModelAvailability.Unreadable;
		}
	}

	private static async Task<ModelDescriptor?> FindAsync(
		SqliteConnection connection,
		string id,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = $"SELECT {Columns} FROM models WHERE id = $id LIMIT 1;";
		command.Parameters.AddWithValue("$id", id);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		return await reader.ReadAsync(cancellationToken) ? ReadDescriptor(reader) : null;
	}

	private static async Task EnsurePathAvailableAsync(
		SqliteConnection connection,
		string path,
		string? excludedId,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = excludedId is null
			? "SELECT id FROM models WHERE path = $path LIMIT 1;"
			: "SELECT id FROM models WHERE path = $path AND id <> $id LIMIT 1;";
		command.Parameters.AddWithValue("$path", path);

		if (excludedId is not null)
		{
			command.Parameters.AddWithValue("$id", excludedId);
		}

		if (await command.ExecuteScalarAsync(cancellationToken) is string otherId)
		{
			throw new InvalidOperationException(
				$"The model path is already used by '{otherId}'.");
		}
	}

	private static void AddParameters(SqliteCommand command, ModelDescriptor descriptor)
	{
		command.Parameters.AddWithValue("$id", descriptor.Id);
		command.Parameters.AddWithValue("$name", descriptor.Name);
		command.Parameters.AddWithValue("$path", descriptor.Path);
		command.Parameters.AddWithValue("$format", ToStorage(descriptor.Format));
		command.Parameters.AddWithValue(
			"$quantization",
			(object?)descriptor.Quantization ?? DBNull.Value);
		command.Parameters.AddWithValue("$sizeBytes", descriptor.SizeBytes);
		command.Parameters.AddWithValue(
			"$contextWindow",
			(object?)descriptor.ContextWindow ?? DBNull.Value);
		command.Parameters.AddWithValue("$capabilities", (int)descriptor.Capabilities);
		command.Parameters.AddWithValue("$source", (object?)descriptor.Source ?? DBNull.Value);
		command.Parameters.AddWithValue("$license", (object?)descriptor.License ?? DBNull.Value);
		command.Parameters.AddWithValue(
			"$requiredRamBytes",
			(object?)descriptor.RequiredRamBytes ?? DBNull.Value);
		command.Parameters.AddWithValue(
			"$requiredVramBytes",
			(object?)descriptor.RequiredVramBytes ?? DBNull.Value);
		command.Parameters.AddWithValue(
			"$preferredTasks",
			(object?)descriptor.PreferredTasks ?? DBNull.Value);
		command.Parameters.AddWithValue(
			"$modelVersion",
			(object?)descriptor.ModelVersion ?? DBNull.Value);
		command.Parameters.AddWithValue("$testStatus", ToStorage(descriptor.TestStatus));
		command.Parameters.AddWithValue(
			"$testedAt",
			(object?)FormatTimestamp(descriptor.TestedAt) ?? DBNull.Value);
		command.Parameters.AddWithValue(
			"$testNotes",
			(object?)descriptor.TestNotes ?? DBNull.Value);
		command.Parameters.AddWithValue("$createdAt", FormatTimestamp(descriptor.CreatedAt));
		command.Parameters.AddWithValue("$updatedAt", FormatTimestamp(descriptor.UpdatedAt));
	}

	private static ModelDescriptor ReadDescriptor(SqliteDataReader reader) => new()
	{
		Id = reader.GetString(0),
		Name = reader.GetString(1),
		Path = reader.GetString(2),
		Format = ParseFormat(reader.GetString(3)),
		Quantization = reader.IsDBNull(4) ? null : reader.GetString(4),
		SizeBytes = reader.GetInt64(5),
		ContextWindow = reader.IsDBNull(6) ? null : reader.GetInt32(6),
		Capabilities = (ModelCapabilities)reader.GetInt32(7),
		Source = reader.IsDBNull(8) ? null : reader.GetString(8),
		License = reader.IsDBNull(9) ? null : reader.GetString(9),
		RequiredRamBytes = reader.IsDBNull(10) ? null : reader.GetInt64(10),
		RequiredVramBytes = reader.IsDBNull(11) ? null : reader.GetInt64(11),
		PreferredTasks = reader.IsDBNull(12) ? null : reader.GetString(12),
		ModelVersion = reader.IsDBNull(13) ? null : reader.GetString(13),
		TestStatus = ParseTestStatus(reader.GetString(14)),
		TestedAt = reader.IsDBNull(15) ? null : ParseTimestamp(reader.GetString(15)),
		TestNotes = reader.IsDBNull(16) ? null : reader.GetString(16),
		CreatedAt = ParseTimestamp(reader.GetString(17)),
		UpdatedAt = ParseTimestamp(reader.GetString(18)),
	};

	private static string ToStorage(ModelFormat value) => value switch
	{
		ModelFormat.Gguf => "gguf",
		_ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported model format."),
	};

	private static ModelFormat ParseFormat(string value) => value switch
	{
		"gguf" => ModelFormat.Gguf,
		_ => throw new InvalidDataException($"Unknown model format '{value}'."),
	};

	private static string ToStorage(ModelTestStatus value) => value switch
	{
		ModelTestStatus.Untested => "untested",
		ModelTestStatus.Passed => "passed",
		ModelTestStatus.Failed => "failed",
		_ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported test status."),
	};

	private static ModelTestStatus ParseTestStatus(string value) => value switch
	{
		"untested" => ModelTestStatus.Untested,
		"passed" => ModelTestStatus.Passed,
		"failed" => ModelTestStatus.Failed,
		_ => throw new InvalidDataException($"Unknown model test status '{value}'."),
	};

	private static string? FormatTimestamp(DateTimeOffset? value) =>
		value?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

	private static string FormatTimestamp(DateTimeOffset value) =>
		value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

	private static DateTimeOffset ParseTimestamp(string value) =>
		DateTimeOffset
			.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
			.ToUniversalTime();
}
