using System.Text.Json;
using ZVec.Rag.Retrieval;
using ZVec.Rag.Testing;
using ZVec.Rag.Testing.Evaluation;

namespace ZVec.Rag.Tests.Evaluation;

public sealed class DeterministicEvaluatorTests
{
    private readonly DeterministicEvaluator _evaluator = new();

    [Fact]
    public void EvaluateRetrieval_RecallAtK_CountsGoldHitsInTopK()
    {
        var metrics = _evaluator.EvaluateRetrieval(
            goldChunkIds: ["a", "b"],
            retrievedChunkIds: ["x", "a", "y", "b"],
            k: 3);

        Assert.Equal(3, metrics.K);
        Assert.Equal(0.5d, metrics.RecallAtK, precision: 6);
        Assert.Equal(0.5d, metrics.Mrr, precision: 6);
    }

    [Fact]
    public void EvaluateRetrieval_EmptyGold_ReturnsZeroMetrics()
    {
        var metrics = _evaluator.EvaluateRetrieval([], ["a"], k: 5);

        Assert.Equal(0d, metrics.RecallAtK);
        Assert.Equal(0d, metrics.Mrr);
        Assert.Equal(0d, metrics.NdcgAtK);
    }

    [Fact]
    public void EvaluateRetrieval_NdcgAtK_UsesRelevanceGrades()
    {
        var metrics = _evaluator.EvaluateRetrieval(
            goldChunkIds: ["a"],
            retrievedChunkIds: ["a"],
            k: 1,
            relevanceGrades: new Dictionary<string, int> { ["a"] = 2 });

        Assert.Equal(1d, metrics.NdcgAtK, precision: 6);
    }

    [Fact]
    public void RecallAtKLift_WhenBaselineZero_RelativeLiftIsNull()
    {
        var baseline = new RagRetrievalMetrics(0d, 0d, 0d, 5);
        var treatment = new RagRetrievalMetrics(0.5d, 0.5d, 0.5d, 5);

        RecallAtKLiftResult lift = _evaluator.RecallAtKLift(baseline, treatment, k: 5);

        Assert.Equal(0d, lift.Baseline);
        Assert.Equal(0.5d, lift.Treatment);
        Assert.Null(lift.RelativeLift);
        Assert.Equal(0.5d, lift.Delta, precision: 6);
    }

    [Fact]
    public void RecallAtKLift_WhenBaselineNonZero_ComputesRelativeLift()
    {
        var baseline = new RagRetrievalMetrics(0.5d, 0.5d, 0.5d, 5);
        var treatment = new RagRetrievalMetrics(0.75d, 0.75d, 0.75d, 5);

        RecallAtKLiftResult lift = _evaluator.RecallAtKLift(baseline, treatment, k: 5);

        Assert.Equal(0.25d, lift.Delta, precision: 6);
        Assert.Equal(0.5d, lift.RelativeLift!.Value, precision: 6);
    }

    [Fact]
    public void EvaluateRetrieval_NullGold_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _evaluator.EvaluateRetrieval(null!, ["a"], k: 1));
    }

    [Fact]
    public void EvaluateRetrieval_InvalidK_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _evaluator.EvaluateRetrieval(["a"], ["a"], k: 0));
    }
}

public sealed class SemanticTestEmbedderTests
{
    [Fact]
    public void CreateVector_OverlappingTokens_ProducesHigherCosineThanUnrelated()
    {
        var embedder = new SemanticTestEmbedder(128);
        ReadOnlyMemory<float> left = embedder.CreateVector("Orion battery 5V regulated output");
        ReadOnlyMemory<float> right = embedder.CreateVector("Orion battery pack 5V output");
        ReadOnlyMemory<float> unrelated = embedder.CreateVector("Zephyr firmware sleep scheduling");

        float related = RagRetriever.ComputeCosineSimilarity(left, right);
        float distant = RagRetriever.ComputeCosineSimilarity(left, unrelated);

        Assert.True(related > distant);
        Assert.InRange(related, 0f, 1f);
    }
}

public sealed class DeterministicGenerationEvaluatorTests
{
    [Fact]
    public async Task EvaluateGenerationAsync_ReturnsConfiguredScores()
    {
        var evaluator = new DeterministicGenerationEvaluator(0.8d, 0.6d);
        RagGenerationMetrics metrics = await evaluator.EvaluateGenerationAsync(
            "query",
            "answer",
            ["context"],
            CancellationToken.None);

        Assert.Equal(0.8d, metrics.Faithfulness);
        Assert.Equal(0.6d, metrics.ContextPrecision);
    }
}

public sealed class LlmJudgeGenerationEvaluatorTests
{
    [Fact]
    public async Task EvaluateGenerationAsync_ParsesFakeJudgeJson()
    {
        var chat = new FakeChatClient("{\"faithfulness\":0.9,\"contextPrecision\":0.7}");
        var evaluator = new LlmJudgeGenerationEvaluator(chat);

        RagGenerationMetrics metrics = await evaluator.EvaluateGenerationAsync(
            "What is Orion voltage?",
            "5V",
            ["Orion battery 5V"],
            CancellationToken.None);

        Assert.Equal(0.9d, metrics.Faithfulness, precision: 6);
        Assert.Equal(0.7d, metrics.ContextPrecision, precision: 6);
    }
}
