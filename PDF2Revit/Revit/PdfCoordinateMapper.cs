using Autodesk.Revit.DB;
using PDF2Revit.Core;
using PDF2Revit.Models;

namespace PDF2Revit.Revit;

public sealed class PdfCoordinateMapper
{
    private readonly XYZ insertion;
    private readonly P2 anchor;
    private readonly double scale, cosine, sine, elevation;
    public double ScaleFeetPerPoint => scale;
    public double RotationRadians { get; }

    public PdfCoordinateMapper(PdfToRevitSettings settings, XYZ insertion, P2 anchor, double rotationDegrees, double elevation)
    {
        double mm = settings.MmPerPdfPoint;
        if (mm <= 0 || double.IsNaN(mm) || double.IsInfinity(mm))
            throw new System.InvalidOperationException("Invalid PDF scale.");

        scale = UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
        this.insertion = insertion;
        this.anchor = anchor;
        this.elevation = elevation;
        RotationRadians = rotationDegrees * System.Math.PI / 180.0;
        cosine = System.Math.Cos(RotationRadians);
        sine = System.Math.Sin(RotationRadians);
    }

    public XYZ ToRevit(P2 point)
    {
        double dx = (point.X - anchor.X) * scale;
        double dy = (point.Y - anchor.Y) * scale;
        return new XYZ(insertion.X + dx * cosine - dy * sine,
                       insertion.Y + dx * sine + dy * cosine, elevation);
    }
}
