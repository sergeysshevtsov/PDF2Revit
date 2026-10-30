using Autodesk.Revit.DB;
using PDF2Revit.Core;
using PDF2Revit.Models;

namespace PDF2Revit.Revit;

public static class RevitWallCreator
{
    public static int Create(
        Document document,
        IReadOnlyList<WallCandidate> walls,
        PdfToRevitSettings settings,
        XYZ insertionPoint,
        P2 pdfAnchor,
        double rotationDegrees,
        bool usePdfBackground = false,
        PdfGeometry? pdfGeometry = null,
        View? targetView = null)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        if (walls == null)
            throw new ArgumentNullException(nameof(walls));

        if (walls.Count == 0)
            return 0;

        Level baseLevel = document.GetElement(settings.BaseLevelId) as Level ?? throw new InvalidOperationException("Base level not found.");
        WallType wallType = document.GetElement(settings.WallTypeId) as WallType ?? throw new InvalidOperationException("Wall type not found.");
        double height = UnitUtils.ConvertToInternalUnits(settings.WallHeightMm, UnitTypeId.Millimeters);

        Level? topLevel = null;
        if (settings.HeightMode == WallHeightMode.UpToLevel)
        {
            topLevel = document.GetElement(settings.TopLevelId!) as Level ??
                throw new InvalidOperationException("Top level not found.");

            height = topLevel.Elevation - baseLevel.Elevation;

            if (height <= 0)
                throw new InvalidOperationException("Top level must be above base level.");
        }

        if (height <= 0)
            throw new InvalidOperationException(
                "Invalid wall height.");

        var mapper = new PdfCoordinateMapper(settings, insertionPoint, pdfAnchor,
                                             rotationDegrees, baseLevel.Elevation);
        int created = 0;
        var normalizedWalls = WallCandidateNormalizer.Normalize(walls.Where(w => w.Approved));
        var uniqueAxes = new HashSet<string>(StringComparer.Ordinal);

        using Transaction transaction = new(document, "PDF to Revit - Generate Walls");
        transaction.Start();

        try
        {
            foreach (WallCandidate candidate in normalizedWalls)
            {
                if (!candidate.Approved)
                    continue;

                XYZ start = mapper.ToRevit(candidate.A);
                XYZ end = mapper.ToRevit(candidate.B);

                if (start.DistanceTo(end) < 0.11)
                    continue;

                string startKey = $"{Math.Round(start.X, 5)}:{Math.Round(start.Y, 5)}";
                string endKey = $"{Math.Round(end.X, 5)}:{Math.Round(end.Y, 5)}";
                string axisKey = string.CompareOrdinal(startKey, endKey) <= 0
                    ? startKey + "|" + endKey : endKey + "|" + startKey;

                if (!uniqueAxes.Add(axisKey))
                    continue;

                Line axis = Line.CreateBound(start, end);

                Wall wall = Wall.Create(
                    document,
                    axis,
                    wallType.Id,
                    baseLevel.Id,
                    height,
                    0,
                    false,
                    false);

                if (topLevel != null)
                {
                    Parameter topConstraint = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE);

                    if (topConstraint == null || topConstraint.IsReadOnly)
                        throw new InvalidOperationException("Cannot set wall top constraint.");

                    topConstraint.Set(topLevel.Id);
                }

                created++;
            }

            if (usePdfBackground)
            {
                if (pdfGeometry == null || targetView == null)
                    throw new InvalidOperationException("PDF geometry and a plan view are required.");
                PdfBackgroundPlacer.Place(document, targetView, pdfGeometry, settings, mapper);
            }

            transaction.Commit();
            return created;
        }
        catch
        {
            if (transaction.GetStatus() == TransactionStatus.Started)
                transaction.RollBack();

            throw;
        }
    }
}

