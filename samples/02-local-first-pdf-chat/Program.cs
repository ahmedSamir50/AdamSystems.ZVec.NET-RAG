using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using ZVec.Rag.Abstractions;
using ZVec.Rag.Constants;
using ZVec.Rag.Streaming;
using ZVec.Rag.Testing;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddZVecRag(opts =>
{
    opts.StoragePath = Path.Combine(Path.GetTempPath(), "ZVecSample02", Guid.NewGuid().ToString("N"));
    opts.Embedder = new DeterministicEmbedder();
    // Test double for CI and first run. Concatenates tokens; does not call a model. Replace with your IChatClient (Story 4.1).
    opts.Chat = new FakeChatClient("ZVec", " is local-first.");
})
.AddTokenChunker()
.AddZVecRagPdf();

var app = builder.Build();
var pipeline = app.Services.GetRequiredService<IRagPipeline>();

string fixturesDir = Path.Combine(AppContext.BaseDirectory, "fixtures");
await pipeline.IngestTextAsync(
    await File.ReadAllTextAsync(Path.Combine(fixturesDir, "en-faq.txt")),
    "en-faq.txt");
await pipeline.IngestTextAsync(
    await File.ReadAllTextAsync(Path.Combine(fixturesDir, "ar-faq.txt")),
    "ar-faq.txt");

byte[] pdfBytes = CreateOnePagePdf("HELLO-ZVEC-PDF");
using (var pdfStream = new MemoryStream(pdfBytes))
{
    await pipeline.IngestDocumentAsync(
        pdfStream,
        "hello.pdf",
        ZVecRagConstants.PdfContentType);
}

app.MapRagSseEndpoint("/chat");
app.Run();

static byte[] CreateOnePagePdf(string text)
{
    var builder = new PdfDocumentBuilder();
    PdfDocumentBuilder.AddedFont font = builder.AddStandard14Font(Standard14Font.Helvetica);
    PdfPageBuilder page = builder.AddPage(PageSize.A4);
    page.AddText(text, 12, new PdfPoint(25, page.PageSize.Top - 50), font);
    return builder.Build();
}
