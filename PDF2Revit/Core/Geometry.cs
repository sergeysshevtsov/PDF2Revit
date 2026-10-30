namespace PDF2Revit.Core;

public class P2(double x, double y)
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
    public static P2 operator +(P2 a, P2 b) => new(a.X + b.X, a.Y + b.Y);
    public static P2 operator -(P2 a, P2 b) => new(a.X - b.X, a.Y - b.Y);
    public static P2 operator *(P2 a, double k) => new(a.X * k, a.Y * k);
    public double Length => Math.Sqrt(X * X + Y * Y);
}

public sealed class PdfSegment(P2 a, P2 b, bool isFilled, double strokeWidth)
{
    public P2 A { get; } = a ?? throw new ArgumentNullException(nameof(a));
    public P2 B { get; } = b ?? throw new ArgumentNullException(nameof(b));
    public bool IsFilled { get; } = isFilled;
    public double StrokeWidth { get; } = strokeWidth;
    public double Length => (B - A).Length;
}

public class PdfGeometry
{
    public double WidthPt { get; set; }
    public double HeightPt { get; set; }
    public List<PdfSegment> Segments { get; set; } = new();
}

public class WallCandidate
{
    public int Id { get; set; }
    public P2 A { get; set; }
    public P2 B { get; set; }
    public double WidthPt { get; set; }
    public double Score { get; set; }
    public bool Approved { get; set; }
    public double LengthPt => (B - A).Length;
}

public class DetectionOptions
{
    public double MinimumLengthPt { get; set; } = 40;
    public double MinimumWidthPt { get; set; } = 1.2;
    public double MaximumWidthPt { get; set; } = 22;
    public double AxisTolerancePt { get; set; } = 0.4;
    public double MinimumOverlapRatio { get; set; } = 0.72;
    public int MaximumCandidates { get; set; } = 5000;
}

