using Autodesk.Revit.DB;
using PDF2Revit.Core;
using PDF2Revit.Models;

namespace PDF2Revit.Revit;

public static class PdfBackgroundPlacer
{
    public static ImageInstance Place(Document document, View view, PdfGeometry geometry, PdfToRevitSettings settings, PdfCoordinateMapper mapper)
    {
        if (view is not ViewPlan || view.IsTemplate)
            throw new InvalidOperationException("Open a floor plan view to place the source PDF.");

        if (geometry.WidthPt <= 0 || geometry.HeightPt <= 0)
            throw new InvalidOperationException("Invalid PDF page dimensions.");

        using var options = new ImageTypeOptions(settings.PdfFilePath, false, ImageTypeSource.Import)
        {
            PageNumber = settings.PageNumber,
            Resolution = 150
        };

        ImageType type = ImageType.Create(document, options);
        P2 pageCenter = new(geometry.WidthPt / 2, geometry.HeightPt / 2);
        XYZ center = mapper.ToRevit(pageCenter);
        using var placement = new ImagePlacementOptions(center, BoxPlacement.Center);
        ImageInstance image = ImageInstance.Create(document, view, type.Id, placement);
        image.Width = geometry.WidthPt * mapper.ScaleFeetPerPoint;
        image.DrawLayer = DrawLayer.Background;

        if (Math.Abs(mapper.RotationRadians) > 1e-10)
        {
            Line axis = Line.CreateBound(center, center + XYZ.BasisZ);
            ElementTransformUtils.RotateElement(document, image.Id, axis, mapper.RotationRadians);
        }
        image.Pinned = true;
        return image;
    }
}
