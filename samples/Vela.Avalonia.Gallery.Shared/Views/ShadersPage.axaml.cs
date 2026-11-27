using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Vela.Avalonia.Gallery.Views;

public partial class ShadersPage : UserControl
{
    private bool _draggingGlass;
    private Point _dragStartPointer;
    private Point _dragStartPos;

    public ShadersPage()
    {
        InitializeComponent();
    }

    private void OnGlassDragStart(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(DraggableGlassPill).Properties.IsLeftButtonPressed)
            return;

        _draggingGlass = true;
        _dragStartPointer = e.GetPosition(GlassStageCanvas);
        _dragStartPos = new Point(Canvas.GetLeft(DraggableGlassPill), Canvas.GetTop(DraggableGlassPill));
        e.Pointer.Capture(DraggableGlassPill);
        e.Handled = true;
    }

    private void OnGlassDragMove(object? sender, PointerEventArgs e)
    {
        if (!_draggingGlass) return;
        var current = e.GetPosition(GlassStageCanvas);
        var nextX = _dragStartPos.X + (current.X - _dragStartPointer.X);
        var nextY = _dragStartPos.Y + (current.Y - _dragStartPointer.Y);

        var maxX = Math.Max(0, GlassStageCanvas.Bounds.Width - DraggableGlassPill.Bounds.Width);
        var maxY = Math.Max(0, GlassStageCanvas.Bounds.Height - DraggableGlassPill.Bounds.Height);
        Canvas.SetLeft(DraggableGlassPill, Math.Clamp(nextX, 0, maxX));
        Canvas.SetTop(DraggableGlassPill, Math.Clamp(nextY, 0, maxY));
        e.Handled = true;
    }

    private void OnGlassDragEnd(object? sender, PointerReleasedEventArgs e)
    {
        if (!_draggingGlass) return;
        _draggingGlass = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }
}
