using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ZVec.Rag.Pdf.Tests;

internal static class PdfTestFixtures
{
    internal static byte[] CreateOnePagePdf(string text)
    {
        var builder = new PdfDocumentBuilder();
        PdfDocumentBuilder.AddedFont font = builder.AddStandard14Font(Standard14Font.Helvetica);
        PdfPageBuilder page = builder.AddPage(PageSize.A4);

        if (!string.IsNullOrEmpty(text))
        {
            page.AddText(text, 12, new PdfPoint(25, page.PageSize.Top - 50), font);
        }

        return builder.Build();
    }
}
