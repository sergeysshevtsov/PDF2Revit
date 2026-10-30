using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using PDF2Revit.Core;
using PDF2Revit.Models;
using PDF2Revit.PDF;
using PDF2Revit.Revit;
using PDF2Revit.UI.Views;
using System.Windows.Interop;

namespace PDF2Revit.Ribbon.Commands;

[Transaction(TransactionMode.Manual)]
public class CmdGenerateWalls : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIDocument uiDocument = commandData.Application.ActiveUIDocument;

        if (uiDocument == null)
            return Result.Failed;

        Document document = uiDocument.Document;
        IntPtr owner = commandData.Application.MainWindowHandle;

        try
        {
            var settingsWindow = new GenerateWallsWindow(document);

            new WindowInteropHelper(settingsWindow)
            {
                Owner = owner
            };

            if (settingsWindow.ShowDialog() != true)
                return Result.Cancelled;

            PdfToRevitSettings settings = settingsWindow.ViewModel.Result!;

            var pdfGeometry = ITextVectorExtractor.Extract(settings.PdfFilePath, settings.PageNumber);

            if (pdfGeometry.Segments.Count == 0)
            {
                TaskDialog.Show("PDF to Revit", "No vector line segments were found on this page.");
                return Result.Cancelled;
            }

            var rawCandidates = WallDetector.Find(pdfGeometry);
            var candidates = WallCandidateNormalizer.Normalize(rawCandidates);

            var previewWindow = new WallPreviewWindow(pdfGeometry, candidates);
            new WindowInteropHelper(previewWindow)
            {
                Owner = owner
            };

            if (previewWindow.ShowDialog() != true)
                return Result.Cancelled;

            var approvedWalls = previewWindow.ApprovedWalls.ToList();
            P2 pdfAnchor = previewWindow.PdfAnchorPoint;
            double rotationDegrees = previewWindow.RotationDegrees;
            bool usePdfBackground = previewWindow.UsePdfBackground;
            if (usePdfBackground && uiDocument.ActiveView is not ViewPlan)
            {
                TaskDialog.Show("PDF to Revit", "Open a floor plan view to place the source PDF as background.");
                return Result.Cancelled;
            }

            if (approvedWalls.Count == 0)
            {
                TaskDialog.Show("PDF to Revit", "No walls were selected.");
                return Result.Cancelled;
            }

            XYZ insertionPoint = settings.PlacementMode == PlacementMode.PickPoint ? uiDocument.Selection.PickPoint("Select insertion point") : XYZ.Zero;

            int created = RevitWallCreator.Create(
                document,
                approvedWalls,
                settings,
                insertionPoint,
                pdfAnchor,
                rotationDegrees,
                usePdfBackground,
                pdfGeometry,
                uiDocument.ActiveView);

            TaskDialog.Show("PDF to Revit", $"Successfully created {created} walls.");
            return Result.Succeeded;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return Result.Cancelled;
        }
        catch (Exception ex)
        {
            message = ex.ToString();
            return Result.Failed;
        }
    }
}
