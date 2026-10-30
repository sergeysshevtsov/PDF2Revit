using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace PDF2Revit;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
internal class Application : IExternalApplication
{
    public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;
    public Result OnStartup(UIControlledApplication application)
    {
        if (!AddMenu(application, string.Empty))
            return Result.Failed;

        return Result.Succeeded;
    }

    private static bool AddMenu(UIControlledApplication application, string tabname)
    {
        try
        {
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            var resourceString = "pack://application:,,,/PDF2Revit;component/Resources/Images/Ribbon/";
            var rb = GetRibbonPanel(application, "PDF2Revit", tabname);
            rb.AddItem(
                new PushButtonData("cmdGenerateWalls", "Generate\nWalls", assemblyPath, "PDF2Revit.Ribbon.Commands.CmdGenerateWalls")
                {
                    ToolTip = "Generate walls from PDF",
                    LargeImage = new BitmapImage(new Uri(string.Concat(resourceString, "32_GenerateWalls.png"))),
                    Image = new BitmapImage(new Uri(string.Concat(resourceString, "16_GenerateWalls.png")))
                });
        }
        catch
        {
            return false;
        }

        return true;
    }

    private static RibbonPanel GetRibbonPanel(UIControlledApplication application, string ribbonName, string? tabName = null)
    {
        IList<RibbonPanel> ribbonPanels;
        if (!string.IsNullOrEmpty(tabName))
        {
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch { }
            ribbonPanels = application.GetRibbonPanels(tabName);
        }
        else
            ribbonPanels = application.GetRibbonPanels();

        RibbonPanel? ribbonPanel = null;
        foreach (RibbonPanel rp in ribbonPanels)
        {
            if (rp.Name.Equals(ribbonName))
            {
                ribbonPanel = rp;
                break;
            }
        }

        ribbonPanel ??= (string.IsNullOrEmpty(tabName)) ?
                application.CreateRibbonPanel(ribbonName) :
                application.CreateRibbonPanel(tabName, ribbonName);

        return ribbonPanel;
    }
}
