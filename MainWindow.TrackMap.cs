using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;       // Polyline, Ellipse per la mappa
using TraceEngineer.Models;

namespace TraceEngineer;

public partial class MainWindow : Window
{

    private void RefreshTrackMap()
    {
        if (_activeTab == "stint")
        { TrackMapBorder.Visibility = Visibility.Collapsed; return; }        
        TrackMapCanvas.Children.Clear();
        _mapDots.Clear();

        if (_mapProfile == null || _hoverEntries.Count == 0)
        { TrackMapBorder.Visibility = Visibility.Collapsed; return; }

        // Disegna profilo cached
        var poly = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromRgb(120, 120, 128)),
            StrokeThickness = 1.5
        };
        foreach (var p in _mapProfile) poly.Points.Add(p);
        TrackMapCanvas.Children.Add(poly);

        // Start/finish
        var sf = new Ellipse
        {
            Width = 5, Height = 5,
            Fill = new SolidColorBrush(Color.FromRgb(255, 255, 255))
        };
        Canvas.SetLeft(sf, _mapProfile[0].X - 2.5);
        Canvas.SetTop(sf, _mapProfile[0].Y - 2.5);
        TrackMapCanvas.Children.Add(sf);

        // Pallini per ogni giro confrontato
        for (int i = 0; i < _hoverEntries.Count; i++)
        {
            var dot = new Ellipse
            {
                Width = 7, Height = 7,
                Fill = new SolidColorBrush(LapColors[i % LapColors.Length]),
                Stroke = new SolidColorBrush(Colors.Black),
                StrokeThickness = 1
            };
            TrackMapCanvas.Children.Add(dot);
            _mapDots.Add(dot);
        }

        TrackMapBorder.Visibility = Visibility.Visible;
        UpdateCursorVisual();
    }

    private Point PointAtArcFraction(double f)
    {
        if (_mapOutline == null || _mapOutline.Count < 2) return new Point(0, 0);
        f = Math.Clamp(f, 0, 1);
        double target = f * _mapTotalLen;
        int lo = 0, hi = _mapCumLen.Count - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (_mapCumLen[mid] < target) lo = mid; else hi = mid;
        }
        double seg = _mapCumLen[hi] - _mapCumLen[lo];
        double t = seg > 1e-9 ? (target - _mapCumLen[lo]) / seg : 0;
        var a = _mapOutline[lo]; var b = _mapOutline[hi];
        return new Point(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    }

    private Point MapToCanvas(double rx, double ry)
    {
        double w = TrackMapCanvas.ActualWidth, h = TrackMapCanvas.ActualHeight;
        double pad = 6;
        double sx = (w - 2 * pad) / (_mapRawMaxX - _mapRawMinX);
        double sy = (h - 2 * pad) / (_mapRawMaxY - _mapRawMinY);
        double s = Math.Min(sx, sy);
        double ox = pad + ((w - 2 * pad) - (_mapRawMaxX - _mapRawMinX) * s) / 2;
        double oy = pad + ((h - 2 * pad) - (_mapRawMaxY - _mapRawMinY) * s) / 2;
        return new Point(ox + (rx - _mapRawMinX) * s,
                         oy + (_mapRawMaxY - ry) * s);   // Y flip: nord in alto
    }

    
}