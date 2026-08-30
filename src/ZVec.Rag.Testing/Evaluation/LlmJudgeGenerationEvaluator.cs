using System.Text.Json;
using Microsoft.Extensions.AI;

namespace ZVec.Rag.Testing.Evaluation;

/// <summary>
/// Parses an LLM judge JSON payload via <see cref="IChatClient"/> (off in CI by default).
/// </summary>
public sealed class LlmJudgeGenerationEvaluator : IRagGenerationEvaluator
{
    private readonly IChatClient _chatClient;

    /// <summary>Initializes a new instance.</summary>
    public LlmJudgeGenerationEvaluator(IChatClient chatClient)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
    }

    /// <inheritdoc />
    public async Task<RagGenerationMetrics> EvaluateGenerationAsync(
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

        string contextBlock = string.Join("\n---\n", contexts);
        var messages = new List<ChatMessage>
        {
            new(
                ChatRole.System,
                "Return JSON only: {\"faithfulness\":0-1,\"contextPrecision\":0-1}. Faithfulness = answer uses only contexts. Context precision = share of contexts that help answer the query."),
            new(
                ChatRole.User,
                $"Query: {query}\nAnswer: {answer}\nContexts:\n{contextBlock}")
        };

        ChatResponse response = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        string payload = response.Messages.LastOrDefault()?.Text ?? response.Text ?? string.Empty;
        using JsonDocument doc = JsonDocument.Parse(payload);
        double faithfulness = doc.RootElement.GetProperty("faithfulness").GetDouble();
        double contextPrecision = doc.RootElement.GetProperty("contextPrecision").GetDouble();
        return new RagGenerationMetrics(faithfulness, contextPrecision);
    }
}
