namespace ZVec.Rag.Testing.Evaluation;

/// <summary>Optional generation-quality metrics (LLM-as-judge off in CI by default).</summary>
/// <param name="Faithfulness">Answer grounded only in supplied contexts (0–1).</param>
/// <param name="ContextPrecision">Share of retrieved contexts that are relevant (0–1).</param>
public readonly record struct RagGenerationMetrics(double Faithfulness, double ContextPrecision);

/// <summary>Evaluates answer faithfulness and context precision.</summary>
public interface IRagGenerationEvaluator
{
    /// <summary>Scores generation quality for a single query/answer pair.</summary>
    Task<RagGenerationMetrics> EvaluateGenerationAsync(
        string query,
        string answer,
        IReadOnlyList<string> contexts,
        CancellationToken cancellationToken = default);
}

/// <summary>Deterministic generation evaluator for unit tests (no live LLM).</summary>
public sealed class DeterministicGenerationEvaluator : IRagGenerationEvaluator
{
    private readonly double _faithfulness;
    private readonly double _contextPrecision;

    /// <summary>Initializes with fixed scores.</summary>
    public DeterministicGenerationEvaluator(double faithfulness = 1d, double contextPrecision = 1d)
    {
        if (faithfulness is < 0d or > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(faithfulness));
        }

        if (contextPrecision is < 0d or > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(contextPrecision));
        }

        _faithfulness = faithfulness;
        _contextPrecision = contextPrecision;
    }

    /// <inheritdoc />
    public Task<RagGenerationMetrics> EvaluateGenerationAsync(
        string query,
        string answer,
        IReadOnlyList<string> contexts,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query is required.", nameof(query));
        }

        if (answer == null)
        {
            throw new ArgumentNullException(nameof(answer));
        }

        if (contexts == null)
        {
            throw new ArgumentNullException(nameof(contexts));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new RagGenerationMetrics(_faithfulness, _contextPrecision));
    }
}
