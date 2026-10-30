namespace PDF2Revit.Core;

public sealed class WallDetector
{
    private class AxisLine(bool horizontal, double @fixed, double start, double end)
    {
        public bool Horizontal { get; set; } = horizontal;
        public double Fixed { get; set; } = @fixed;
        public double Start { get; set; } = start;
        public double End { get; set; } = end;
    }

    public static IReadOnlyList<WallCandidate> Find(PdfGeometry drawing, DetectionOptions? options = null)
    {
        var o = options ?? new DetectionOptions();
        var lines = new List<AxisLine>();
        foreach (var s in drawing.Segments)
        {
            if (s.Length < o.MinimumLengthPt) 
                continue;
            var dx = Math.Abs(s.A.X - s.B.X);
            var dy = Math.Abs(s.A.Y - s.B.Y);
            if (dy <= o.AxisTolerancePt)
                lines.Add(new AxisLine(true, (s.A.Y + s.B.Y) / 2, Math.Min(s.A.X, s.B.X), Math.Max(s.A.X, s.B.X)));
            else 
                if (dx <= o.AxisTolerancePt)
                    lines.Add(new AxisLine(false, (s.A.X + s.B.X) / 2, Math.Min(s.A.Y, s.B.Y), Math.Max(s.A.Y, s.B.Y)));
        }

        lines = lines.GroupBy(x => (x.Horizontal, Fixed: Math.Round(x.Fixed, 1), Start: Math.Round(x.Start, 1), End: Math.Round(x.End, 1)))
                     .Select(g => g.First()).ToList();
        var result = new List<WallCandidate>();
        foreach (var horizontal in new[] { true, false })
        {
            var group = lines.Where(x => x.Horizontal == horizontal).OrderBy(x => x.Fixed).ToArray();
            for (int i = 0; i < group.Length; i++)
            {
                var a = group[i];
                for (int j = i + 1; j < group.Length; j++)
                {
                    var b = group[j];
                    double distance = b.Fixed - a.Fixed;
                    if (distance > o.MaximumWidthPt) 
                        break;
                    if (distance < o.MinimumWidthPt) 
                        continue;

                    double lo = Math.Max(a.Start, b.Start), hi = Math.Min(a.End, b.End);
                    if (hi - lo < o.MinimumLengthPt) 
                        continue;

                    var ratio = (hi - lo) / Math.Max(a.End - a.Start, b.End - b.Start);
                    if (ratio < o.MinimumOverlapRatio) 
                        continue;

                    double center = (a.Fixed + b.Fixed) / 2;
                    result.Add(new WallCandidate
                    {
                        A = horizontal ? new P2(lo, center) : new P2(center, lo),
                        B = horizontal ? new P2(hi, center) : new P2(center, hi),
                        WidthPt = distance,
                        Score = ratio,
                        Approved = false
                    });
                    if (result.Count >= o.MaximumCandidates) 
                        return Rank(result);
                }
            }
        }
        return Rank(result);
    }

    private static IReadOnlyList<WallCandidate> Rank(List<WallCandidate> input)
    {
        var result = input.GroupBy(x => (
            Math.Round(x.A.X, 1), Math.Round(x.A.Y, 1),
            Math.Round(x.B.X, 1), Math.Round(x.B.Y, 1)))
            .Select(x => x.OrderByDescending(y => y.Score).First())
            .OrderByDescending(x => x.LengthPt * x.Score).ToList();
        for (int i = 0; i < result.Count; i++) result[i].Id = i + 1;
        return result;
    }
}
