using Autodesk.Revit.DB;
using PDF2Revit.UI.ViewModels;
using System.Windows;

namespace PDF2Revit.UI.Views;

public partial class GenerateWallsWindow : Window
{
    public GenerateWallsViewModel ViewModel { get; }

    public GenerateWallsWindow(Document document)
    {
        InitializeComponent();

        ViewModel = new GenerateWallsViewModel(document);
        DataContext = ViewModel;

        ViewModel.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested(bool result)
    {
        DialogResult = result;
    }
}
