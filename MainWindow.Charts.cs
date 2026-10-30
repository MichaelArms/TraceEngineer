using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;   // ← Line, Rectangle, Polyline, Ellipse, Canvas, TextBlock
using System.Windows.Shapes;
using System.Windows.Media;       // ← Color, SolidColorBrush, Brush
using TraceEngineer.Models;
namespace TraceEngineer;

public partial class MainWindow : Window
{
    
    private void DrawChart(List<ComparisonEntry> entries)
    {
        if (ChartCanvas is null) return;

        ChartCanvas.Children.Clear();
        _hoverLanes = new List<HoverLane>();
        _hoverCompound = new List<HoverCompound>();
        _hoverEntries = entries ?? new List<ComparisonEntry>();
        if (_activeTab == "stint")
        {
            CursorSlider.Visibility = Visibility.Collapsed;
            TrackMapBorder.Visibility = Visibility.Collapsed;
            DrawStintChart();
            return;
        }    

        double W = ChartCanvas.ActualWidth;
        double H = ChartCanvas.ActualHeight;
        if (W < 80 || H < 80) return;

        if (entries == null || entries.Count == 0)
        {
            Center(W, H, "Select laps from the session or from the saved library", 14, "#FFFFFF");
            return;
        }

        bool useDistance = RbDistance.IsChecked == true;
        // Stile MoTeC: in confronto (2 giri) un colore unico per giro su tutte le corsie.
        // Coppia colorblind-safe ad alto contrasto su sfondo scuro.
        bool compareMode = entries.Count == 2;
        

        var lanes = new List<Lane>();
        var compoundWheels = new List<int>();

        if (_activeTab == "car")
        {
            if (ChkSpeed.IsChecked == true)
                lanes.Add(new Lane("Speed", "km/h", p => p.Speed, Color.FromRgb(255, 255, 255)));
            if (ChkThrottle.IsChecked == true)
                lanes.Add(new Lane("Throttle", "%", p => p.Throttle * 100.0, Color.FromRgb(0, 214, 255)));
            if (ChkBrake.IsChecked == true)
                lanes.Add(new Lane("Brake", "%", p => p.Brake * 100.0, Color.FromRgb(255, 82, 82)));
            if (ChkSteering.IsChecked == true)
                lanes.Add(new Lane("Steering", "raw", p => p.Steering, Color.FromRgb(150, 255, 130)));
            if (ChkRpm.IsChecked == true)
                lanes.Add(new Lane("Engine RPM", "rpm", p => p.RPM, Color.FromRgb(255, 205, 60)));
            if (ChkSoC.IsChecked == true)
                lanes.Add(new Lane("SoC", "%", p => p.SoC, Color.FromRgb(186, 104, 255)));
            if (ChkErs.IsChecked == true)
                lanes.Add(new Lane("ERS power", "%/s", p => p.ErsPower, Color.FromRgb(0, 230, 118)));
        }
        else if (_activeTab == "tyres")
        {
            compoundWheels = new List<int> { 0, 1, 2, 3 };
        }
        else // brakes
        {
            lanes.Add(WheelLane("Brake temp", "°C",
                p => p.BrakeTempFL, p => p.BrakeTempFR, p => p.BrakeTempRL, p => p.BrakeTempRR));
            lanes.Add(WheelLane("Brake wear", "mm",
                p => p.BrakeWearFL, p => p.BrakeWearFR, p => p.BrakeWearRL, p => p.BrakeWearRR));
        }


        if (lanes.Count == 0 && compoundWheels.Count == 0)
        {
            Center(W, H, "Select at least one channel", 14, "#FFFFFF");
            return;
        }

        // Delta: solo in modalità distanza con esattamente 2 giri
        var deltaData = new List<(double X, double Y)>();
        bool showDelta = useDistance && entries.Count == 2;
        if (showDelta)
        {
            deltaData = ComputeDelta(entries[0].Points, entries[1].Points);
            if (deltaData.Count < 2) showDelta = false;
        }

        double plotW = W - LeftPad - RightPad;
        double plotH = H - TopPad - BottomPad;
        if (plotW < 30 || plotH < 30) return;

        int totalLanes = lanes.Count + 2 * compoundWheels.Count + (showDelta ? 1 : 0);
        double laneH = plotH / totalLanes;
        int laneOffset = showDelta ? 1 : 0;   // il delta occupa la corsia 0

        // Asse X comune (tempo o distanza)
        Func<TelemetryPoint, double> selX = useDistance ? (p => p.Distance) : (p => p.Time);
        _hoverSelX = selX;
        _hoverCompare = compareMode;
        double xMax = 0;
        foreach (var e in entries)
        {
            if (e.Points.Count == 0) continue;
            double last = selX(e.Points[^1]);
            if (last > xMax) xMax = last;
        }
        double xSpan = Math.Max(1e-6, xMax);

        // ---- Sfondo corsie ----
        for (int i = 0; i < totalLanes; i++)
        {
            var bg = new Rectangle
            {
                Width = plotW,
                Height = Math.Max(1, laneH - 3),
                Fill = new SolidColorBrush(i % 2 == 0 ? Color.FromRgb(37, 37, 40) : Color.FromRgb(31, 31, 34)),
                Stroke = new SolidColorBrush(Color.FromRgb(58, 58, 62)),
                StrokeThickness = 1
            };
            Canvas.SetLeft(bg, LeftPad);
            Canvas.SetTop(bg, TopPad + i * laneH);
            ChartCanvas.Children.Add(bg);
        }

        // ---- Griglia asse X ----
        for (int s = 0; s <= 10; s++)
        {
            double x = LeftPad + plotW * s / 10.0;
            var grid = new Line
            {
                X1 = x, Y1 = TopPad, X2 = x, Y2 = TopPad + plotH,
                Stroke = new SolidColorBrush(Color.FromRgb(50, 50, 54)),
                StrokeThickness = 1
            };
            ChartCanvas.Children.Add(grid);

            double xVal = xSpan * s / 10.0;
            string lab = useDistance ? $"{xVal:F0} m" : $"{xVal:F1} s";
            var labEl = Label(lab, 10, "#FFFFFF");
            Canvas.SetLeft(labEl, x - 20);
            Canvas.SetTop(labEl, TopPad + plotH + 6);
            ChartCanvas.Children.Add(labEl);
        }

        // ---- CORSIA DELTA (in alto, se attiva) ----
        if (showDelta)
        {
            double dTop = TopPad;
            double traceTop = dTop + 4;
            double traceH = Math.Max(6, laneH - 11);

            double dMin = deltaData.Min(p => p.Y);
            double dMax = deltaData.Max(p => p.Y);
            dMin = Math.Min(dMin, 0);   // forza lo zero visibile
            dMax = Math.Max(dMax, 0);
            double dSpan = dMax - dMin;
            if (dSpan < 1e-9) { dMin -= 0.5; dSpan = 1.0; }
            _hoverLanes.Add(new HoverLane
            {
                IsDelta = true,
                Delta = deltaData,
                TraceTop = traceTop,
                TraceH = traceH,
                VMin = dMin,
                VSpan = dSpan
            });

            // linea dello zero
            double zeroY = traceTop + traceH - ((0 - dMin) / dSpan) * traceH;
            var zeroLine = new Line
            {
                X1 = LeftPad, Y1 = zeroY, X2 = LeftPad + plotW, Y2 = zeroY,
                Stroke = new SolidColorBrush(Color.FromRgb(110, 110, 116)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 3 }
            };
            ChartCanvas.Children.Add(zeroLine);

            // traccia delta
            var poly = new Polyline
            {
                Points = BuildTraceXY(deltaData, 0, xSpan, dMin, dSpan, plotW, traceH, 2000),
                Stroke = new SolidColorBrush(Color.FromRgb(255, 77, 210)),
                StrokeThickness = 1.1,
                RenderTransform = new TranslateTransform(LeftPad, traceTop)
            };
            ChartCanvas.Children.Add(poly);

            // etichetta con gap finale
            double finalGap = deltaData[^1].Y;
            string gapTxt = finalGap >= 0 ? $"+{finalGap:F3}s" : $"{finalGap:F3}s";
            var name = Label($"Δ Time (lap2 − lap1)   final gap: {gapTxt}", 11, Color.FromRgb(255, 77, 210));
            name.FontWeight = FontWeights.SemiBold;
            Canvas.SetLeft(name, 5);
            Canvas.SetTop(name, dTop + 3);
            ChartCanvas.Children.Add(name);

            var unit = Label("s", 9, "#FFFFFF");
            Canvas.SetLeft(unit, 5);
            Canvas.SetTop(unit, dTop + 18);
            ChartCanvas.Children.Add(unit);
        }

        // ---- TRACCE DEI CANALI ----
        for (int i = 0; i < lanes.Count; i++)
        {
            var lane = lanes[i];
            double laneTop = TopPad + (laneOffset + i) * laneH;
            double traceTop = laneTop + 4;
            double traceH = Math.Max(6, laneH - 11);

            // Scala condivisa su tutta la corsia
            double vMin = double.MaxValue, vMax = double.MinValue;
            if (lane.Wheel != null)
            {
                foreach (var e in entries)
                    foreach (var p in e.Points)
                        foreach (var ws in lane.Wheel)
                        {
                            double v = ws.Sel(p);
                            if (v < vMin) vMin = v;
                            if (v > vMax) vMax = v;
                        }
            }
            else
            {
                foreach (var e in entries)
                    foreach (var p in e.Points)
                    {
                        double v = lane.Sel(p);
                        if (v < vMin) vMin = v;
                        if (v > vMax) vMax = v;
                    }
            }
            double vSpan = vMax - vMin;
            if (vSpan < 1e-9) { vMin -= 0.5; vSpan = 1.0; }
            // Unità rilevata dall'ordine di grandezza dei valori loggati
            if (lane.Name == "Tyre pressure")
            {
                lane.Unit = vMax < 10 ? "bar" : (vMax < 100 ? "psi" : "kPa");
            }
            _hoverLanes.Add(new HoverLane
            {
                Lane = lane,
                TraceTop = traceTop,
                TraceH = traceH,
                VMin = vMin,
                VSpan = vSpan
            });

            if (lane.Wheel != null)
            {
                // Una serie per giro per ruota: in confronto i colori sono quelli
                // del giro (ciano/arancio), in singolo quelli della ruota.
                for (int gi = 0; gi < entries.Count; gi++)
                {
                    var entry = entries[gi];
                    int buckets = entry.Points.Count <= 15000 ? entry.Points.Count : 6000;

                    for (int w = 0; w < lane.Wheel.Count; w++)
                    {
                        var ws = lane.Wheel[w];
                        var col = compareMode ? LapColors[gi] : WheelKey[w].Color;
                        var brush = new SolidColorBrush(Color.FromArgb(200, col.R, col.G, col.B));

                        var poly = new Polyline
                        {
                            Points = BuildTrace(entry.Points, selX, ws.Sel, 0, xSpan, vMin, vSpan, plotW, traceH, buckets),
                            Stroke = brush,
                            StrokeThickness = 1.0,
                            RenderTransform = new TranslateTransform(LeftPad, traceTop)
                        };
                        ChartCanvas.Children.Add(poly);

                        // Etichetta ruota all'inizio della linea
                        double v0 = ws.Sel(entry.Points[0]);
                        double y0 = traceTop + traceH - ((v0 - vMin) / vSpan) * traceH;
                        var tag = Label(ws.Name, 9, col);
                        Canvas.SetLeft(tag, LeftPad + 2);
                        Canvas.SetTop(tag, gi == 0 ? y0 - 11 : y0 + 2);
                        ChartCanvas.Children.Add(tag);
                    }
                }
            }
            else
            {
                for (int gi = 0; gi < entries.Count; gi++)
                {
                    var entry = entries[gi];
                    var baseCol = compareMode ? LapColors[gi] : lane.Color;
                    int buckets = entry.Points.Count <= 15000 ? entry.Points.Count : 6000;
                    var poly = new Polyline
                    {
                        Points = BuildTrace(entry.Points, selX, lane.Sel, 0, xSpan, vMin, vSpan, plotW, traceH, buckets),
                        Stroke = new SolidColorBrush(baseCol),
                        StrokeThickness = 1.0,
                        RenderTransform = new TranslateTransform(LeftPad, traceTop)
                    };
                    ChartCanvas.Children.Add(poly);
                }
            }

            // Scala numerica a sinistra per le corsie di temperatura
            if (lane.Unit == "°C")
            {
                double[] ticks = { vMax, (vMax + vMin) / 2, vMin };
                foreach (var tv in ticks)
                {
                    double ty = traceTop + traceH - ((tv - vMin) / vSpan) * traceH;
                    var t = Label(tv.ToString("0"), 9, Color.FromRgb(150, 150, 155));
                    Canvas.SetLeft(t, LeftPad + 4);
                    Canvas.SetTop(t, ty - 6);
                    ChartCanvas.Children.Add(t);
                }
            }
        }

        // ---- ETICHETTE CORSIE CANALI ----
        for (int i = 0; i < lanes.Count; i++)
        {
            var lane = lanes[i];
            double laneTop = TopPad + (laneOffset + i) * laneH;

            var name = Label(lane.Name, 11, compareMode ? Color.FromRgb(200, 200, 206) : lane.Color);
            name.FontWeight = FontWeights.SemiBold;
            Canvas.SetLeft(name, 5);
            Canvas.SetTop(name, laneTop + 3);
            ChartCanvas.Children.Add(name);

            var unit = Label(lane.Unit, 9, "#FFFFFF");
            Canvas.SetLeft(unit, 5);
            Canvas.SetTop(unit, laneTop + 18);
            ChartCanvas.Children.Add(unit);
        }

        // ---- COMPOUND LANES (una per ruota) ----
        int slotStart = laneOffset + lanes.Count;
        for (int j = 0; j < compoundWheels.Count; j++)
        {
            double top = TopPad + (slotStart + 2 * j) * laneH;
            DrawCompoundLane(compoundWheels[j], entries, compareMode, top, laneH,
                            plotW, selX, xSpan);
        }

        // ---- LEGENDA ----
        double legendY = TopPad + plotH + 26;
        double lx = LeftPad;

        if (compareMode)
        {
            for (int gi = 0; gi < entries.Count; gi++)
            {
                var col = LapColors[gi];
                var sw = new Line
                {
                    X1 = lx, Y1 = legendY + 7, X2 = lx + 30, Y2 = legendY + 7,
                    Stroke = new SolidColorBrush(col), StrokeThickness = 1.0
                };
                ChartCanvas.Children.Add(sw);

                var txt = Label(entries[gi].Label, 11, col);
                Canvas.SetLeft(txt, lx + 36);
                Canvas.SetTop(txt, legendY);
                ChartCanvas.Children.Add(txt);

                lx += 36 + entries[gi].Label.Length * 6.3 + 28;
            }
        }

        // Chiave ruote solo in singolo: in confronto il colore identifica il giro
        // e le etichette a inizio riga identificano la ruota.
        if (!compareMode && lanes.Any(l => l.Wheel != null))
        {
            for (int w = 0; w < 4; w++)
            {
                var sw2 = new Line
                {
                    X1 = lx, Y1 = legendY + 7, X2 = lx + 18, Y2 = legendY + 7,
                    Stroke = new SolidColorBrush(WheelKey[w].Color), StrokeThickness = 1.0
                };
                ChartCanvas.Children.Add(sw2);

                var t2 = Label(WheelKey[w].Name, 11, WheelKey[w].Color);
                Canvas.SetLeft(t2, lx + 22);
                Canvas.SetTop(t2, legendY);
                ChartCanvas.Children.Add(t2);

                lx += 22 + WheelKey[w].Name.Length * 6.3 + 24;
            }
        }

        // Nota sulla sovrapposizione delle tracce per-ruota
        if (lanes.Any(l => l.Wheel != null))
        {
            var note = Label("Overlapping wheel traces blend: higher saturation = coincident lines",
                            10, Color.FromRgb(140, 140, 145));
            Canvas.SetLeft(note, lx + 10);
            Canvas.SetTop(note, legendY);
            ChartCanvas.Children.Add(note);
        }


        // ---- CURSORE VERTICALE ----
        _plotW = plotW;
        _plotH = plotH;
        _xSpan = xSpan;
        _axisDistance = RbDistance.IsChecked == true;

        if (entries.Count > 0)
        {
            _cursorLine = new Line
            {
                Stroke = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                StrokeThickness = 0.6
            };
            ChartCanvas.Children.Add(_cursorLine);

            _cursorLabel = Label("", 10, Color.FromRgb(230, 230, 235));
            ChartCanvas.Children.Add(_cursorLabel);

            UpdateCursorVisual();
        }
        else
        {
            _cursorLine = null;
            _cursorLabel = null;
        }

    }


    private PointCollection BuildTrace(
        List<TelemetryPoint> points,
        Func<TelemetryPoint, double> selX,
        Func<TelemetryPoint, double> selY,
        double xMin, double xSpan,
        double vMin, double vSpan,
        double width, double height,
        int buckets)
    {
        int n = points.Count;
        var pc = new PointCollection(Math.Min(n, buckets * 2) + 4);

        double MapX(double x) => (x - xMin) / xSpan * width;
        double MapY(double v) => height - ((v - vMin) / vSpan) * height;

        if (n <= buckets)
        {
            foreach (var p in points)
                pc.Add(new Point(MapX(selX(p)), MapY(selY(p))));
            return pc;
        }

        double step = (double)n / buckets;
        for (int b = 0; b < buckets; b++)
        {
            int i0 = (int)(b * step);
            int i1 = (int)((b + 1) * step);
            if (i1 > n) i1 = n;
            if (i1 <= i0) continue;

            int iLo = i0, iHi = i0;
            double vLo = selY(points[i0]), vHi = vLo;

            for (int i = i0 + 1; i < i1; i++)
            {
                double v = selY(points[i]);
                if (v < vLo) { vLo = v; iLo = i; }
                if (v > vHi) { vHi = v; iHi = i; }
            }

            if (iLo <= iHi)
            {
                pc.Add(new Point(MapX(selX(points[iLo])), MapY(vLo)));
                pc.Add(new Point(MapX(selX(points[iHi])), MapY(vHi)));
            }
            else
            {
                pc.Add(new Point(MapX(selX(points[iHi])), MapY(vHi)));
                pc.Add(new Point(MapX(selX(points[iLo])), MapY(vLo)));
            }
        }
        return pc;
    }

    // Variante generica per il delta (lista di coppie X,Y)
    private PointCollection BuildTraceXY(
        IReadOnlyList<(double X, double Y)> data,
        double xMin, double xSpan,
        double yMin, double ySpan,
        double width, double height,
        int buckets)
    {
        int n = data.Count;
        var pc = new PointCollection(Math.Min(n, buckets * 2) + 4);

        double MapX(double x) => (x - xMin) / xSpan * width;
        double MapY(double y) => height - ((y - yMin) / ySpan) * height;

        if (n <= buckets)
        {
            foreach (var p in data)
                pc.Add(new Point(MapX(p.X), MapY(p.Y)));
            return pc;
        }

        double step = (double)n / buckets;
        for (int b = 0; b < buckets; b++)
        {
            int i0 = (int)(b * step);
            int i1 = (int)((b + 1) * step);
            if (i1 > n) i1 = n;
            if (i1 <= i0) continue;

            int iLo = i0, iHi = i0;
            double vLo = data[i0].Y, vHi = vLo;

            for (int i = i0 + 1; i < i1; i++)
            {
                double v = data[i].Y;
                if (v < vLo) { vLo = v; iLo = i; }
                if (v > vHi) { vHi = v; iHi = i; }
            }

            if (iLo <= iHi)
            {
                pc.Add(new Point(MapX(data[iLo].X), MapY(vLo)));
                pc.Add(new Point(MapX(data[iHi].X), MapY(vHi)));
            }
            else
            {
                pc.Add(new Point(MapX(data[iHi].X), MapY(vHi)));
                pc.Add(new Point(MapX(data[iLo].X), MapY(vLo)));
            }
        }
        return pc;
    }    
    // Calcola la distanza percorsa integrando la velocità (m/s) sul tempo
    private static void ComputeDistance(List<TelemetryPoint> pts)
    {
        if (pts.Count == 0) return;
        pts[0].Distance = 0;
        for (int i = 1; i < pts.Count; i++)
        {
            double dt = pts[i].Time - pts[i - 1].Time;
            if (dt < 0) dt = 0;
            double avgSpeed = (pts[i - 1].Speed + pts[i].Speed) / 2.0 / 3.6; // speed è in km/h, converti in m/s
            pts[i].Distance = pts[i - 1].Distance + avgSpeed * dt;
        }
    }

    // Filtro a mediana su finestra temporale: rimuove le escursioni brevi
    // (cut di cambiata, blip di doppietta) preservando rampe e plateau.
    // Solo visivo: CSV e salvataggi restano grezzi.
    private static void MedianFilter(
        List<TelemetryPoint> pts,
        Func<TelemetryPoint, double> get,
        Action<TelemetryPoint, double> set,
        double windowSec)
    {
        int n = pts.Count;
        if (n < 3) return;

        double dur = pts[n - 1].Time - pts[0].Time;
        if (dur <= 0) return;

        int rate = Math.Max(1, (int)Math.Round(n / dur));
        int half = Math.Max(1, (int)Math.Round(windowSec * rate) / 2);

        var src = new double[n];
        for (int i = 0; i < n; i++) src[i] = get(pts[i]);

        var dst = new double[n];
        var buf = new double[2 * half + 1];

        for (int i = 0; i < n; i++)
        {
            int a = Math.Max(0, i - half);
            int b = Math.Min(n - 1, i + half);
            int m = b - a + 1;

            for (int k = 0; k < m; k++) buf[k] = src[a + k];
            Array.Sort(buf, 0, m);
            dst[i] = buf[m / 2];
        }

        for (int i = 0; i < n; i++) set(pts[i], dst[i]);
    }

    // Interpola il tempo a una data distanza (pts ordinati per Distance crescente)
    private static double InterpTimeAtDistance(List<TelemetryPoint> pts, double d, ref int idx)
    {
        while (idx < pts.Count - 1 && pts[idx + 1].Distance < d) idx++;
        if (idx >= pts.Count - 1) return double.NaN;

        var p0 = pts[idx];
        var p1 = pts[idx + 1];
        double span = p1.Distance - p0.Distance;
        if (span < 1e-9) return p0.Time;
        double frac = (d - p0.Distance) / span;
        return p0.Time + frac * (p1.Time - p0.Time);
    }

    // Calcola il delta temporale tra due giri in funzione della distanza
    private static List<(double X, double Y)> ComputeDelta(List<TelemetryPoint> refLap, List<TelemetryPoint> cmpLap, int numPoints = 1200)
    {
        var result = new List<(double, double)>();
        if (refLap.Count < 2 || cmpLap.Count < 2) return result;

        double refLen = refLap[^1].Distance;
        double cmpLen = cmpLap[^1].Distance;
        double maxDist = Math.Min(refLen, cmpLen);
        if (maxDist <= 1) return result;

        int iRef = 0, iCmp = 0;
        for (int k = 0; k <= numPoints; k++)
        {
            double d = maxDist * k / numPoints;
            double tRef = InterpTimeAtDistance(refLap, d, ref iRef);
            double tCmp = InterpTimeAtDistance(cmpLap, d, ref iCmp);
            if (double.IsNaN(tRef) || double.IsNaN(tCmp)) continue;
            result.Add((d, tCmp - tRef));   // positivo = giro2 più lento
        }
        return result;
    }

    private static int IndexAtX(List<TelemetryPoint> pts, Func<TelemetryPoint, double> selX, double x)
    {
        if (pts.Count == 0) return -1;
        int lo = 0, hi = pts.Count - 1;
        if (x <= selX(pts[lo])) return lo;
        if (x >= selX(pts[hi])) return hi;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (selX(pts[mid]) < x) lo = mid; else hi = mid;
        }
        return (x - selX(pts[lo]) < selX(pts[hi]) - x) ? lo : hi;
    }

 
    private static int IndexAtDeltaX(List<(double X, double Y)> data, double x)
    {
        int lo = 0, hi = data.Count - 1;
        if (x <= data[lo].X) return lo;
        if (x >= data[hi].X) return hi;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (data[mid].X < x) lo = mid; else hi = mid;
        }
        return (x - data[lo].X < data[hi].X - x) ? lo : hi;
    }       



}