using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ZVec.Rag.Abstractions;

namespace ZVec.Rag.Streaming;

/// <summary>
/// ASP.NET Core SSE helpers for streaming <see cref="Models.RagChunk"/> responses.
/// </summary>
public static class RagSseEndpointExtensions
{
    /// <summary>
    /// Maps an SSE chat endpoint that streams RAG answers for a query string <c>question</c>.
    /// </summary>
    [RequiresUnreferencedCode("ASP.NET Core endpoint mapping is not trim-safe.")]
    public static IEndpointConventionBuilder MapRagSseEndpoint(this IEndpointRouteBuilder endpoints, string pattern)
    {
        return endpoints.MapGet(pattern, HandleSseRequest);
    }

    [RequiresUnreferencedCode("ASP.NET Core endpoint mapping is not trim-safe.")]
    private static async Task HandleSseRequest(
        HttpContext httpContext,
        string? question,
        IRagGenerator generator,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsync("question is required", cancellationToken).ConfigureAwait(false);
            return;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            httpContext.RequestAborted,
            cancellationToken);

        httpContext.Response.Headers.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";

        await foreach (var chunk in generator.AskAsync(
            question,
            history: null,
            streamCitations: true,
            cancellationToken: linkedCts.Token).ConfigureAwait(false))
        {
            var payloadModel = new RagSsePayload(chunk.Text, chunk.IsFinal, chunk.Citations);
            string payload = JsonSerializer.Serialize(payloadModel, RagSseJsonContext.Default.RagSsePayload);

            await httpContext.Response.WriteAsync($"data: {payload}\n\n", linkedCts.Token).ConfigureAwait(false);
            await httpContext.Response.BodyWriter.FlushAsync(linkedCts.Token).ConfigureAwait(false);
        }
    }
}
