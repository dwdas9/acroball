using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Acroball.Domain.Annotations;
using Acroball.UI.Services;
using Acroball.UI.ViewModels;

namespace Acroball.UI.Views;

/// <summary>
/// Code-behind for the Viewer tool view: drives lazy per-page rendering off
/// the virtualizing panel's container realize/derealize notifications,
/// bridges bookmark clicks to scrolling the page list, and captures pointer
/// input on each page's overlay canvas to draw annotations (ADR-0013).
/// </summary>
public partial class ViewerView : UserControl
{
    private ViewerViewModel? _subscribedViewModel;

    // Drag state for Square/Highlight/Ink — at most one drag is ever active,
    // since the pointer is captured on the canvas it started on.
    private ViewerPageViewModel? _dragPage;
    private Canvas? _dragCanvas;
    private Point _dragStart;
    private List<Point>? _inkPoints;

    // Inline text-entry state for FreeText.
    private TextBox? _activeFreeTextBox;
    private ViewerPageViewModel? _activeFreeTextPage;
    private Point _activeFreeTextOrigin;

    /// <summary>Creates the view.</summary>
    public ViewerView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private ViewerViewModel? ViewModel => DataContext as ViewerViewModel;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.ScrollToPageRequested -= OnScrollToPageRequested;
        }

        _subscribedViewModel = ViewModel;

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.ScrollToPageRequested += OnScrollToPageRequested;
        }
    }

    private void OnPageContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container.DataContext is ViewerPageViewModel page)
        {
            _ = ViewModel?.LoadPageImageAsync(page);
        }
    }

    private void OnPageContainerClearing(object? sender, ContainerClearingEventArgs e)
    {
        if (e.Container.DataContext is ViewerPageViewModel page)
        {
            ViewModel?.UnloadPageImage(page);
        }
    }

    private void OnOutlineSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is OutlineNodeViewModel { DestinationPageNumber: { } pageNumber })
        {
            ViewModel?.RequestScrollToPage(pageNumber);
        }
    }

    private void OnScrollToPageRequested(int pageNumber)
    {
        var page = ViewModel?.Pages.FirstOrDefault(p => p.PageNumber == pageNumber);
        if (page is not null)
        {
            PagesList.ScrollIntoView(page);
        }
    }

    // ======================== annotation drawing ========================

    private void OnPageCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Canvas canvas || ViewModel is not { } vm || canvas.DataContext is not ViewerPageViewModel page)
        {
            return;
        }

        var tool = vm.SelectedAnnotationTool;
        if (tool is null)
        {
            return;
        }

        var point = e.GetPosition(canvas);
        e.Pointer.Capture(canvas);

        switch (tool)
        {
            case AnnotationKind.Square:
            case AnnotationKind.Highlight:
                _dragPage = page;
                _dragCanvas = canvas;
                _dragStart = point;
                break;
            case AnnotationKind.Ink:
                _dragPage = page;
                _dragCanvas = canvas;
                _inkPoints = [point];
                break;
            case AnnotationKind.FreeText:
                BeginFreeTextInput(canvas, page, point);
                break;
        }

        e.Handled = true;
    }

    private void OnPageCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_inkPoints is not null && sender is Canvas canvas && ReferenceEquals(canvas, _dragCanvas))
        {
            _inkPoints.Add(e.GetPosition(canvas));
        }
    }

    private void OnPageCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not Canvas canvas || !ReferenceEquals(canvas, _dragCanvas) || ViewModel is not { } vm || _dragPage is not { } page)
        {
            _dragPage = null;
            _dragCanvas = null;
            _inkPoints = null;
            return;
        }

        e.Pointer.Capture(null);
        var end = e.GetPosition(canvas);

        if (_inkPoints is { } inkPoints)
        {
            CommitInk(vm, page, inkPoints);
        }
        else
        {
            CommitRect(vm, page, _dragStart, end);
        }

        _dragPage = null;
        _dragCanvas = null;
        _inkPoints = null;
        e.Handled = true;
    }

    private static void CommitRect(ViewerViewModel vm, ViewerPageViewModel page, Point start, Point end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);
        if (width < 4 || height < 4)
        {
            // Too small to be an intentional shape — likely an accidental click.
            return;
        }

        var (x1, y1) = MapPoint(page, left, top);
        var (x2, y2) = MapPoint(page, left + width, top + height);
        var minX = Math.Min(x1, x2);
        var maxX = Math.Max(x1, x2);
        var minY = Math.Min(y1, y2);
        var maxY = Math.Max(y1, y2);

        if (vm.SelectedAnnotationTool == AnnotationKind.Highlight)
        {
            var quad = new QuadPoints(minX, maxY, maxX, maxY, minX, minY, maxX, minY);
            vm.AddHighlightAnnotation(page, quad, (left, top, width, height));
        }
        else
        {
            vm.AddSquareAnnotation(page, (minX, minY, maxX - minX, maxY - minY), (left, top, width, height));
        }
    }

    private static void CommitInk(ViewerViewModel vm, ViewerPageViewModel page, List<Point> points)
    {
        if (points.Count < 2)
        {
            return;
        }

        var pdfPoints = points.Select(p =>
        {
            var (x, y) = MapPoint(page, p.X, p.Y);
            return new InkPoint(x, y);
        }).ToList();

        vm.AddInkAnnotation(page, new InkStroke(pdfPoints), new Points(points));
    }

    private static (double X, double Y) MapPoint(ViewerPageViewModel page, double screenX, double screenY)
        => AnnotationCoordinateMapper.ToPdfPoint(screenX, screenY, ViewerPageViewModel.DisplayWidthPx, page.WidthPoints, page.HeightPoints, page.Rotation);

    private void BeginFreeTextInput(Canvas canvas, ViewerPageViewModel page, Point origin)
    {
        RemoveActiveFreeTextBox();

        var textBox = new TextBox
        {
            Width = 200,
            Height = 60,
            AcceptsReturn = true,
            PlaceholderText = "Type a note, then press Enter",
        };
        Canvas.SetLeft(textBox, origin.X);
        Canvas.SetTop(textBox, origin.Y);
        canvas.Children.Add(textBox);

        _activeFreeTextBox = textBox;
        _activeFreeTextPage = page;
        _activeFreeTextOrigin = origin;

        textBox.KeyDown += OnFreeTextBoxKeyDown;
        textBox.LostFocus += OnFreeTextBoxLostFocus;
        textBox.Focus();
    }

    private void OnFreeTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            CommitFreeTextInput();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            RemoveActiveFreeTextBox();
            e.Handled = true;
        }
    }

    private void OnFreeTextBoxLostFocus(object? sender, RoutedEventArgs e) => CommitFreeTextInput();

    private void CommitFreeTextInput()
    {
        if (_activeFreeTextBox is null || _activeFreeTextPage is null || ViewModel is not { } vm)
        {
            RemoveActiveFreeTextBox();
            return;
        }

        var text = (_activeFreeTextBox.Text ?? string.Empty).Trim();
        var page = _activeFreeTextPage;
        var origin = _activeFreeTextOrigin;
        var width = _activeFreeTextBox.Width;
        var height = _activeFreeTextBox.Height;

        RemoveActiveFreeTextBox();

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var (x1, y1) = MapPoint(page, origin.X, origin.Y);
        var (x2, y2) = MapPoint(page, origin.X + width, origin.Y + height);
        var minX = Math.Min(x1, x2);
        var maxX = Math.Max(x1, x2);
        var minY = Math.Min(y1, y2);
        var maxY = Math.Max(y1, y2);

        vm.AddFreeTextAnnotation(page, (minX, minY, maxX - minX, maxY - minY), (origin.X, origin.Y, width, height), text);
    }

    private void RemoveActiveFreeTextBox()
    {
        if (_activeFreeTextBox is not null)
        {
            _activeFreeTextBox.KeyDown -= OnFreeTextBoxKeyDown;
            _activeFreeTextBox.LostFocus -= OnFreeTextBoxLostFocus;
            (_activeFreeTextBox.Parent as Canvas)?.Children.Remove(_activeFreeTextBox);
        }

        _activeFreeTextBox = null;
        _activeFreeTextPage = null;
    }
}
