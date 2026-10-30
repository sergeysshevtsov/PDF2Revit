using System;
using System.Collections.Generic;
using System.Linq;

namespace PDF2Revit.Core;

public static class WallCandidateNormalizer
{
    public sealed class Options
    {
        public double AxisTolerancePt { get; set; } = 0.25;
        public double WidthTolerancePt { get; set; } = 0.75;
        public double AngleToleranceDegrees { get; set; } = 0.5;
        public double MaximumGapPt { get; set; } = 0;
    }

    private sealed class Axis
    {
        public bool Horizontal;
        public double Fixed;
        public double Start;
        public double End;
        public double Width;
        public double Score;
        public bool Approved;
    }

    public static IReadOnlyList<WallCandidate> Normalize(
        IEnumerable<WallCandidate> source, Options? options = null)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        var o = options ?? new Options();
        if (o.AxisTolerancePt < 0 || o.WidthTolerancePt < 0 || o.MaximumGapPt < 0 || o.AngleToleranceDegrees < 0 || o.AngleToleranceDegrees >= 45)
            throw new ArgumentOutOfRangeException(nameof(options));

        var axes = new List<Axis>();
        var oblique = new List<WallCandidate>();
        double tangent = Math.Tan(o.AngleToleranceDegrees * Math.PI / 180);

        foreach (WallCandidate c in source)
        {
            if (c?.A == null || c.B == null || c.LengthPt <= 0)
                continue;

            double dx = Math.Abs(c.B.X - c.A.X);
            double dy = Math.Abs(c.B.Y - c.A.Y);
            bool horizontal = dy <= dx * tangent;
            bool vertical = dx <= dy * tangent;
            if (!horizontal && !vertical)
            {
                oblique.Add(Clone(c));
                continue;
            }

            bool h = horizontal;
            axes.Add(new Axis
            {
                Horizontal = h,
                Fixed = h ? (c.A.Y + c.B.Y) / 2 : (c.A.X + c.B.X) / 2,
                Start = h ? Math.Min(c.A.X, c.B.X) : Math.Min(c.A.Y, c.B.Y),
                End = h ? Math.Max(c.A.X, c.B.X) : Math.Max(c.A.Y, c.B.Y),
                Width = c.WidthPt,
                Score = c.Score,
                Approved = c.Approved
            });
        }

        var merged = new List<Axis>();
        foreach (var axis in axes.OrderBy(x => x.Horizontal).ThenBy(x => x.Fixed).ThenBy(x => x.Start))
        {
            Axis? match = null;
            for (int i = merged.Count - 1; i >= 0; i--)
            {
                var current = merged[i];
                if (current.Horizontal != axis.Horizontal)
                    continue;

                if (Math.Abs(current.Fixed - axis.Fixed) > o.AxisTolerancePt)
                    continue;

                if (Math.Abs(current.Width - axis.Width) > o.WidthTolerancePt)
                    continue;

                if (axis.Start > current.End + o.MaximumGapPt || current.Start > axis.End + o.MaximumGapPt) 
                    continue;

                match = current;
                break;
            }

            if (match == null)
            {
                merged.Add(axis);
                continue;
            }
            double oldLength = match.End - match.Start;
            double newLength = axis.End - axis.Start;
            double total = oldLength + newLength;
            if (total > 0)
                match.Fixed = (match.Fixed * oldLength + axis.Fixed * newLength) / total;
            match.Start = Math.Min(match.Start, axis.Start);
            match.End = Math.Max(match.End, axis.End);
            match.Score = Math.Max(match.Score, axis.Score);
            match.Approved |= axis.Approved;
            
            bool changed;
            do
            {
                changed = false;
                for (int i = merged.Count - 1; i >= 0; i--)
                {
                    Axis other = merged[i];
                    if (ReferenceEquals(other, match) || other.Horizontal != match.Horizontal ||
                        Math.Abs(other.Fixed - match.Fixed) > o.AxisTolerancePt ||
                        Math.Abs(other.Width - match.Width) > o.WidthTolerancePt ||
                        other.Start > match.End + o.MaximumGapPt ||
                        match.Start > other.End + o.MaximumGapPt) 
                        continue;

                    match.Start = Math.Min(match.Start, other.Start);
                    match.End = Math.Max(match.End, other.End);
                    match.Score = Math.Max(match.Score, other.Score);
                    match.Approved |= other.Approved;
                    merged.RemoveAt(i);
                    changed = true;
                    break;
                }
            } while (changed);
        }

        var result = merged.Select(x => new WallCandidate
        {
            A = x.Horizontal ? new P2(x.Start, x.Fixed) : new P2(x.Fixed, x.Start),
            B = x.Horizontal ? new P2(x.End, x.Fixed) : new P2(x.Fixed, x.End),
            WidthPt = x.Width,
            Score = x.Score,
            Approved = x.Approved
        }).Concat(oblique).OrderByDescending(x => x.LengthPt * x.Score).ToList();
        for (int i = 0; i < result.Count; i++) result[i].Id = i + 1;
        return result;
    }

    private static WallCandidate Clone(WallCandidate source) => new()
    {
        Id = source.Id,
        A = new P2(source.A.X, source.A.Y),
        B = new P2(source.B.X, source.B.Y),
        WidthPt = source.WidthPt,
        Score = source.Score,
        Approved = source.Approved
    };
}
