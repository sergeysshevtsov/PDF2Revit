using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using PDF2Revit.Core;

namespace PDF2Revit.PDF;

public class ITextVectorExtractor
{
    public static PdfGeometry Extract(string path, int pageNumber = 1)
    {
        using var document = new PdfDocument(new PdfReader(path));
        if (pageNumber < 1 || pageNumber > document.GetNumberOfPages())
            throw new ArgumentOutOfRangeException(nameof(pageNumber));

        var page = document.GetPage(pageNumber);
        var box = page.GetPageSize();
        int rotation = ((page.GetRotation() % 360) + 360) % 360;
        var output = new PdfGeometry
        {
            WidthPt = (rotation == 90 || rotation == 270) ? box.GetHeight() : box.GetWidth(),
            HeightPt = (rotation == 90 || rotation == 270) ? box.GetWidth() : box.GetHeight()
        };
        var listener = new PathListener(output, box.GetLeft(), box.GetBottom(), box.GetWidth(), box.GetHeight(), rotation);
        new PdfCanvasProcessor(listener).ProcessPageContent(page);
        return output;
    }

    private sealed class PathListener(PdfGeometry output, double left, double bottom, double width, double height, int rotation) : IEventListener
    {
        private readonly PdfGeometry output = output;
        private readonly double left = left, bottom = bottom, width = width, height = height;
        private readonly int rotation = rotation;

        public ICollection<EventType> GetSupportedEvents() => [EventType.RENDER_PATH];
        public void EventOccurred(IEventData data, EventType eventType)
        {
            if (eventType != EventType.RENDER_PATH || data is not PathRenderInfo info) 
                return;

            if (info.GetOperation() == PathRenderInfo.NO_OP) 
                return;

            bool filled = (info.GetOperation() & PathRenderInfo.FILL) != 0;
            var matrix = info.GetCtm();
            P2 Convert(Point p)
            {
                double x = p.GetX(), y = p.GetY();
                double px = matrix.Get(Matrix.I11) * x + matrix.Get(Matrix.I21) * y + matrix.Get(Matrix.I31) - left;
                double py = matrix.Get(Matrix.I12) * x + matrix.Get(Matrix.I22) * y + matrix.Get(Matrix.I32) - bottom;
                return rotation switch
                {
                    90 => new P2(py, width - px),
                    180 => new P2(width - px, height - py),
                    270 => new P2(height - py, px),
                    _ => new P2(px, py)
                };
            }

            foreach (var subpath in info.GetPath().GetSubpaths())
            {
                var edges = new List<(P2 a, P2 b)>();
                foreach (var shape in subpath.GetSegments())
                {
                    var pts = shape.GetBasePoints();
                    if (pts.Count == 2) edges.Add((Convert(pts[0]), Convert(pts[1])));
                }
                foreach (var (a, b) in edges)
                    if ((a - b).Length > 1e-5)
                        output.Segments.Add(new PdfSegment(a, b, filled, info.GetLineWidth()));
            }
        }
    }
}
