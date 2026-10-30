using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;        // MouseMove, MouseEventArgs
using System.Windows.Media;
using System.Windows.Shapes;       // Line per il cursore verticale, Ellipse per HoverDot
using TraceEngineer.Models;

namespace TraceEngineer;

public partial class MainWindow : Window
{

    private void ChartCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_activeTab == "stint")
        {
            HandleStintProbe(e.GetPosition(ChartCanvas));
            return;
        }        
        if ((_hoverLanes.Count == 0 && _hoverCompound.Count == 0) || _hoverEntries.Count == 0 || _plotW <= 0)
        { HideHover(); return; }

        var pos = e.GetPosition(ChartCanvas);

        HoverLane? hi = null;
        foreach (var h in _hoverLanes)
            if (pos.Y >= h.TraceTop - 3 && pos.Y <= h.TraceTop + h.TraceH + 3) { hi = h; break; }

        if (hi == null)
        {
            HoverCompound? hc = null;
            bool inA = false;
            foreach (var c in _hoverCompound)
            {
                if (pos.Y >= c.ATop - 3 && pos.Y <= c.ATop + c.AH + 3) { hc = c; inA = true; break; }
                if (pos.Y >= c.BTop - 3 && pos.Y <= c.BTop + c.BH + 3) { hc = c; inA = false; break; }
            }
            if (hc == null) { HideHover(); return; }

            double cfrac = (pos.X - LeftPad) / _plotW;
            if (cfrac < 0 || cfrac > 1) { HideHover(); return; }
            double cxVal = cfrac * _xSpan;

            int wi = hc.WheelIndex;
            double cDist = double.MaxValue;
            string cText = "";
            double cX = 0, cY = 0;
            var cCol = Colors.White;

            for (int gi = 0; gi < _hoverEntries.Count; gi++)
            {
                var entry = _hoverEntries[gi];
                int idx = IndexAtX(entry.Points, _hoverSelX, cxVal);
                if (idx < 0) continue;
                var p = entry.Points[idx];
                double px = LeftPad + (_hoverSelX(p) / _xSpan) * _plotW;

                var candidates = new List<(string N, Func<TelemetryPoint, double> S, Color C)>();
                if (inA)
                {
                    if (_hoverCompare)
                        candidates.Add(("carcass", Carcass(wi), LapColors[gi]));
                    else
                    {
                        candidates.Add(("carcass", Carcass(wi), Color.FromRgb(255, 255, 255)));
                        candidates.Add(("inner", Inner(wi), Color.FromRgb(255, 82, 82)));
                        candidates.Add(("centre", Centre(wi), Color.FromRgb(255, 205, 60)));
                        candidates.Add(("outer", Outer(wi), Color.FromRgb(150, 255, 130)));
                        candidates.Add(("rim", Rim(wi), Color.FromRgb(255, 145, 0)));
                    }
                }
                else
                {
                    candidates.Add(("pressure", Press(wi), _hoverCompare ? LapColors[gi] : Color.FromRgb(0, 214, 255)));
                    candidates.Add(("wear", TWear(wi), _hoverCompare ? LapColors[gi] : Color.FromRgb(255, 77, 210)));
                }

                foreach (var cd in candidates)
                {
                    double v = cd.S(p);
                    double py = inA
                        ? hc.ATop + hc.AH - ((v - hc.AMin) / hc.ASpan) * hc.AH
                        : (cd.N == "pressure"
                            ? hc.BTop + hc.BH - ((v - hc.PMin) / hc.PSpan) * hc.BH
                            : hc.BTop + hc.BH - ((v - hc.WMin) / hc.WSpan) * hc.BH);
                    double d = Math.Abs(py - pos.Y);
                    if (d < cDist)
                    {
                        cDist = d; cX = px; cY = py; cCol = cd.C;
                        string lapTag = _hoverCompare ? entry.Label + "  " : "";
                        string xTxt = _axisDistance ? $"{_hoverSelX(p):F0} m" : $"{_hoverSelX(p):F2} s";
                        string vTxt = inA ? $"{v:F1} °C" : $"{v:F2}";
                        cText = $"{lapTag}{WheelKey[wi].Name} {cd.N}\n{xTxt}    {vTxt}";
                    }
                }
            }

            if (cDist > 12) { HideHover(); return; }
            ShowHover(cX, cY, cCol, cText);
            return;
        }

        double frac = (pos.X - LeftPad) / _plotW;
        if (frac < 0 || frac > 1) { HideHover(); return; }
        double xVal = frac * _xSpan;

        if (hi.IsDelta && hi.Delta != null && hi.Delta.Count > 1)
        {
            int di = IndexAtDeltaX(hi.Delta, xVal);
            var (dx, dy) = hi.Delta[di];
            double px = LeftPad + (dx / _xSpan) * _plotW;
            double py = hi.TraceTop + hi.TraceH - ((dy - hi.VMin) / hi.VSpan) * hi.TraceH;
            if (Math.Abs(py - pos.Y) > 12) { HideHover(); return; }

            string xTxt = _axisDistance ? $"{dx:F0} m" : $"{dx:F2} s";
            string sign = dy >= 0 ? "+" : "";
            ShowHover(px, py, Color.FromRgb(255, 77, 210),
                      $"Δ Time (lap2 − lap1)\n{xTxt}    {sign}{dy:F3} s");
            return;
        }

        double bestDist = double.MaxValue;
        string bestText = "";
        double bestX = 0, bestY = 0;
        var bestCol = Colors.White;

        for (int gi = 0; gi < _hoverEntries.Count; gi++)
        {
            var entry = _hoverEntries[gi];
            int idx = IndexAtX(entry.Points, _hoverSelX, xVal);
            if (idx < 0) continue;
            var p = entry.Points[idx];
            double px = LeftPad + (_hoverSelX(p) / _xSpan) * _plotW;

            int seriesCount = hi.Lane.Wheel != null ? hi.Lane.Wheel.Count : 1;
            for (int w = 0; w < seriesCount; w++)
            {
                Func<TelemetryPoint, double> sel = hi.Lane.Wheel != null ? hi.Lane.Wheel[w].Sel : hi.Lane.Sel;
                string sname = hi.Lane.Wheel != null ? hi.Lane.Wheel[w].Name : "";
                Color col = _hoverCompare
                    ? LapColors[gi]
                    : (hi.Lane.Wheel != null ? WheelKey[w].Color : hi.Lane.Color);

                double v = sel(p);
                double py = hi.TraceTop + hi.TraceH - ((v - hi.VMin) / hi.VSpan) * hi.TraceH;
                double d = Math.Abs(py - pos.Y);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestX = px;
                    bestY = py;
                    bestCol = col;
                    string lapTag = _hoverCompare ? entry.Label + "  " : "";
                    string wheelTag = sname.Length > 0 ? sname + "  " : "";
                    string xTxt = _axisDistance ? $"{_hoverSelX(p):F0} m" : $"{_hoverSelX(p):F2} s";
                    string vTxt = hi.Lane.Unit.Length > 0 ? $"{v:F1} {hi.Lane.Unit}" : $"{v:F3}";
                    bestText = $"{lapTag}{wheelTag}{hi.Lane.Name}\n{xTxt}    {vTxt}";
                }
            }
        }

        if (bestDist > 12) { HideHover(); return; }

        ShowHover(bestX, bestY, bestCol, bestText);
    }


    private void CursorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _cursorFrac = CursorSlider.Value / 1000.0;   // sempre, anche senza linea
        if (_cursorLine == null) return;
        UpdateCursorVisual();
    }


    private void UpdateCursorVisual()
    {
        _cursorFrac = CursorSlider.Value / 1000.0;
        if (_cursorLine == null || _cursorLabel == null || _plotW <= 0) return;

        double x = LeftPad + _cursorFrac * _plotW;

        _cursorLine.X1 = x;
        _cursorLine.X2 = x;
        _cursorLine.Y1 = TopPad;
        _cursorLine.Y2 = ChartCanvas.ActualHeight;

        _cursorLabel.Text = _axisDistance
            ? $"{_cursorFrac * _xSpan:F0} m"
            : $"{_cursorFrac * _xSpan:F2} s";

        Canvas.SetLeft(_cursorLabel, x + 4);
        Canvas.SetTop(_cursorLabel, TopPad + 2);
        // ---- Segnaposto track map ----
        if (_mapHasGps && _mapOutline != null && _mapDots.Count > 0 && _xSpan > 0)
        {
            for (int i = 0; i < _mapDots.Count && i < _hoverEntries.Count; i++)
            {
                var entry = _hoverEntries[i];
                if (entry.Points.Count == 0) continue;
                int idx = IndexAtX(entry.Points, _hoverSelX, _cursorFrac * _xSpan);
                if (idx < 0) continue;
                double f = entry.Points.Count > 1
                    ? (double)idx / (entry.Points.Count - 1) : 0;
                Point pt = PointAtArcFraction(f);
                Canvas.SetLeft(_mapDots[i], pt.X - 3.5);
                Canvas.SetTop(_mapDots[i], pt.Y - 3.5);
            }
        }        
    }

    // Risale dal punto cliccato all'elemento della lista, gestendo sia
    // Visual sia ContentElement (es. <Run> dentro TextBlock: il genitore
    // logico è il TextBlock, non un TextElement).
    private static object? ItemAtPoint(ListBox list, Point p)
    {
        var el = list.InputHitTest(p) as DependencyObject;
        while (el != null && el != list)
        {
            if (el is ListBoxItem lbi)
                return list.ItemContainerGenerator.ItemFromContainer(lbi);
            el = GetParentAny(el);
        }
        return null;
    }

    private static DependencyObject? GetParentAny(DependencyObject el)
    {
        if (el is Visual v)
            return VisualTreeHelper.GetParent(v);

        if (el is ContentElement)
            return LogicalTreeHelper.GetParent(el);   // Run -> TextBlock -> StackPanel -> ...

        return null;
    }

    private void ShowHover(double x, double y, Color col, string text)
    {
        HoverDot.Visibility = Visibility.Visible;
        HoverDot.Fill = new SolidColorBrush(col);
        HoverDot.Margin = new Thickness(x - 3.5, y - 3.5, 0, 0);

        HoverTipText.Text = text;
        HoverTip.Visibility = Visibility.Visible;
        double tipX = Math.Min(x + 12, Math.Max(0, ChartCanvas.ActualWidth - 180));
        double tipY = Math.Max(4, y - 46);
        HoverTip.Margin = new Thickness(tipX, tipY, 0, 0);
    }

    private void HideHover()
    {
        HoverTip.Visibility = Visibility.Collapsed;
        HoverDot.Visibility = Visibility.Collapsed;
    }        


}