using Autodesk.Revit.DB;

namespace PDF2Revit.Models;

public enum WallHeightMode
{
    Unconnected,
    UpToLevel
}

public enum PdfScaleMode
{
    KnownScale,
    Calibration
}

public enum PlacementMode
{
    PickPoint,
    InternalOrigin
}

public class PdfToRevitSettings
{
    public string PdfFilePath { get; set; } = "";

    public ElementId BaseLevelId { get; set; }
    public ElementId? TopLevelId { get; set; }
    public ElementId WallTypeId { get; set; }

    public WallHeightMode HeightMode { get; set; }
        = WallHeightMode.Unconnected;

    public double WallHeightMm { get; set; } = 3000;

    public PdfScaleMode ScaleMode { get; set; }
        = PdfScaleMode.KnownScale;

    public double ScaleDenominator { get; set; } = 100;

    public double PdfDistancePoints { get; set; }
    public double RealDistanceMm { get; set; }

    public PlacementMode PlacementMode { get; set; } = PlacementMode.PickPoint;

    public int PageNumber { get; set; } = 1;

    public double MmPerPdfPoint => 
        ScaleMode == PdfScaleMode.KnownScale ? 25.4 / 72.0 * ScaleDenominator : RealDistanceMm / PdfDistancePoints;
}

