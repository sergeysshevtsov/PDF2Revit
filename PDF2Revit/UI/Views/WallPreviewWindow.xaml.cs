using PDF2Revit.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PDF2Revit.UI.Views;

public partial class WallPreviewWindow : Window
{
    private readonly PdfGeometry geometry;
    private readonly List<WallCandidate> candidates;
    private double factor = 1;
    private double minRotX;
    private double maxRotY;
    private bool viewportReady;
    private bool isPickingAnchor;
    private int lastClickedCandidateIndex = -1;
    private const double MinZoom = 0.2;
    private const double MaxZoom = 12.0;
    private double zoom = 1.0;
    private bool isPanning;
    private Point panStart;
    private double panStartHorizontal;
    private double panStartVertical;
    private bool isMarqueePending;
    private bool isMarqueeDragging;
    private Point marqueeStart;
    private Rectangle? marqueeRectangle;
    private const double MarqueeDragThreshold = 4;


    public P2 PdfAnchorPoint { get; private set; } = new(0, 0);
    public double RotationDegrees { get; private set; }
    public bool UsePdfBackground => UsePdfBackgroundCheckBox.IsChecked == true;
    public IReadOnlyList<WallCandidate> ApprovedWalls =>
        candidates.Where(x => x.Approved).ToList();

    public WallPreviewWindow(PdfGeometry geometry, IReadOnlyList<WallCandidate> candidates)
    {
        InitializeComponent();
        this.geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        this.candidates = candidates?.ToList() ?? throw new ArgumentNullException(nameof(candidates));
        WallGrid.ItemsSource = this.candidates;
        var points = geometry.Segments.SelectMany(s => new[] { s.A, s.B }).ToList();
        if (points.Count != 0)
            PdfAnchorPoint = new P2(points.Min(p => p.X), points.Max(p => p.Y));
        UpdateAnchorInfo();
        Render();
        UpdateZoomLabel();
    }

    private static P2 Rotate(P2 p, double degrees)
    {
        double angle = degrees * Math.PI / 180.0;
        double cosine = Math.Cos(angle);
        double sine = Math.Sin(angle);
        return new P2(p.X * cosine - p.Y * sine, p.X * sine + p.Y * cosine);
    }

    private void UpdateViewport()
    {
        var points = geometry.Segments.SelectMany(s => new[] { s.A, s.B })
            .Concat(candidates.SelectMany(w => new[] { w.A, w.B }))
            .Select(p => Rotate(p, RotationDegrees))
            .ToList();
        if (points.Count == 0)
        {
            factor = 1;
            minRotX = 0;
            maxRotY = 0;
            Drawing.Width = 720;
            Drawing.Height = 720;
            viewportReady = true;
            return;
        }
        double minX = points.Min(p => p.X);
        double maxX = points.Max(p => p.X);
        double minY = points.Min(p => p.Y);
        double maxY = points.Max(p => p.Y);
        double spanX = Math.Max(1, maxX - minX);
        double spanY = Math.Max(1, maxY - minY);
        factor = Math.Min(700.0 / spanX, 700.0 / spanY);
        minRotX = minX;
        maxRotY = maxY;
        Drawing.Width = spanX * factor + 20;
        Drawing.Height = spanY * factor + 20;
        viewportReady = true;
    }

    private Point At(P2 original)
    {
        P2 p = Rotate(original, RotationDegrees);
        return new Point(10 + (p.X - minRotX) * factor,
                         10 + (maxRotY - p.Y) * factor);
    }

    private P2 FromCanvas(Point canvas)
    {
        var rotated = new P2(minRotX + (canvas.X - 10) / factor,
                             maxRotY - (canvas.Y - 10) / factor);
        return Rotate(rotated, -RotationDegrees);
    }

    private void Render(int selectedId = -1)
    {
        if (!viewportReady) UpdateViewport();
        Drawing.Children.Clear();
        RenderPdfLinework();
        foreach (var w in candidates)
        {
            Brush color = w.Id == selectedId ? Brushes.OrangeRed :
                w.Approved ? Brushes.ForestGreen : Brushes.SteelBlue;
            var line = AddLine(w.A, w.B, color, w.Id == selectedId ? 3.4 : 1.6);
            line.Tag = w;
        }
        DrawAnchor();
        int approvedCount = candidates.Count(x => x.Approved);
        int finalCount = WallCandidateNormalizer.Normalize(candidates.Where(x => x.Approved)).Count;
        ApprovedCount.Text = $"{approvedCount:N0} selected | {finalCount:N0} final walls";
        Summary.Text = $"{geometry.Segments.Count:N0} PDF segments | " +
            $"{candidates.Count:N0} normalized candidates | {finalCount:N0} final walls | " +
            $"Rotation: {RotationDegrees:0}°. Blue = candidate, green = approved, orange = selected.";
    }

    private void RenderPdfLinework()
    {
        var geometryPath = new StreamGeometry();
        using (StreamGeometryContext context = geometryPath.Open())
        {
            foreach (PdfSegment segment in geometry.Segments)
            {
                if (segment.Length < 2) continue;
                context.BeginFigure(At(segment.A), false, false);
                context.LineTo(At(segment.B), true, false);
            }
        }
        geometryPath.Freeze();
        Drawing.Children.Add(new System.Windows.Shapes.Path
        {
            Data = geometryPath,
            Stroke = Brushes.LightGray,
            StrokeThickness = 0.5,
            IsHitTestVisible = false
        });
    }

    private Line AddLine(P2 a, P2 b, Brush stroke, double width)
    {
        Point start = At(a), end = At(b);
        var l = new Line
        {
            X1 = start.X, Y1 = start.Y, X2 = end.X, Y2 = end.Y,
            Stroke = stroke, StrokeThickness = width
        };
        Drawing.Children.Add(l);
        return l;
    }

    private void DrawAnchor()
    {
        Point p = At(PdfAnchorPoint);
        const double radius = 5;
        var circle = new Ellipse
        {
            Width = radius * 2, Height = radius * 2,
            Stroke = Brushes.White, StrokeThickness = 1.5,
            Fill = Brushes.Red, IsHitTestVisible = false
        };
        Canvas.SetLeft(circle, p.X - radius);
        Canvas.SetTop(circle, p.Y - radius);
        Drawing.Children.Add(circle);
    }

    private void UpdateAnchorInfo() =>
        AnchorInfo.Text = $"Anchor: {PdfAnchorPoint.X:F1}, {PdfAnchorPoint.Y:F1} pt";

    private void RotationChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RotationComboBox?.SelectedItem is not ComboBoxItem item ||
            !double.TryParse(item.Tag?.ToString(), out double angle)) return;
        RotationDegrees = angle;
        viewportReady = false;
        if (Drawing == null || Summary == null || WallGrid == null) return;
        Render((WallGrid.SelectedItem as WallCandidate)?.Id ?? -1);
    }

    private void PickAnchorClick(object sender, RoutedEventArgs e)
    {
        isPickingAnchor = !isPickingAnchor;
        Drawing.Cursor = isPickingAnchor ? Cursors.Cross : Cursors.Arrow;
        PickAnchorButton.Content = isPickingAnchor ? "Cancel picking" : "Pick Anchor Point";
    }

    private void DrawingClick(object sender, MouseButtonEventArgs e)
    {
        if (isPickingAnchor)
        {
            PdfAnchorPoint = FromCanvas(e.GetPosition(Drawing));
            isPickingAnchor = false;
            Drawing.Cursor = Cursors.Arrow;
            PickAnchorButton.Content = "Pick Anchor Point";
            UpdateAnchorInfo();
            Render((WallGrid.SelectedItem as WallCandidate)?.Id ?? -1);
            e.Handled = true;
            return;
        }
        if (e.OriginalSource is not Line { Tag: WallCandidate })
        {
            isMarqueePending = true;
            isMarqueeDragging = false;
            marqueeStart = e.GetPosition(Drawing);
            Drawing.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (e.OriginalSource is Line { Tag: WallCandidate candidate })
        {
            int index = candidates.IndexOf(candidate);
            if (index < 0) return;

            WallGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            WallGrid.CommitEdit(DataGridEditingUnit.Row, true);

            ModifierKeys modifiers = Keyboard.Modifiers;
            bool remove = (modifiers & ModifierKeys.Control) != 0;
            bool range = (modifiers & ModifierKeys.Shift) != 0 &&
                         lastClickedCandidateIndex >= 0;

            if (range)
            {
                int start = Math.Min(index, lastClickedCandidateIndex);
                int end = Math.Max(index, lastClickedCandidateIndex);
                for (int i = start; i <= end; i++)
                    candidates[i].Approved = !remove;
            }
            else
                candidate.Approved = !remove;

            if (!range) lastClickedCandidateIndex = index;

            WallGrid.SelectedItem = candidate;
            WallGrid.ScrollIntoView(candidate);
            WallGrid.Items.Refresh();
            Render(candidate.Id);
            e.Handled = true;
        }
    }

    private void DrawingMouseMove(object sender, MouseEventArgs e)
    {
        if (!isMarqueePending) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            CancelMarquee();
            return;
        }

        Point current = e.GetPosition(Drawing);
        if (!isMarqueeDragging &&
            (current - marqueeStart).Length < MarqueeDragThreshold)
            return;

        if (!isMarqueeDragging)
        {
            isMarqueeDragging = true;
            marqueeRectangle = new Rectangle
            {
                Stroke = Brushes.DodgerBlue,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                Fill = new SolidColorBrush(Color.FromArgb(35, 30, 100, 220)),
                IsHitTestVisible = false
            };
            Drawing.Children.Add(marqueeRectangle);
        }

        UpdateMarqueeVisual(current);
        e.Handled = true;
    }

    private void DrawingMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!isMarqueePending) return;

        bool dragged = isMarqueeDragging;
        Point end = e.GetPosition(Drawing);
        Rect selection = CreateSelectionRect(marqueeStart, end);
        CancelMarquee();

        if (dragged)
        {
            WallGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            WallGrid.CommitEdit(DataGridEditingUnit.Row, true);

            bool remove = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            foreach (WallCandidate candidate in candidates)
            {
                if (SegmentIntersectsRect(At(candidate.A), At(candidate.B), selection))
                    candidate.Approved = !remove;
            }
            WallGrid.Items.Refresh();
            Render((WallGrid.SelectedItem as WallCandidate)?.Id ?? -1);
        }
        e.Handled = true;
    }

    private void DrawingLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (isMarqueePending) CancelMarquee();
    }

    private void UpdateMarqueeVisual(Point current)
    {
        if (marqueeRectangle == null) return;
        Rect rect = CreateSelectionRect(marqueeStart, current);
        Canvas.SetLeft(marqueeRectangle, rect.Left);
        Canvas.SetTop(marqueeRectangle, rect.Top);
        marqueeRectangle.Width = rect.Width;
        marqueeRectangle.Height = rect.Height;
    }

    private void CancelMarquee()
    {
        isMarqueePending = false;
        isMarqueeDragging = false;
        if (marqueeRectangle != null)
        {
            Drawing.Children.Remove(marqueeRectangle);
            marqueeRectangle = null;
        }
        if (Drawing.IsMouseCaptured)
            Drawing.ReleaseMouseCapture();
    }

    private static Rect CreateSelectionRect(Point start, Point end) =>
        new(start, end);

    private static bool SegmentIntersectsRect(Point a, Point b, Rect rect)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double t0 = 0, t1 = 1;
        double[] p = { -dx, dx, -dy, dy };
        double[] q = { a.X - rect.Left, rect.Right - a.X,
                       a.Y - rect.Top, rect.Bottom - a.Y };
        for (int i = 0; i < 4; i++)
        {
            if (Math.Abs(p[i]) < 1e-12)
            {
                if (q[i] < 0) return false;
                continue;
            }
            double r = q[i] / p[i];
            if (p[i] < 0) t0 = Math.Max(t0, r);
            else t1 = Math.Min(t1, r);
            if (t0 > t1) return false;
        }
        return true;
    }

    private void GridSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        Render((WallGrid.SelectedItem as WallCandidate)?.Id ?? -1);

    private void RefreshClick(object sender, RoutedEventArgs e)
    {
        WallGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        WallGrid.CommitEdit(DataGridEditingUnit.Row, true);
        Render((WallGrid.SelectedItem as WallCandidate)?.Id ?? -1);
    }

    private void SelectAllClick(object sender, RoutedEventArgs e)
    {
        WallGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        WallGrid.CommitEdit(DataGridEditingUnit.Row, true);
        foreach (var wall in candidates) wall.Approved = true;
        WallGrid.Items.Refresh();
        Render((WallGrid.SelectedItem as WallCandidate)?.Id ?? -1);
    }

    private void ClearAllClick(object sender, RoutedEventArgs e)
    {
        WallGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        WallGrid.CommitEdit(DataGridEditingUnit.Row, true);
        foreach (var wall in candidates) wall.Approved = false;
        lastClickedCandidateIndex = -1;
        WallGrid.Items.Refresh();
        Render((WallGrid.SelectedItem as WallCandidate)?.Id ?? -1);
    }

    private void WallGridCurrentCellChanged(object sender, EventArgs e)
    {
        if (WallGrid == null || Summary == null || Drawing == null) return;
        Render((WallGrid.SelectedItem as WallCandidate)?.Id ?? -1);
    }

    private void UpdateZoomLabel() => ZoomLabel.Text = $"{zoom * 100:0}%";

    private void SetZoom(double newZoom, Point? canvasPoint = null, Point? viewportPoint = null)
    {
        newZoom = Math.Max(MinZoom, Math.Min(MaxZoom, newZoom));
        if (Math.Abs(zoom - newZoom) < 0.0001) return;

        Point focus = canvasPoint ?? new Point(Drawing.Width / 2, Drawing.Height / 2);
        Point view = viewportPoint ?? new Point(PreviewScroll.ViewportWidth / 2, PreviewScroll.ViewportHeight / 2);

        zoom = newZoom;
        DrawingZoomTransform.ScaleX = zoom;
        DrawingZoomTransform.ScaleY = zoom;
        UpdateZoomLabel();

        PreviewScroll.Dispatcher.BeginInvoke(new Action(() =>
        {
            PreviewScroll.UpdateLayout();
            PreviewScroll.ScrollToHorizontalOffset(focus.X * zoom - view.X);
            PreviewScroll.ScrollToVerticalOffset(focus.Y * zoom - view.Y);
        }), DispatcherPriority.Loaded);
    }

    private void PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (isPanning) return;
        Point logical = e.GetPosition(Drawing);
        Point viewport = e.GetPosition(PreviewScroll);
        SetZoom(zoom * Math.Pow(1.15, e.Delta / 120.0), logical, viewport);
        e.Handled = true;
    }

    private void ZoomInClick(object sender, RoutedEventArgs e) => SetZoom(zoom * 1.25);
    private void ZoomOutClick(object sender, RoutedEventArgs e) => SetZoom(zoom / 1.25);

    private void FitClick(object sender, RoutedEventArgs e)
    {
        zoom = 1;
        DrawingZoomTransform.ScaleX = 1;
        DrawingZoomTransform.ScaleY = 1;
        UpdateZoomLabel();
        PreviewScroll.ScrollToHorizontalOffset(0);
        PreviewScroll.ScrollToVerticalOffset(0);
    }

    private void PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle) return;
        isPanning = true;
        panStart = e.GetPosition(PreviewScroll);
        panStartHorizontal = PreviewScroll.HorizontalOffset;
        panStartVertical = PreviewScroll.VerticalOffset;
        PreviewScroll.CaptureMouse();
        PreviewScroll.Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!isPanning) return;
        if (e.MiddleButton != MouseButtonState.Pressed)
        {
            EndPan();
            return;
        }
        Point now = e.GetPosition(PreviewScroll);
        PreviewScroll.ScrollToHorizontalOffset(panStartHorizontal - (now.X - panStart.X));
        PreviewScroll.ScrollToVerticalOffset(panStartVertical - (now.Y - panStart.Y));
        e.Handled = true;
    }

    private void PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || !isPanning) return;
        EndPan();
        e.Handled = true;
    }

    private void PreviewLostMouseCapture(object sender, MouseEventArgs e) => EndPan();

    private void EndPan()
    {
        isPanning = false;
        if (PreviewScroll.IsMouseCaptured) PreviewScroll.ReleaseMouseCapture();
        PreviewScroll.Cursor = Cursors.Arrow;
    }

    private void ConfirmClick(object sender, RoutedEventArgs e)
    {
        WallGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        WallGrid.CommitEdit(DataGridEditingUnit.Row, true);
        int approvedCount = candidates.Count(x => x.Approved);
        if (approvedCount == 0)
        {
            MessageBox.Show("Select at least one wall to create.", "PDF to Revit");
            return;
        }
        if (approvedCount > 200 && MessageBox.Show(
            $"You selected {approvedCount:N0} candidate walls. The detector may include furniture and dimensions. Continue?",
            "Confirm bulk creation", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        DialogResult = true;
    }
}
