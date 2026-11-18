namespace Astra.Infrastructure;

/// <summary>
/// Resolves the data root and the storage areas defined by the specification.
/// </summary>
public sealed class AstraPaths
{
	/// <summary>Environment variable that overrides the data root.</summary>
	public const string DataDirectoryEnvironmentVariable = "ASTRA_DATA_DIR";

	/// <summary>Configuration key that overrides the data root.</summary>
	public const string DataDirectoryConfigurationKey = "Astra:DataDirectory";

	private AstraPaths(string root)
	{
		Root = root;
	}

	/// <summary>Gets the absolute data root.</summary>
	public string Root { get; }

	/// <summary>Gets the configuration area.</summary>
	public string ConfigurationDirectory => Path.Combine(Root, "configuration");

	/// <summary>Gets the database area.</summary>
	public string DatabaseDirectory => Path.Combine(Root, "database");

	/// <summary>Gets the primary database file path.</summary>
	public string DatabaseFile => Path.Combine(DatabaseDirectory, "astra.db");

	/// <summary>Gets the models area.</summary>
	public string ModelsDirectory => Path.Combine(Root, "models");

	/// <summary>Gets the knowledge area.</summary>
	public string KnowledgeDirectory => Path.Combine(Root, "knowledge");

	/// <summary>Gets the projects area.</summary>
	public string ProjectsDirectory => Path.Combine(Root, "projects");

	/// <summary>Gets the extensions area.</summary>
	public string ExtensionsDirectory => Path.Combine(Root, "extensions");

	/// <summary>Gets the logs area.</summary>
	public string LogsDirectory => Path.Combine(Root, "logs");

	/// <summary>Gets the cache area.</summary>
	public string CacheDirectory => Path.Combine(Root, "cache");

	/// <summary>Gets the backups area.</summary>
	public string BackupsDirectory => Path.Combine(Root, "backups");

	/// <summary>
	/// Resolves the data root from an explicit value, the environment, or the OS default.
	/// </summary>
	/// <param name="configuredRoot">Absolute root path, usually from configuration.</param>
	/// <exception cref="ArgumentException">The resolved path is not absolute.</exception>
	public static AstraPaths Resolve(string? configuredRoot = null)
	{
		var root = configuredRoot;
		if (string.IsNullOrWhiteSpace(root))
		{
			root = Environment.GetEnvironmentVariable(DataDirectoryEnvironmentVariable);
		}

		if (string.IsNullOrWhiteSpace(root))
		{
			root = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"Astra");
		}

		if (!Path.IsPathFullyQualified(root))
		{
			throw new ArgumentException(
				"The Astra data directory must be an absolute path.",
				nameof(configuredRoot));
		}

		return new AstraPaths(Path.GetFullPath(root));
	}

	/// <summary>
	/// Creates the directories required by the current stage with owner-only permissions on Unix.
	/// </summary>
	public void EnsureDirectories()
	{
		CreatePrivateDirectory(Root);
		CreatePrivateDirectory(DatabaseDirectory);
		CreatePrivateDirectory(LogsDirectory);
	}

	private static void CreatePrivateDirectory(string path)
	{
		if (Directory.Exists(path))
		{
			return;
		}

		Directory.CreateDirectory(path);

		if (!OperatingSystem.IsWindows())
		{
			File.SetUnixFileMode(
				path,
				UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
		}
	}
}
