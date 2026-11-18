namespace Astra.Core.Models;

/// <summary>
/// Validates model descriptors before they are persisted or consumed.
/// </summary>
public static class ModelDescriptorValidator
{
	/// <summary>Maximum length of a model identifier.</summary>
	public const int MaxIdLength = 100;

	/// <summary>Maximum length of a model display name.</summary>
	public const int MaxNameLength = 200;

	/// <summary>Maximum length of a model path.</summary>
	public const int MaxPathLength = 4096;

	/// <summary>Maximum length of a quantization label.</summary>
	public const int MaxQuantizationLength = 40;

	/// <summary>Maximum length of a source reference.</summary>
	public const int MaxSourceLength = 500;

	/// <summary>Maximum length of a license label.</summary>
	public const int MaxLicenseLength = 100;

	/// <summary>Maximum length of the preferred-tasks hint.</summary>
	public const int MaxPreferredTasksLength = 500;

	/// <summary>Maximum length of a model version label.</summary>
	public const int MaxModelVersionLength = 100;

	/// <summary>Maximum length of validation notes.</summary>
	public const int MaxTestNotesLength = 2000;

	/// <summary>Largest accepted context window.</summary>
	public const int MaxContextWindow = 10_000_000;

	private const ModelCapabilities AllCapabilities =
		ModelCapabilities.Chat |
		ModelCapabilities.Completion |
		ModelCapabilities.Embedding |
		ModelCapabilities.Vision;

	/// <summary>
	/// Validates a descriptor.
	/// </summary>
	/// <param name="descriptor">Descriptor to validate.</param>
	/// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">A field is invalid.</exception>
	public static void Validate(ModelDescriptor descriptor)
	{
		ArgumentNullException.ThrowIfNull(descriptor);

		ValidateId(descriptor.Id);
		ValidateText(descriptor.Name, MaxNameLength, nameof(descriptor.Name), required: true);
		ValidatePath(descriptor.Path);
		ValidateEnum(descriptor.Format, nameof(descriptor.Format));
		ValidateText(
			descriptor.Quantization,
			MaxQuantizationLength,
			nameof(descriptor.Quantization),
			required: false);
		ValidateNonNegative(descriptor.SizeBytes, nameof(descriptor.SizeBytes));
		ValidateContextWindow(descriptor.ContextWindow);
		ValidateCapabilities(descriptor.Capabilities);
		ValidateText(descriptor.Source, MaxSourceLength, nameof(descriptor.Source), required: false);
		ValidateText(descriptor.License, MaxLicenseLength, nameof(descriptor.License), required: false);
		ValidateOptionalNonNegative(descriptor.RequiredRamBytes, nameof(descriptor.RequiredRamBytes));
		ValidateOptionalNonNegative(descriptor.RequiredVramBytes, nameof(descriptor.RequiredVramBytes));
		ValidateText(
			descriptor.PreferredTasks,
			MaxPreferredTasksLength,
			nameof(descriptor.PreferredTasks),
			required: false);
		ValidateText(
			descriptor.ModelVersion,
			MaxModelVersionLength,
			nameof(descriptor.ModelVersion),
			required: false);
		ValidateEnum(descriptor.TestStatus, nameof(descriptor.TestStatus));
		ValidateText(
			descriptor.TestNotes,
			MaxTestNotesLength,
			nameof(descriptor.TestNotes),
			required: false);

		if (descriptor.TestedAt == default(DateTimeOffset))
		{
			throw new ArgumentException(
				"The test timestamp must be a real timestamp when supplied.",
				nameof(descriptor));
		}
	}

	private static void ValidateId(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			throw new ArgumentException("A model identifier is required.", nameof(id));
		}

		if (id.Length > MaxIdLength)
		{
			throw new ArgumentException(
				$"Model identifiers must not exceed {MaxIdLength} characters.",
				nameof(id));
		}

		if (!char.IsAsciiLetterLower(id[0]) && !char.IsAsciiDigit(id[0]))
		{
			throw new ArgumentException(
				"Model identifiers must start with a lowercase letter or a digit.",
				nameof(id));
		}

		foreach (var character in id)
		{
			if (!char.IsAsciiLetterLower(character) &&
				!char.IsAsciiDigit(character) &&
				character is not '.' and not '_' and not '-')
			{
				throw new ArgumentException(
					"Model identifiers may contain only lowercase ASCII letters, digits, '.', '_' and '-'.",
					nameof(id));
			}
		}
	}

	private static void ValidatePath(string path)
	{
		ValidateText(path, MaxPathLength, nameof(path), required: true);

		if (!System.IO.Path.IsPathFullyQualified(path))
		{
			throw new ArgumentException("Model paths must be absolute.", nameof(path));
		}

		if (!string.Equals(
				System.IO.Path.GetExtension(path),
				".gguf",
				StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException("Only .gguf model files are supported.", nameof(path));
		}
	}

	private static void ValidateContextWindow(int? contextWindow)
	{
		if (contextWindow is null)
		{
			return;
		}

		if (contextWindow <= 0 || contextWindow > MaxContextWindow)
		{
			throw new ArgumentException(
				$"The context window must be between 1 and {MaxContextWindow} tokens.",
				nameof(contextWindow));
		}
	}

	private static void ValidateCapabilities(ModelCapabilities capabilities)
	{
		if ((capabilities & ~AllCapabilities) != 0)
		{
			throw new ArgumentException(
				"The descriptor contains undefined model capabilities.",
				nameof(capabilities));
		}
	}

	private static void ValidateEnum<TEnum>(TEnum value, string parameterName)
		where TEnum : struct, Enum
	{
		if (!Enum.IsDefined(value))
		{
			throw new ArgumentException(
				$"The value '{value}' is not defined for {typeof(TEnum).Name}.",
				parameterName);
		}
	}

	private static void ValidateNonNegative(long value, string parameterName)
	{
		if (value < 0)
		{
			throw new ArgumentException("The value must not be negative.", parameterName);
		}
	}

	private static void ValidateOptionalNonNegative(long? value, string parameterName)
	{
		if (value < 0)
		{
			throw new ArgumentException("The value must not be negative.", parameterName);
		}
	}

	private static void ValidateText(
		string? value,
		int maxLength,
		string parameterName,
		bool required)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			if (required)
			{
				throw new ArgumentException("The value is required.", parameterName);
			}

			return;
		}

		if (value.Length > maxLength)
		{
			throw new ArgumentException(
				$"The value must not exceed {maxLength} characters.",
				parameterName);
		}

		if (value.Any(char.IsControl))
		{
			throw new ArgumentException(
				"The value must not contain control characters.",
				parameterName);
		}
	}
}
