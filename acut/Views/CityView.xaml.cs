using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace acut.Views;

public partial class CityView : UserControl
{
    private Point _dragStartPoint;
    private double _startHorizontalOffset;
    private double _startVerticalOffset;
    private bool _isPointerDown;
    private bool _isDragging;
    private const double DragThreshold = 6.0;

    public CityView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            CityMap.ScrollToHorizontalOffset(0);
            CityMap.ScrollToVerticalOffset(0);
        };
    }

    private void CityMap_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isPointerDown = true;
        _isDragging = false;
        _dragStartPoint = e.GetPosition(CityMap);
        _startHorizontalOffset = CityMap.HorizontalOffset;
        _startVerticalOffset = CityMap.VerticalOffset;
    }

    private void CityMap_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPointerDown || e.LeftButton != MouseButtonState.Pressed)
            return;

        Point currentPoint = e.GetPosition(CityMap);
        Vector delta = currentPoint - _dragStartPoint;

        if (!_isDragging && (Math.Abs(delta.X) > DragThreshold || Math.Abs(delta.Y) > DragThreshold))
        {
            _isDragging = true;
            CityMap.CaptureMouse();
            CityMap.Cursor = Cursors.ScrollAll;
        }

        if (_isDragging)
        {
            CityMap.ScrollToHorizontalOffset(_startHorizontalOffset - delta.X);
            CityMap.ScrollToVerticalOffset(_startVerticalOffset - delta.Y);
            e.Handled = true;
        }
    }

    private void CityMap_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            CityMap.ReleaseMouseCapture();
            CityMap.Cursor = Cursors.Arrow;
            e.Handled = true;
        }

        _isPointerDown = false;
        _isDragging = false;
    }

    private void CityMap_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
        {
            _isPointerDown = false;
        }
    }
}