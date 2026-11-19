namespace Astra.Infrastructure.Tests;

public sealed class AstraPathsTests
{
	[Fact]
	public void ResolveAcceptsAbsoluteOverride()
	{
		var root = Path.Combine(Path.GetTempPath(), "astra-tests", Guid.NewGuid().ToString("N"));

		var paths = AstraPaths.Resolve(root);

		Assert.Equal(Path.GetFullPath(root), paths.Root);
		Assert.Equal(Path.Combine(paths.Root, "database", "astra.db"), paths.DatabaseFile);
	}

	[Fact]
	public void ResolveRejectsRelativeOverride()
	{
		Assert.Throws<ArgumentException>(() => AstraPaths.Resolve("relative/path"));
	}

	[Fact]
	public void EnsureDirectoriesCreatesRequiredAreas()
	{
		var root = Path.Combine(Path.GetTempPath(), "astra-tests", Guid.NewGuid().ToString("N"));
		var paths = AstraPaths.Resolve(root);

		try
		{
			paths.EnsureDirectories();

			Assert.True(Directory.Exists(paths.Root));
			Assert.True(Directory.Exists(paths.DatabaseDirectory));
			Assert.True(Directory.Exists(paths.LogsDirectory));
			Assert.True(Directory.Exists(paths.IpcDirectory));
			Assert.False(Directory.Exists(paths.ModelsDirectory));
		}
		finally
		{
			if (Directory.Exists(root))
			{
				Directory.Delete(root, recursive: true);
			}
		}
	}

	[Fact]
	public void EnsureDirectoriesRestrictsAccessOnUnix()
	{
		if (OperatingSystem.IsWindows())
		{
			return;
		}

		var root = Path.Combine(Path.GetTempPath(), "astra-tests", Guid.NewGuid().ToString("N"));
		var paths = AstraPaths.Resolve(root);

		try
		{
			paths.EnsureDirectories();

			const UnixFileMode expected =
				UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
			Assert.Equal(expected, File.GetUnixFileMode(paths.Root));
			Assert.Equal(expected, File.GetUnixFileMode(paths.DatabaseDirectory));
			Assert.Equal(expected, File.GetUnixFileMode(paths.IpcDirectory));
		}
		finally
		{
			if (Directory.Exists(root))
			{
				Directory.Delete(root, recursive: true);
			}
		}
	}
}
