using Autodesk.Revit.DB;
using Microsoft.Win32;
using PDF2Revit.Models;
using PDF2Revit.UI.Commands;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace PDF2Revit.UI.ViewModels;

public class GenerateWallsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<bool>? CloseRequested;

    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string pdfFilePath = "";
    public string PdfFilePath
    {
        get => pdfFilePath;
        set
        {
            pdfFilePath = value;
            Notify();
        }
    }

    private RevitItem? selectedBaseLevel;
    public RevitItem? SelectedBaseLevel
    {
        get => selectedBaseLevel;
        set
        {
            selectedBaseLevel = value;
            Notify();
            UpdateAvailableTopLevels();
        }
    }

    private RevitItem? selectedTopLevel;
    public RevitItem? SelectedTopLevel
    {
        get => selectedTopLevel;
        set
        {
            selectedTopLevel = value;
            Notify();
        }
    }

    private RevitItem? selectedWallType;
    public RevitItem? SelectedWallType
    {
        get => selectedWallType;
        set
        {
            selectedWallType = value;
            Notify();
        }
    }

    private ScaleOption? selectedScale;
    public ScaleOption? SelectedScale
    {
        get => selectedScale;
        set
        {
            selectedScale = value;
            Notify();
        }
    }

    private string wallHeightText = "3000";
    public string WallHeightText
    {
        get => wallHeightText;
        set
        {
            wallHeightText = value;
            Notify();
        }
    }

    private bool isUnconnected = true;
    public bool IsUnconnected
    {
        get => isUnconnected;
        set
        {
            if (isUnconnected == value)
                return;

            isUnconnected = value;
            Notify();
            Notify(nameof(IsUpToLevel));
        }
    }

    public bool IsUpToLevel
    {
        get => !IsUnconnected;
        set
        {
            if (value)
                IsUnconnected = false;
        }
    }

    private bool isPickPoint = true;
    public bool IsPickPoint
    {
        get => isPickPoint;
        set
        {
            if (isPickPoint == value)
                return;

            isPickPoint = value;
            Notify();
            Notify(nameof(IsInternalOrigin));
        }
    }

    public bool IsInternalOrigin
    {
        get => !IsPickPoint;
        set
        {
            if (value)
                IsPickPoint = false;
        }
    }

    public ObservableCollection<RevitItem> Levels { get; } = [];
    public ObservableCollection<RevitItem> AvailableTopLevels { get; } = [];
    public ObservableCollection<RevitItem> WallTypes { get; } = [];
    public ObservableCollection<ScaleOption> ScaleOptions { get; } = [];

    public ICommand BrowseCommand { get; }
    public ICommand GenerateCommand { get; }
    public ICommand CancelCommand { get; }

    public PdfToRevitSettings? Result { get; private set; }

    private readonly Dictionary<long, double> levelElevations = [];

    public GenerateWallsViewModel(Document document)
    {
        BrowseCommand = new RelayCommand(_ => BrowsePdf());
        GenerateCommand = new RelayCommand(_ => Generate());
        CancelCommand = new RelayCommand(
            _ => CloseRequested?.Invoke(false));

        LoadLevels(document);
        LoadWallTypes(document);
        LoadScales();
    }

    private void LoadLevels(Document document)
    {
        var levels = new FilteredElementCollector(document)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(x => x.Elevation)
            .ToList();

        foreach (var level in levels)
        {
            Levels.Add(new RevitItem(level.Id, level.Name));
            levelElevations[level.Id.IntegerValue] = level.Elevation;
        }

        SelectedBaseLevel = Levels.FirstOrDefault();
    }

    private void UpdateAvailableTopLevels()
    {
        AvailableTopLevels.Clear();

        if (SelectedBaseLevel == null)
        {
            SelectedTopLevel = null;
            return;
        }

        double baseElevation = levelElevations[SelectedBaseLevel.Id.IntegerValue];
        foreach (var level in Levels)
        {
            if (levelElevations[level.Id.IntegerValue] > baseElevation)
                AvailableTopLevels.Add(level);
        }

        SelectedTopLevel = AvailableTopLevels.FirstOrDefault();
    }

    private void LoadWallTypes(Document document)
    {
        var wallTypes = new FilteredElementCollector(document)
            .OfClass(typeof(WallType))
            .Cast<WallType>()
            .Where(x => x.Kind == WallKind.Basic)
            .OrderBy(x => x.Name)
            .ToList();

        foreach (var wallType in wallTypes)
        {
            WallTypes.Add(new RevitItem(wallType.Id, wallType.Name));
        }

        SelectedWallType = WallTypes.FirstOrDefault();
    }

    private void LoadScales()
    {
        foreach (double scale in new double[]
        {
            20, 25, 50, 75, 100, 125, 150, 200, 250, 500
        })
        {
            ScaleOptions.Add(new ScaleOption(scale));
        }

        SelectedScale = ScaleOptions.FirstOrDefault(x => x.Denominator == 100);
    }

    private void BrowsePdf()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select PDF Floor Plan",
            Filter = "PDF Files (*.pdf)|*.pdf",
            Multiselect = false,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
            PdfFilePath = dialog.FileName;
    }

    private void Generate()
    {
        if (string.IsNullOrWhiteSpace(PdfFilePath) ||
            !File.Exists(PdfFilePath))
        {
            ShowWarning("Please select an existing PDF file.");
            return;
        }

        if (SelectedBaseLevel == null)
        {
            ShowWarning("Please select a base level.");
            return;
        }

        if (SelectedWallType == null)
        {
            ShowWarning("Please select a wall type.");
            return;
        }

        if (SelectedScale == null)
        {
            ShowWarning("Please select a drawing scale.");
            return;
        }

        double height = 0;

        if (IsUnconnected)
        {
            if (!double.TryParse(
                    WallHeightText,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.CurrentCulture,
                    out height) || height <= 0)
            {
                ShowWarning("Wall height must be a positive number.");
                return;
            }
        }
        else if (SelectedTopLevel == null)
        {
            ShowWarning(
                "Please select a top level above the base level.");
            return;
        }

        Result = new PdfToRevitSettings
        {
            PdfFilePath = PdfFilePath,

            BaseLevelId = SelectedBaseLevel.Id,
            TopLevelId = IsUnconnected ? null : SelectedTopLevel!.Id,
            WallTypeId = SelectedWallType.Id,
            HeightMode = IsUnconnected ? WallHeightMode.Unconnected : WallHeightMode.UpToLevel,
            WallHeightMm = height,
            ScaleMode = PdfScaleMode.KnownScale,
            ScaleDenominator = SelectedScale.Denominator,
            PlacementMode = IsPickPoint ? PlacementMode.PickPoint : PlacementMode.InternalOrigin
        };

        CloseRequested?.Invoke(true);
    }

    private static void ShowWarning(string message)
    {
        MessageBox.Show(
            message,
            "PDF to Revit",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}

public class RevitItem(ElementId id, string name)
{
    public ElementId Id { get; } = id;
    public string Name { get; } = name;
}

public class ScaleOption(double denominator)
{
    public string Name { get; } = $"1:{denominator}";
    public double Denominator { get; } = denominator;
}
