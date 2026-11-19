using Astra.Core.Models;

namespace Astra.Core.Inference;

/// <summary>
/// Shared validation rules for <see cref="ILocalModelRuntime"/> implementations.
/// </summary>
public static class LocalRuntimeValidation
{
	/// <summary>Largest accepted generation length in tokens.</summary>
	public const int MaxGenerationTokens = 32_768;

	/// <summary>Largest accepted prompt length in characters.</summary>
	public const int MaxPromptLength = 1_000_000;

	/// <summary>Largest accepted number of inputs per embedding request.</summary>
	public const int MaxEmbeddingInputs = 256;

	/// <summary>Largest accepted embedding input length in characters.</summary>
	public const int MaxEmbeddingInputLength = 100_000;

	/// <summary>Largest accepted GPU layer count.</summary>
	public const int MaxGpuLayers = 1000;

	/// <summary>Largest accepted sampling temperature.</summary>
	public const float MaxTemperature = 2.0f;

	/// <summary>Validates a model identifier.</summary>
	/// <exception cref="ArgumentException">The identifier is empty or too long.</exception>
	public static void ValidateModelId(string modelId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

		if (modelId.Length > ModelDescriptorValidator.MaxIdLength)
		{
			throw new ArgumentException(
				$"Model identifiers must not exceed {ModelDescriptorValidator.MaxIdLength} characters.",
				nameof(modelId));
		}
	}

	/// <summary>Validates load parameters.</summary>
	/// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The model path is invalid.</exception>
	/// <exception cref="ArgumentOutOfRangeException">An override is outside its allowed range.</exception>
	public static void ValidateLoadOptions(ModelLoadOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);
		ModelDescriptorValidator.ValidateModelPath(options.ModelPath);

		if (options.ContextWindow is { } contextWindow &&
			(contextWindow <= 0 || contextWindow > ModelDescriptorValidator.MaxContextWindow))
		{
			throw new ArgumentOutOfRangeException(
				nameof(options),
				contextWindow,
				$"The context window must be between 1 and {ModelDescriptorValidator.MaxContextWindow} tokens.");
		}

		if (options.GpuLayerCount is { } gpuLayers && (gpuLayers < 0 || gpuLayers > MaxGpuLayers))
		{
			throw new ArgumentOutOfRangeException(
				nameof(options),
				gpuLayers,
				$"The GPU layer count must be between 0 and {MaxGpuLayers}.");
		}
	}

	/// <summary>Validates a generation request.</summary>
	/// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">A numeric parameter is outside its range.</exception>
	/// <exception cref="ArgumentException">A text parameter is empty or too long.</exception>
	public static void ValidateGenerationRequest(GenerationRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);

		ValidatePrompt(request.Prompt, nameof(request.Prompt));

		if (request.SystemPrompt is { Length: > MaxPromptLength })
		{
			throw new ArgumentException(
				$"System prompts must not exceed {MaxPromptLength} characters.",
				nameof(request));
		}

		if (request.MaxTokens <= 0 || request.MaxTokens > MaxGenerationTokens)
		{
			throw new ArgumentOutOfRangeException(
				nameof(request),
				request.MaxTokens,
				$"MaxTokens must be between 1 and {MaxGenerationTokens}.");
		}

		if (!float.IsFinite(request.Temperature) ||
			request.Temperature < 0 ||
			request.Temperature > MaxTemperature)
		{
			throw new ArgumentOutOfRangeException(
				nameof(request),
				request.Temperature,
				$"Temperature must be between 0 and {MaxTemperature}.");
		}
	}

	/// <summary>Validates an embedding request.</summary>
	/// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The input list or one of its entries is invalid.</exception>
	public static void ValidateEmbeddingRequest(EmbeddingRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(request.Inputs);

		if (request.Inputs.Count == 0 || request.Inputs.Count > MaxEmbeddingInputs)
		{
			throw new ArgumentException(
				$"Embedding requests must contain between 1 and {MaxEmbeddingInputs} inputs.",
				nameof(request));
		}

		foreach (var input in request.Inputs)
		{
			if (string.IsNullOrEmpty(input))
			{
				throw new ArgumentException(
					"Embedding inputs must not be empty.",
					nameof(request));
			}

			if (input.Length > MaxEmbeddingInputLength)
			{
				throw new ArgumentException(
					$"Embedding inputs must not exceed {MaxEmbeddingInputLength} characters.",
					nameof(request));
			}
		}
	}

	private static void ValidatePrompt(string prompt, string parameterName)
	{
		if (string.IsNullOrWhiteSpace(prompt))
		{
			throw new ArgumentException("The prompt is required.", parameterName);
		}

		if (prompt.Length > MaxPromptLength)
		{
			throw new ArgumentException(
				$"Prompts must not exceed {MaxPromptLength} characters.",
				parameterName);
		}
	}
}
