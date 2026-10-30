using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;        // se HandleStintProbe usa MouseEventArgs
using System.Windows.Media;
using System.Windows.Shapes;       // Line, Polyline, Rectangle per il disegno lane
using TraceEngineer.Models;

namespace TraceEngineer;

public partial class MainWindow : Window
{

    private void DrawStintChart()
    {
        ChartCanvas.Children.Clear();
        _hoverStintLanes.Clear();

        if (_lapStats == null || _lapStats.Count == 0)
        {
            Center(ChartCanvas.ActualWidth, ChartCanvas.ActualHeight,
                   "Load a session to see the stint chart", 14, "#FFFFFF");
            return;
        }

        var stats = _lapStats;
        if (ChkStintExclude.IsChecked == true)
        {
            double med = Median(stats.Select(s => s.LapTime).ToList());
            double thrHigh = med * AnomalousFactor;   // giri lenti (out-lap, pit, warmup)
            double thrLow = med * IncompleteFactor;   // giri tronchi/incompleti (in-lap)
            stats = stats.Where(s => s.LapTime <= thrHigh && s.LapTime >= thrLow).ToList();
        }
        if (stats.Count == 0)
        {
            Center(ChartCanvas.ActualWidth, ChartCanvas.ActualHeight,
                   "No laps after filter", 14, "#FFFFFF");
            return;
        }

        // ---- Costruisci una lane per metrica ----
        var lanes = new List<StintLane>();

        if (ChkStLapTime.IsChecked == true)
            lanes.Add(new StintLane { Name = "Lap time", Unit = "s", Series =
            {
                new StintSeries { Name="lap", Color=Color.FromRgb(255,255,255), Values=stats.Select(s=>s.LapTime).ToList() }
            }});

        if (ChkStTyreTemp.IsChecked == true)
            lanes.Add(new StintLane { Name = "Tyre temp", Unit = "°C", Series =
            {
                new StintSeries { Name="FL", Color=StintWheelColors[0], Values=stats.Select(s=>s.TyreTempFL).ToList() },
                new StintSeries { Name="FR", Color=StintWheelColors[1], Values=stats.Select(s=>s.TyreTempFR).ToList() },
                new StintSeries { Name="RL", Color=StintWheelColors[2], Values=stats.Select(s=>s.TyreTempRL).ToList() },
                new StintSeries { Name="RR", Color=StintWheelColors[3], Values=stats.Select(s=>s.TyreTempRR).ToList() }
            }});

        if (ChkStTyrePress.IsChecked == true)
            lanes.Add(new StintLane { Name = "Tyre pressure", Unit = "", Series =
            {
                new StintSeries { Name="FL", Color=StintWheelColors[0], Values=stats.Select(s=>s.TyrePressFL).ToList() },
                new StintSeries { Name="FR", Color=StintWheelColors[1], Values=stats.Select(s=>s.TyrePressFR).ToList() },
                new StintSeries { Name="RL", Color=StintWheelColors[2], Values=stats.Select(s=>s.TyrePressRL).ToList() },
                new StintSeries { Name="RR", Color=StintWheelColors[3], Values=stats.Select(s=>s.TyrePressRR).ToList() }
            }});

        if (ChkStTyreWear.IsChecked == true)
            lanes.Add(new StintLane { Name = "Tyre wear", Unit = "", Series =
            {
                new StintSeries { Name="FL", Color=StintWheelColors[0], Values=stats.Select(s=>s.TyreWearFL).ToList() },
                new StintSeries { Name="FR", Color=StintWheelColors[1], Values=stats.Select(s=>s.TyreWearFR).ToList() },
                new StintSeries { Name="RL", Color=StintWheelColors[2], Values=stats.Select(s=>s.TyreWearRL).ToList() },
                new StintSeries { Name="RR", Color=StintWheelColors[3], Values=stats.Select(s=>s.TyreWearRR).ToList() }
            }});

        if (ChkStSoC.IsChecked == true)
            lanes.Add(new StintLane { Name = "SoC", Unit = "%", Series =
            {
                new StintSeries { Name="start", Color=Color.FromRgb(186,104,255), Values=stats.Select(s=>s.SoCStart).ToList() },
                new StintSeries { Name="end",   Color=Color.FromRgb(0,230,118),   Values=stats.Select(s=>s.SoCEnd).ToList() }
            }});

        if (ChkStBrakeTemp.IsChecked == true)
            lanes.Add(new StintLane { Name = "Brake temp", Unit = "°C", Series =
            {
                new StintSeries { Name="FL", Color=StintWheelColors[0], Values=stats.Select(s=>s.BrakeTempFL).ToList() },
                new StintSeries { Name="FR", Color=StintWheelColors[1], Values=stats.Select(s=>s.BrakeTempFR).ToList() },
                new StintSeries { Name="RL", Color=StintWheelColors[2], Values=stats.Select(s=>s.BrakeTempRL).ToList() },
                new StintSeries { Name="RR", Color=StintWheelColors[3], Values=stats.Select(s=>s.BrakeTempRR).ToList() }
            }});
        if (ChkStBrakeWear.IsChecked == true)
            lanes.Add(new StintLane { Name = "Brake wear", Unit = "", Series =
            {
                new StintSeries { Name="FL", Color=StintWheelColors[0], Values=stats.Select(s=>s.BrakeWearFL).ToList() },
                new StintSeries { Name="FR", Color=StintWheelColors[1], Values=stats.Select(s=>s.BrakeWearFR).ToList() },
                new StintSeries { Name="RL", Color=StintWheelColors[2], Values=stats.Select(s=>s.BrakeWearRL).ToList() },
                new StintSeries { Name="RR", Color=StintWheelColors[3], Values=stats.Select(s=>s.BrakeWearRR).ToList() }
            }});
        if (ChkStSpeed.IsChecked == true)
            lanes.Add(new StintLane { Name = "Speed", Unit = "km/h", Series =
            {
                new StintSeries { Name="avg", Color=Color.FromRgb(255,205,60), Values=stats.Select(s=>s.AvgSpeed).ToList() },
                new StintSeries { Name="max", Color=Color.FromRgb(0,214,255),  Values=stats.Select(s=>s.MaxSpeed).ToList() }
            }});

        if (lanes.Count == 0)
        {
            Center(ChartCanvas.ActualWidth, ChartCanvas.ActualHeight,
                   "Select at least one metric", 14, "#FFFFFF");
            return;
        }

        // ---- Setup geometria ----
        double W = ChartCanvas.ActualWidth, H = ChartCanvas.ActualHeight;
        double plotW = W - LeftPad - RightPad;
        double plotH = H - TopPad - BottomPad;
        double laneH = plotH / lanes.Count;

        double lapMin = stats.Min(s => s.LapNumber);
        double lapMax = stats.Max(s => s.LapNumber);
        double lapSpan = lapMax - lapMin; if (lapSpan < 1) lapSpan = 1;
        double X(double lap) => LeftPad + ((lap - lapMin) / lapSpan) * plotW;

        // ---- Sfondo alternato ----
        for (int li = 0; li < lanes.Count; li++)
        {
            if (li % 2 == 0)
            {
                ChartCanvas.Children.Add(new Rectangle
                {
                    Width = plotW, Height = laneH,
                    Fill = new SolidColorBrush(Color.FromRgb(38, 38, 42)),
                    RenderTransform = new TranslateTransform(LeftPad, TopPad + li * laneH)
                });
            }     
        }

        // ---- Disegna ogni lane ----
        for (int li = 0; li < lanes.Count; li++)
        {
            var lane = lanes[li];
            double laneTop = TopPad + li * laneH;
            double traceTop = laneTop + 4;
            double traceH = Math.Max(6, laneH - 11);

            double yMin = double.MaxValue, yMax = double.MinValue;
            foreach (var sr in lane.Series)
                foreach (var v in sr.Values)
                { if (v < yMin) yMin = v; if (v > yMax) yMax = v; }
            double yPad = (yMax - yMin) * 0.08; if (yPad < 1e-9) yPad = 1;
            yMin -= yPad; yMax += yPad;
            double ySpan = yMax - yMin;
            double YL(double v) => traceTop + traceH - ((v - yMin) / ySpan) * traceH;

            foreach (var sr in lane.Series)
            {
                var poly = new Polyline
                {
                    Stroke = new SolidColorBrush(sr.Color),
                    StrokeThickness = 1.2
                };
                for (int i = 0; i < sr.Values.Count && i < stats.Count; i++)
                    poly.Points.Add(new Point(X(stats[i].LapNumber), YL(sr.Values[i])));
                ChartCanvas.Children.Add(poly);

                for (int i = 0; i < sr.Values.Count && i < stats.Count; i++)
                {
                    var dot = new Ellipse { Width = 5, Height = 5, Fill = new SolidColorBrush(sr.Color) };
                    Canvas.SetLeft(dot, X(stats[i].LapNumber) - 2.5);
                    Canvas.SetTop(dot, YL(sr.Values[i]) - 2.5);
                    ChartCanvas.Children.Add(dot);
                }
            }

            // Titolo lane
            string laneTitle = lane.Unit != "" ? $"{lane.Name} ({lane.Unit})" : lane.Name;
            var title = Label(laneTitle, 11, Color.FromRgb(200, 200, 206));
            title.FontWeight = FontWeights.SemiBold;
            Canvas.SetLeft(title, 5);
            Canvas.SetTop(title, laneTop + 3);
            ChartCanvas.Children.Add(title);

            // Scala Y (min / mid / max)
            foreach (var tv in new[] { yMax - yPad, (yMax + yMin) / 2, yMin + yPad })
            {
                double ty = YL(tv);
                string s = lane.Unit == "s" ? FormatLapTime(tv) : tv.ToString("0.#");
                var t = Label(s, 9, Color.FromRgb(150, 150, 155));
                Canvas.SetLeft(t, LeftPad + 4);
                Canvas.SetTop(t, ty - 6);
                ChartCanvas.Children.Add(t);
            }

            // Linea di separazione
            ChartCanvas.Children.Add(new Line
            {
                X1 = LeftPad, Y1 = laneTop + laneH, X2 = LeftPad + plotW, Y2 = laneTop + laneH,
                Stroke = new SolidColorBrush(Color.FromRgb(58, 58, 62)), StrokeThickness = 1
            });

            _hoverStintLanes.Add(new HoverStintLane
            {
                LaneName = lane.Name,
                Unit = lane.Unit,
                TraceTop = traceTop,
                TraceH = traceH,
                YMin = yMin,
                YSpan = ySpan,
                Series = lane.Series,
                LapNumbers = stats.Select(s => s.LapNumber).ToList(),
                Xs = stats.Select(s => X(s.LapNumber)).ToList()
            });
            
        }

        // ---- Legenda (orizzontale, in basso) ----
        var legend = new List<(string Name, Color Color)>();
        if (lanes.Any(l => l.Series.Count == 4))
        {
            legend.Add(("FL", StintWheelColors[0]));
            legend.Add(("FR", StintWheelColors[1]));
            legend.Add(("RL", StintWheelColors[2]));
            legend.Add(("RR", StintWheelColors[3]));
        }
        foreach (var l in lanes)
            if (l.Series.Count > 1 && l.Series.Count != 4)
                foreach (var s in l.Series)
                    legend.Add(($"{l.Name} {s.Name}", s.Color));

        double lgX = LeftPad;
        double lgY = TopPad + plotH + 4;
        foreach (var e in legend)
        {
            ChartCanvas.Children.Add(new Rectangle
            {
                Width = 14, Height = 3,
                Fill = new SolidColorBrush(e.Color),
                RenderTransform = new TranslateTransform(lgX, lgY + 6)
            });
            var lt = Label(e.Name, 9, Color.FromRgb(200, 200, 206));
            Canvas.SetLeft(lt, lgX + 18); Canvas.SetTop(lt, lgY);
            ChartCanvas.Children.Add(lt);
            lgX += 18 + Math.Max(20, e.Name.Length * 7) + 14;
        }
    }

    private void HandleStintProbe(Point pos)
    {
        if (_hoverStintLanes.Count == 0) { HideHover(); return; }

        HoverStintLane? lane = null;
        foreach (var l in _hoverStintLanes)
        {
            if (pos.Y >= l.TraceTop - 4 && pos.Y <= l.TraceTop + l.TraceH + 4)
            { lane = l; break; }
        }
        if (lane == null) { HideHover(); return; }

        int bestIdx = -1; double bestDx = double.MaxValue;
        for (int i = 0; i < lane.Xs.Count; i++)
        {
            double dx = Math.Abs(lane.Xs[i] - pos.X);
            if (dx < bestDx) { bestDx = dx; bestIdx = i; }
        }
        if (bestIdx < 0 || bestDx > 30) { HideHover(); return; }

        int bestSeries = -1; double bestDy = double.MaxValue; double bestVal = 0;
        for (int si = 0; si < lane.Series.Count; si++)
        {
            if (bestIdx >= lane.Series[si].Values.Count) continue;
            double v = lane.Series[si].Values[bestIdx];
            double y = lane.TraceTop + lane.TraceH - ((v - lane.YMin) / lane.YSpan) * lane.TraceH;
            double dy = Math.Abs(y - pos.Y);
            if (dy < bestDy) { bestDy = dy; bestSeries = si; bestVal = v; }
        }
        if (bestSeries < 0 || bestDy > 25) { HideHover(); return; }

        var sr = lane.Series[bestSeries];
        double xv = lane.Xs[bestIdx];
        double yv = lane.TraceTop + lane.TraceH - ((bestVal - lane.YMin) / lane.YSpan) * lane.TraceH;
        string valText = lane.Unit == "s"
            ? FormatLapTime(bestVal)
            : bestVal.ToString("0.##") + (lane.Unit != "" ? " " + lane.Unit : "");
        string text = $"{lane.LaneName} {sr.Name}\nLap {lane.LapNumbers[bestIdx]}    {valText}";
        ShowHover(xv, yv, sr.Color, text);
    }             
    private void RedrawStint()
    {
        if (_activeTab != "stint") return;

        CursorSlider.Visibility = Visibility.Collapsed;
        TrackMapBorder.Visibility = Visibility.Collapsed;
        DrawStintChart();
    } 
 
    private void ComputeLapStats()
    {
        _lapStats.Clear();
        if (_allPoints == null || _allPoints.Count == 0 || _lapInfos == null) return;

        foreach (var lap in _lapInfos)
        {
            var pts = _allPoints.Where(p => p.Time >= lap.StartTime && p.Time < lap.EndTime).ToList();
            if (pts.Count < 5) continue;

            _lapStats.Add(new LapStats
            {
                LapNumber = lap.LapNumber,
                LapTime = lap.LapTime,
                AvgSpeed = pts.Average(p => p.Speed),
                MaxSpeed = pts.Max(p => p.Speed),
                TyreTempFL = pts.Average(p => p.TyreTempFL),
                TyreTempFR = pts.Average(p => p.TyreTempFR),
                TyreTempRL = pts.Average(p => p.TyreTempRL),
                TyreTempRR = pts.Average(p => p.TyreTempRR),
                TyrePressFL = pts.Average(p => p.TyrePressFL),
                TyrePressFR = pts.Average(p => p.TyrePressFR),
                TyrePressRL = pts.Average(p => p.TyrePressRL),
                TyrePressRR = pts.Average(p => p.TyrePressRR),
                TyreWearFL = pts[^1].TyreWearFL,
                TyreWearFR = pts[^1].TyreWearFR,
                TyreWearRL = pts[^1].TyreWearRL,
                TyreWearRR = pts[^1].TyreWearRR,
                SoCStart = pts[0].SoC,
                SoCEnd = pts[^1].SoC,
                BrakeTempFL = pts.Average(p => p.BrakeTempFL),
                BrakeTempFR = pts.Average(p => p.BrakeTempFR),
                BrakeTempRL = pts.Average(p => p.BrakeTempRL),
                BrakeTempRR = pts.Average(p => p.BrakeTempRR),
                BrakeWearFL = pts.Average(p => p.BrakeWearFL),
                BrakeWearFR = pts.Average(p => p.BrakeWearFR),
                BrakeWearRL = pts.Average(p => p.BrakeWearRL),
                BrakeWearRR = pts.Average(p => p.BrakeWearRR),
            });
        }
    }

    private static double Median(List<double> v)
    {
        var s = v.OrderBy(x => x).ToList();
        if (s.Count == 0) return 0;
        int mid = s.Count / 2;
        return s.Count % 2 == 0 ? (s[mid - 1] + s[mid]) / 2 : s[mid];
    }
}