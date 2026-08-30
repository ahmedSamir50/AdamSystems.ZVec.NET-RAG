using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Ingestion;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dependency injection extensions for ZVec.Rag PDF ingestion.
/// </summary>
public static class ZVecRagPdfServiceCollectionExtensions
{
    /// <summary>
    /// Registers PDF document reading and replaces <see cref="IRagDocumentReader"/> with a composite text + PDF reader.
    /// </summary>
    [RequiresUnreferencedCode("PdfPig text extraction is not trim-safe for Native AOT.")]
    public static IServiceCollection AddZVecRagPdf(this IServiceCollection services)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.Replace(ServiceDescriptor.Singleton<IRagDocumentReader>(sp =>
            new ZVec.Rag.Pdf.CompositeRagDocumentReader(
                new PlainTextDocumentReader(),
                new ZVec.Rag.Pdf.PdfDocumentReader())));

        return services;
    }
}
