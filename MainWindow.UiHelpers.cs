using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TraceEngineer;

public partial class MainWindow : Window
{

    private void Center(double w, double h, string text, double size, string hex)
    {
        var tb = Label(text, size, hex);
        Canvas.SetLeft(tb, w / 2 - 200);
        Canvas.SetTop(tb, h / 2 - 10);
        ChartCanvas.Children.Add(tb);
    }
    private static TextBlock Label(string text, double size, string hex) =>
        new() { Text = text, FontSize = size, Foreground = Brush(hex) };

    private static TextBlock Label(string text, double size, Color c) =>
        new() { Text = text, FontSize = size, Foreground = new SolidColorBrush(c) };


    private static string FormatLapTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds:000}";
    }

}