using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Input;
using Microsoft.Win32;
using TraceEngineer.Models;
using TraceEngineer.Services;

namespace TraceEngineer;

public partial class MainWindow : Window
{
    private string? _selectedFile;
    private readonly DuckDbTelemetryReader _reader = new();
    private readonly SavedLapStore _store = new();

    private List<TelemetryPoint> _allPoints = new();
    private List<LapMarker> _allLaps = new();
    private List<LapInfo> _lapInfos = new();
    private List<(string Path, SavedLap Lap)> _savedLaps = new();
    private List<SavedLapListItem> _savedItems = new();
    private readonly List<object> _comparison = new();
    private static readonly SolidColorBrush TransparentBrush = new SolidColorBrush(Colors.Transparent);

    private double _plotW, _plotH, _xSpan;
    private bool _axisDistance;
    private string _activeTab = "car";
    private Line? _cursorLine;
    private TextBlock? _cursorLabel;
    private double _cursorFrac = 0.0;

    private sealed class HoverLane
    {
        public Lane Lane = null!;
        public bool IsDelta;
        public List<(double X, double Y)>? Delta;
        public double TraceTop;
        public double TraceH;
        public double VMin;
        public double VSpan;
    }
    
    private sealed class HoverCompound
    {
        public int WheelIndex;
        public double ATop, AH, AMin, ASpan;
        public double BTop, BH, PMin, PSpan, WMin, WSpan;
    }

    private List<HoverLane> _hoverLanes = new();
    private List<HoverCompound> _hoverCompound = new();
    private List<ComparisonEntry> _hoverEntries = new();
    private Func<TelemetryPoint, double> _hoverSelX = p => p.Time;
    private bool _hoverCompare;

    private string _currentTrackName = "";
    private string _currentSessionDate = "";
    private bool _suppressSelectionEvents = false;
    
    private static Func<TelemetryPoint, double> Carcass(int w) => w switch
    { 0 => p => p.TyreTempFL, 1 => p => p.TyreTempFR, 2 => p => p.TyreTempRL, _ => p => p.TyreTempRR };
    private static Func<TelemetryPoint, double> Inner(int w) => w switch
    { 0 => p => p.TyreInFL, 1 => p => p.TyreInFR, 2 => p => p.TyreInRL, _ => p => p.TyreInRR };
    private static Func<TelemetryPoint, double> Centre(int w) => w switch
    { 0 => p => p.TyreCtFL, 1 => p => p.TyreCtFR, 2 => p => p.TyreCtRL, _ => p => p.TyreCtRR };
    private static Func<TelemetryPoint, double> Outer(int w) => w switch
    { 0 => p => p.TyreOtFL, 1 => p => p.TyreOtFR, 2 => p => p.TyreOtRL, _ => p => p.TyreOtRR };
    private static Func<TelemetryPoint, double> Rim(int w) => w switch
    { 0 => p => p.TyreRimFL, 1 => p => p.TyreRimFR, 2 => p => p.TyreRimRL, _ => p => p.TyreRimRR };
    private static Func<TelemetryPoint, double> Press(int w) => w switch
    { 0 => p => p.TyrePressFL, 1 => p => p.TyrePressFR, 2 => p => p.TyrePressRL, _ => p => p.TyrePressRR };
    private static Func<TelemetryPoint, double> TWear(int w) => w switch
    { 0 => p => p.TyreWearFL, 1 => p => p.TyreWearFR, 2 => p => p.TyreWearRL, _ => p => p.TyreWearRR };

    private void DrawCompoundLane(int wi, List<ComparisonEntry> entries, bool compareMode,
        double top, double laneH, double plotW,
        Func<TelemetryPoint, double> selX, double xSpan)
    {
        string wname = WheelKey[wi].Name;

        // ===== PANEL A: temperature (°C) =====
        double aTop = top + 4, aH = Math.Max(6, laneH - 11);
        var tempSeries = new List<(string N, Func<TelemetryPoint, double> S, Color C, double Th, bool Dash)>();
        if (compareMode)
        {
            // solo carcass per giro
        }
        else
        {
            tempSeries.Add(("carcass", Carcass(wi), Color.FromRgb(255, 255, 255), 1.4, false));
            tempSeries.Add(("inner",  Inner(wi),  Color.FromRgb(255, 82, 82),  1.0, false));
            tempSeries.Add(("centre", Centre(wi), Color.FromRgb(255, 205, 60), 1.0, false));
            tempSeries.Add(("outer",  Outer(wi),  Color.FromRgb(150, 255, 130),1.0, false));
            tempSeries.Add(("rim",    Rim(wi),    Color.FromRgb(255, 145, 0),  1.0, true));
        }

        double aMin = double.MaxValue, aMax = double.MinValue;
        if (compareMode)
        {
            foreach (var e in entries) foreach (var p in e.Points)
            { double v = Carcass(wi)(p); if (v < aMin) aMin = v; if (v > aMax) aMax = v; }
        }
        else
        {
            foreach (var e in entries) foreach (var p in e.Points)
                foreach (var s in tempSeries)
                { double v = s.S(p); if (v < aMin) aMin = v; if (v > aMax) aMax = v; }
        }
        double aSpan = aMax - aMin;
        if (aSpan < 1e-9) { aMin -= 0.5; aSpan = 1.0; }

        if (compareMode)
        {
            for (int gi = 0; gi < entries.Count; gi++)
            {
                var poly = new Polyline
                {
                    Points = BuildTrace(entries[gi].Points, selX, Carcass(wi), 0, xSpan, aMin, aSpan, plotW, aH, 6000),
                    Stroke = new SolidColorBrush(LapColors[gi]),
                    StrokeThickness = 1.2,
                    RenderTransform = new TranslateTransform(LeftPad, aTop)
                };
                ChartCanvas.Children.Add(poly);
            }
        }
        else
        {
            foreach (var s in tempSeries)
            {
                var poly = new Polyline
                {
                    Points = BuildTrace(entries[0].Points, selX, s.S, 0, xSpan, aMin, aSpan, plotW, aH, 6000),
                    Stroke = new SolidColorBrush(s.C),
                    StrokeThickness = s.Th,
                    StrokeDashArray = s.Dash ? new DoubleCollection { 4, 3 } : null,
                    RenderTransform = new TranslateTransform(LeftPad, aTop)
                };
                ChartCanvas.Children.Add(poly);
            }
        }

        // tick °C a sinistra
        foreach (var tv in new[] { aMax, (aMax + aMin) / 2, aMin })
        {
            double ty = aTop + aH - ((tv - aMin) / aSpan) * aH;
            var t = Label(tv.ToString("0"), 9, Color.FromRgb(150, 150, 155));
            Canvas.SetLeft(t, LeftPad + 4);
            Canvas.SetTop(t, ty - 6);
            ChartCanvas.Children.Add(t);
        }

        var nameA = Label($"{wname} tyre — temp", 11, Color.FromRgb(200, 200, 206));
        nameA.FontWeight = FontWeights.SemiBold;
        Canvas.SetLeft(nameA, 5);
        Canvas.SetTop(nameA, top + 3);
        ChartCanvas.Children.Add(nameA);

        // ===== PANEL B: pressure + wear =====
        double bTop = top + laneH + 4, bH = Math.Max(6, laneH - 11);

        double pMin = double.MaxValue, pMax = double.MinValue;
        double wMin = double.MaxValue, wMax = double.MinValue;
        foreach (var e in entries) foreach (var p in e.Points)
        {
            double pv = Press(wi)(p); if (pv < pMin) pMin = pv; if (pv > pMax) pMax = pv;
            double wv = TWear(wi)(p); if (wv < wMin) wMin = wv; if (wv > wMax) wMax = wv;
        }
        double pSpan = pMax - pMin; if (pSpan < 1e-9) { pMin -= 0.5; pSpan = 1.0; }
        double wSpan = wMax - wMin; if (wSpan < 1e-9) { wMin -= 0.05; wSpan = 0.1; }

        for (int gi = 0; gi < entries.Count; gi++)
        {
            var colP = compareMode ? LapColors[gi] : Color.FromRgb(0, 214, 255);
            var colW = compareMode ? LapColors[gi] : Color.FromRgb(255, 77, 210);
            var polyP = new Polyline
            {
                Points = BuildTrace(entries[gi].Points, selX, Press(wi), 0, xSpan, pMin, pSpan, plotW, bH, 6000),
                Stroke = new SolidColorBrush(colP),
                StrokeThickness = 1.1,
                RenderTransform = new TranslateTransform(LeftPad, bTop)
            };
            ChartCanvas.Children.Add(polyP);

            var polyW = new Polyline
            {
                Points = BuildTrace(entries[gi].Points, selX, TWear(wi), 0, xSpan, wMin, wSpan, plotW, bH, 6000),
                Stroke = new SolidColorBrush(colW),
                StrokeThickness = 1.0,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                RenderTransform = new TranslateTransform(LeftPad, bTop)
            };
            ChartCanvas.Children.Add(polyW);
        }

        // tick pressure a sinistra
        foreach (var tv in new[] { pMax, (pMax + pMin) / 2, pMin })
        {
            double ty = bTop + bH - ((tv - pMin) / pSpan) * bH;
            var t = Label(tv.ToString("0.#"), 9, Color.FromRgb(150, 150, 155));
            Canvas.SetLeft(t, LeftPad + 4);
            Canvas.SetTop(t, ty - 6);
            ChartCanvas.Children.Add(t);
        }

        var nameB = Label($"{wname} tyre — press / wear", 10, Color.FromRgb(160, 160, 166));
        Canvas.SetLeft(nameB, 5);
        Canvas.SetTop(nameB, top + laneH + 3);
        ChartCanvas.Children.Add(nameB);
        _hoverCompound.Add(new HoverCompound
        {
            WheelIndex = wi,
            ATop = aTop, AH = aH, AMin = aMin, ASpan = aSpan,
            BTop = bTop, BH = bH, PMin = pMin, PSpan = pSpan, WMin = wMin, WSpan = wSpan
        });        
    }
    private static readonly Color[] LapColors =
    {
        Color.FromRgb(0, 255, 255),    // Giro 1: ciano
        Color.FromRgb(255, 145, 0)     // Giro 2: arancio
    };
    private const double LeftPad = 74, RightPad = 12, TopPad = 6, BottomPad = 48;

    public MainWindow()
    {
        InitializeComponent();
        RefreshSavedList();
    }

    private sealed class Lane
    {
        public Lane(string name, string unit, Func<TelemetryPoint, double> sel, Color color)
        { Name = name; Unit = unit; Sel = sel; Color = color; }
        public string Name { get; }
        public string Unit { get; set; }
        public Func<TelemetryPoint, double> Sel { get; }
        public Color Color { get; }
        public List<WheelSeries>? Wheel { get; init; }
    }

    private sealed class WheelSeries
    {
        public WheelSeries(string name, Func<TelemetryPoint, double> sel)
        { Name = name; Sel = sel; }
        public string Name { get; }
        public Func<TelemetryPoint, double> Sel { get; }
    }

    // Colori standard per ruota (FL/FR/RL/RR)
    private static readonly (string Name, Color Color)[] WheelKey =
    {
        ("FL", Color.FromRgb(255, 82, 82)),
        ("FR", Color.FromRgb(105, 240, 174)),
        ("RL", Color.FromRgb(68, 138, 255)),
        ("RR", Color.FromRgb(255, 215, 64)),
    };

    private static Lane WheelLane(string name, string unit,
        Func<TelemetryPoint, double> fl, Func<TelemetryPoint, double> fr,
        Func<TelemetryPoint, double> rl, Func<TelemetryPoint, double> rr)
        => new Lane(name, unit, fl, WheelKey[0].Color)
        {
            Wheel = new List<WheelSeries>
            {
                new("FL", fl), new("FR", fr), new("RL", rl), new("RR", rr)
            }
        };

    private void OpenDuckDb_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Telemetry (*.duckdb;*.db)|*.duckdb;*.db|All Files (*.*)|*.*",
            Title = "Select Telemetry File"
        };

        if (dlg.ShowDialog() == true)
        {
            _ = LoadAndAnalyzeAsync(dlg.FileName);
        }
    }

    private async Task LoadAndAnalyzeAsync(string filePath)
    {
        try
        {
            // Pulizia completa
            _comparison.Clear();
            _allPoints.Clear();
            _allLaps.Clear();
            _lapInfos.Clear();
            _suppressSelectionEvents = true;
            LapList.ItemsSource = null;
            _suppressSelectionEvents = false;
            ChartCanvas.Children.Clear();
            SessionInfo.Text = string.Empty;
            BtnSaveLap.IsEnabled = false;
            SessionHeader.Text = string.Empty;
            

            _selectedFile = filePath;
            StatusText.Text = $"Lettura: {System.IO.Path.GetFileName(filePath)}...";

            var session = await Task.Run(() => _reader.ReadSession(filePath));

            // Allineamento Throttle/Brake
            for (int i = 0; i < session.ThrottleValues.Count && i * 2 < session.Points.Count; i++)
            {
                double thr = session.ThrottleValues[i];
                double brk = i < session.BrakeValues.Count ? session.BrakeValues[i] : 0;

                session.Points[i * 2].Throttle = thr;
                session.Points[i * 2].Brake = brk;

                if (i * 2 + 1 < session.Points.Count)
                {
                    session.Points[i * 2 + 1].Throttle = thr;
                    session.Points[i * 2 + 1].Brake = brk;
                }
            }

            // Normalizza tempo
            if (session.Points.Count > 0)
            {
                double t0 = session.Points[0].Time;
                foreach (var p in session.Points) p.Time -= t0;
                foreach (var l in session.Laps) l.Time -= t0;
            }

            _allPoints = session.Points;
            _allLaps = session.Laps;
            RefreshChannelAvailability();

            // Calcola info giri
            for (int i = 0; i < _allLaps.Count; i++)
            {
                var lap = _allLaps[i];
                double startTime = lap.Time;
                double endTime = (i + 1 < _allLaps.Count) ? _allLaps[i + 1].Time : _allPoints[^1].Time;
                double lapTime = endTime - startTime;

                _lapInfos.Add(new LapInfo
                {
                    LapNumber = lap.LapNumber,
                    StartTime = startTime,
                    EndTime = endTime,
                    LapTime = lapTime
                });
            }

            LapList.ItemsSource = _lapInfos;

            // Rileva tracciato
            var meta = await Task.Run(() => _reader.ReadMetadata(filePath));
            _currentTrackName = DetectTrackName(meta, filePath);
            _currentSessionDate = DetectDate(meta, filePath);

            TrackRefText.Text = _currentTrackName != ""
                ? $"Reference track: {_currentTrackName}"
                : "Track: not detected";

            SessionHeader.Text = _currentTrackName != ""
                ? $"{_currentTrackName}  •  {_currentSessionDate}"
                : _currentSessionDate;
            SessionHeader.Text += "   •   " + DetectCarName(_reader.ReadMetadata(_selectedFile ?? ""));

            BtnSaveLap.IsEnabled = _lapInfos.Count > 0;
            RefreshSavedList();

            SessionInfo.Text = $"{_allPoints.Count:N0} campioni  |  {_lapInfos.Count} giri";
            StatusText.Text = "Session loaded. Select 1 or 2 laps.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Error";
            MessageBox.Show(ex.Message, "Error reading telemetry",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
    private void Channel_Click(object sender, RoutedEventArgs e) => ApplyComparisonState();
    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyComparisonState();
    private void UpdateComparison()
    {
        if (_suppressSelectionEvents) return;

        var entries = new List<ComparisonEntry>();
        var refTrack = _currentTrackName;

        // Giri della sessione corrente
        foreach (LapInfo li in LapList.SelectedItems)
        {
            var pts = _allPoints
                .Where(p => p.Time >= li.StartTime && p.Time <= li.EndTime)
                .Select(p => new TelemetryPoint
                {
                    Time = p.Time - li.StartTime,
                    Speed = p.Speed, Throttle = p.Throttle, Brake = p.Brake,
                    Steering = p.Steering, RPM = p.RPM
                }).ToList();

            ComputeDistance(pts);   // <-- CALCOLA LA DISTANZA PER IL GIRO DI SESSIONE
            if (ChkHideShifts.IsChecked == true)
            {
                MedianFilter(pts, p => p.Throttle * 100.0, (p, v) => p.Throttle = v / 100.0, 0.30);
                MedianFilter(pts, p => p.Brake * 100.0, (p, v) => p.Brake = v / 100.0, 0.30);
            }

            entries.Add(new ComparisonEntry
            {
                Label = $"L{li.LapNumber} • {li.LapTimeFormatted}",
                TrackName = _currentTrackName,
                CarName = "sessione corrente",
                Points = pts,
                IsSaved = false,
                SourceItem = li
            });
        }

        // Giri salvati
        var toRemove = new List<SavedLapListItem>();

        foreach (SavedLapListItem item in SavedList.SelectedItems)
        {
            var saved = _savedLaps.FirstOrDefault(x => x.Path == item.FilePath).Lap;
            if (saved == null) continue;

            // Verifica tracciato (rete di sicurezza: normalmente la lista è già filtrata)
            if (!string.IsNullOrWhiteSpace(refTrack) && !SavedLapStore.SameTrack(refTrack, saved.TrackName))
            {
                MessageBox.Show(
                    $"This saved lap is from a different track and will not be added to the comparison.\n\n" +
                    $"Reference: {refTrack}\n" +
                    $"Saved lap: {saved.TrackName}",
                    "Different track",
                    MessageBoxButton.OK, MessageBoxImage.Warning);

                toRemove.Add(item);   // lo rimuoveremo DOPO il foreach
                continue;             // non aggiungerlo al confronto
            }

            if (string.IsNullOrWhiteSpace(refTrack)) refTrack = saved.TrackName;

            var ptsSaved = saved.Points.Select(sp => new TelemetryPoint
            {
                Time = sp.T, Speed = sp.S, Throttle = sp.Th, Brake = sp.Br,
                Steering = sp.St, RPM = sp.R
            }).ToList();

            ComputeDistance(ptsSaved);

            if (ChkHideShifts.IsChecked == true)
            {
                MedianFilter(ptsSaved, p => p.Throttle * 100.0, (p, v) => p.Throttle = v / 100.0, 0.30);
                MedianFilter(ptsSaved, p => p.Brake * 100.0, (p, v) => p.Brake = v / 100.0, 0.30);
            }

            entries.Add(new ComparisonEntry
            {
                Label = $"L{saved.LapNumber} • {TimeSpan.FromSeconds(saved.LapTimeSeconds).ToString(@"hh\:mm\:ss\.fff")}",
                TrackName = saved.TrackName,
                CarName = saved.CarName,
                Points = ptsSaved,
                IsSaved = true,
                SourceItem = item
            });
        }

        // Rimuovi gli elementi non coerenti DOPO il foreach (evita il crash)
        if (toRemove.Count > 0)
        {
            _suppressSelectionEvents = true;
            foreach (var it in toRemove)
                SavedList.SelectedItems.Remove(it);
            _suppressSelectionEvents = false;
        }

        if (entries.Count > 2)
            entries = entries.Take(2).ToList();

        // Reset evidenziazioni
        foreach (var li in _lapInfos) li.RowBg = new SolidColorBrush(Colors.Transparent);
        foreach (var si in _savedItems) si.RowBg = new SolidColorBrush(Colors.Transparent);

        // Evidenzia con il colore della traccia i giri attualmente in confronto
        for (int gi = 0; gi < entries.Count; gi++)
        {
            var tint = new SolidColorBrush(Color.FromArgb(255, LapColors[gi].R, LapColors[gi].G, LapColors[gi].B));
            if (entries[gi].SourceItem is LapInfo li2) li2.RowBg = tint;
            else if (entries[gi].SourceItem is SavedLapListItem si2) si2.RowBg = tint;
        }

        LapList.Items.Refresh();
        SavedList.Items.Refresh();

        DrawChart(entries);
    }    

    private void DrawChart(List<ComparisonEntry> entries)
    {
        if (ChartCanvas is null) return;
        ChartCanvas.Children.Clear();
        _hoverLanes = new List<HoverLane>();
        _hoverCompound = new List<HoverCompound>();
        _hoverEntries = entries ?? new List<ComparisonEntry>();

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

    private static TextBlock Label(string text, double size, string hex) =>
        new() { Text = text, FontSize = size, Foreground = Brush(hex) };

    private static TextBlock Label(string text, double size, Color c) =>
        new() { Text = text, FontSize = size, Foreground = new SolidColorBrush(c) };

    private static SolidColorBrush Brush(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    private void Center(double w, double h, string text, double size, string hex)
    {
        var tb = Label(text, size, hex);
        Canvas.SetLeft(tb, w / 2 - 200);
        Canvas.SetTop(tb, h / 2 - 10);
        ChartCanvas.Children.Add(tb);
    }

    // ---------- SALVATAGGIO ----------
    private void SaveLap_Click(object sender, RoutedEventArgs e)
    {
        // Pattern matching: se SelectedItems[0] non è un LapInfo, esce subito.
        // In questo modo lapInfo è garantito non-null dal compilatore.
        var sessionLaps = _comparison.OfType<LapInfo>().ToList();
        if (sessionLaps.Count == 0)
        {
            MessageBox.Show("Click the lap you want to save first (it gets the colored bar).",
                            "No lap", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (sessionLaps.Count > 1)
        {
            MessageBox.Show("Keep only one session lap in the comparison to save it.",
                            "Too many laps", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var lapInfo = sessionLaps[0];

        // Estrai i punti del giro e normalizzali a tempo 0
        var lapPoints = _allPoints
            .Where(p => p.Time >= lapInfo.StartTime && p.Time <= lapInfo.EndTime)
            .Select(p => new TelemetryPoint
            {
                Time = p.Time - lapInfo.StartTime,
                Speed = p.Speed, Throttle = p.Throttle, Brake = p.Brake,
                Steering = p.Steering, RPM = p.RPM,
                SoC = p.SoC,
                ErsPower = p.ErsPower,
                TyreTempFL = p.TyreTempFL, TyreTempFR = p.TyreTempFR,
                TyreTempRL = p.TyreTempRL, TyreTempRR = p.TyreTempRR,
                TyrePressFL = p.TyrePressFL, TyrePressFR = p.TyrePressFR,
                TyrePressRL = p.TyrePressRL, TyrePressRR = p.TyrePressRR,
                TyreWearFL = p.TyreWearFL, TyreWearFR = p.TyreWearFR,
                TyreWearRL = p.TyreWearRL, TyreWearRR = p.TyreWearRR,
                BrakeWearFL = p.BrakeWearFL, BrakeWearFR = p.BrakeWearFR,
                BrakeWearRL = p.BrakeWearRL, BrakeWearRR = p.BrakeWearRR,
                BrakeTempFL = p.BrakeTempFL, BrakeTempFR = p.BrakeTempFR,
                BrakeTempRL = p.BrakeTempRL, BrakeTempRR = p.BrakeTempRR
            })
            .ToList();

        if (lapPoints.Count < 2)
        {
            MessageBox.Show("The selected lap does not have enough data.", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var filePath = _selectedFile ?? "";

        var meta = _reader.ReadMetadata(filePath);
        var dlg = new SaveLapDialog(
            track: _currentTrackName,
            car: DetectCarName(meta),
            carClass: "",
            date: DetectDate(meta, filePath),
            lapInfo: $"Lap {lapInfo.LapNumber}   •   " + TimeSpan.FromSeconds(lapInfo.LapTime).ToString(@"hh\:mm\:ss\.fff")
        );

        if (dlg.ShowDialog() != true || !dlg.Confirmed) return;

        var saved = new SavedLap
        {
            TrackName = dlg.TrackName,
            CarName = dlg.CarName,
            CarClass = dlg.CarClass,
            LapNumber = lapInfo.LapNumber,
            LapTimeSeconds = lapInfo.LapTime,
            LapDateIso = dlg.LapDate,
            SavedAtIso = DateTime.Now.ToString("s"),
            Note = dlg.Note,
            Points = DownsampleForSave(lapPoints, 4000)
        };

        try
        {
            var path = _store.Save(saved);
            RefreshSavedList();
            StatusText.Text = $"Lap saved: {System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error saving lap", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteSavedLap_Click(object sender, RoutedEventArgs e)
    {
        var toDelete = _comparison.OfType<SavedLapListItem>().ToList();
        if (toDelete.Count == 0)
        {
            MessageBox.Show("Click the saved lap(s) you want to delete first (they get the colored bar).",
                            "No selection",
                            MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        int count = toDelete.Count;
        string message = count == 1
            ? "Are you sure you want to delete the selected saved lap?\nThis action cannot be undone."
            : $"Are you sure you want to delete {count} saved laps?\nThis action cannot be undone.";

        var confirm = MessageBox.Show(message, "Confirm deletion",
                                    MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        int deleted = 0;
        var errors = new List<string>();

        foreach (var item in toDelete)
        {
            try
            {
                if (System.IO.File.Exists(item.FilePath))
                {
                    System.IO.File.Delete(item.FilePath);
                    deleted++;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{System.IO.Path.GetFileName(item.FilePath)}: {ex.Message}");
            }
        }

        // Aggiorna la lista e il grafico
        RefreshSavedList();
        //UpdateComparison();

        StatusText.Text = errors.Count == 0
            ? $"Deleted {deleted} saved lap(s)."
            : $"Deleted {deleted} lap(s), {errors.Count} error(s).";

        if (errors.Count > 0)
        {
            MessageBox.Show("Some files could not be deleted:\n\n" + string.Join("\n", errors),
                            "Deletion errors",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }


    private static List<SavedPoint> DownsampleForSave(List<TelemetryPoint> pts, int target)
    {
        var result = new List<SavedPoint>();
        if (pts.Count == 0) return result;

        int step = Math.Max(1, pts.Count / target);
        for (int i = 0; i < pts.Count; i += step)
        {
            var p = pts[i];
            result.Add(new SavedPoint
            {
                T = p.Time, 
                S = p.Speed, 
                Th = p.Throttle, 
                Br = p.Brake, 
                St = p.Steering, 
                R = p.RPM,
                SoC = p.SoC,
                Ers = p.ErsPower,
                Tt1 = p.TyreTempFL, 
                Tt2 = p.TyreTempFR, 
                Tt3 = p.TyreTempRL, 
                Tt4 = p.TyreTempRR,
                Tp1 = p.TyrePressFL,
                Tp2 = p.TyrePressFR, 
                Tp3 = p.TyrePressRL, 
                Tp4 = p.TyrePressRR,
                Tw1 = p.TyreWearFL, 
                Tw2 = p.TyreWearFR, 
                Tw3 = p.TyreWearRL, 
                Tw4 = p.TyreWearRR,
                Bt1 = p.BrakeTempFL, 
                Bt2 = p.BrakeTempFR, 
                Bt3 = p.BrakeTempRL, 
                Bt4 = p.BrakeTempRR,
                Bw1 = p.BrakeWearFL, 
                Bw2 = p.BrakeWearFR, 
                Bw3 = p.BrakeWearRL, 
                Bw4 = p.BrakeWearRR,
                Rt1 = p.TyreRimFL, Rt2 = p.TyreRimFR, Rt3 = p.TyreRimRL, Rt4 = p.TyreRimRR,
                Zi1 = p.TyreInFL, Zi2 = p.TyreInFR, Zi3 = p.TyreInRL, Zi4 = p.TyreInRR,
                Zc1 = p.TyreCtFL, Zc2 = p.TyreCtFR, Zc3 = p.TyreCtRL, Zc4 = p.TyreCtRR,
                Zo1 = p.TyreOtFL, Zo2 = p.TyreOtFR, Zo3 = p.TyreOtRL, Zo4 = p.TyreOtRR,                
            });
        }

        var last = pts[^1];
        if (result.Count == 0 || Math.Abs(result[^1].T - last.Time) > 1e-6)
            result.Add(new SavedPoint { 
                T = last.Time, 
                S = last.Speed, 
                Th = last.Throttle, 
                Br = last.Brake, 
                St = last.Steering, 
                R = last.RPM,
                SoC = last.SoC,
                Ers = last.ErsPower,
                Tt1 = last.TyreTempFL, 
                Tt2 = last.TyreTempFR, 
                Tt3 = last.TyreTempRL, 
                Tt4 = last.TyreTempRR,
                Tp1 = last.TyrePressFL,
                Tp2 = last.TyrePressFR, 
                Tp3 = last.TyrePressRL, 
                Tp4 = last.TyrePressRR,
                Tw1 = last.TyreWearFL, 
                Tw2 = last.TyreWearFR, 
                Tw3 = last.TyreWearRL, 
                Tw4 = last.TyreWearRR,
                Bt1 = last.BrakeTempFL, 
                Bt2 = last.BrakeTempFR, 
                Bt3 = last.BrakeTempRL, 
                Bt4 = last.BrakeTempRR,
                Bw1 = last.BrakeWearFL, 
                Bw2 = last.BrakeWearFR, 
                Bw3 = last.BrakeWearRL, 
                Bw4 = last.BrakeWearRR, 
                Rt1 = last.TyreRimFL, Rt2 = last.TyreRimFR, Rt3 = last.TyreRimRL, Rt4 = last.TyreRimRR,
                Zi1 = last.TyreInFL, Zi2 = last.TyreInFR, Zi3 = last.TyreInRL, Zi4 = last.TyreInRR,
                Zc1 = last.TyreCtFL, Zc2 = last.TyreCtFR, Zc3 = last.TyreCtRL, Zc4 = last.TyreCtRR,
                Zo1 = last.TyreOtFL, Zo2 = last.TyreOtFR, Zo3 = last.TyreOtRL, Zo4 = last.TyreOtRR,
            });

        return result;
    }

    // ---------- HELPERS ----------
    private static string DetectTrackName(Dictionary<string, string> meta, string filePath)
    {
        foreach (var kv in meta)
        {
            var k = kv.Key.ToLowerInvariant();
            if (k.Contains("track") || k.Contains("circuit") || k.Contains("venue") || k.Contains("map"))
                if (!string.IsNullOrWhiteSpace(kv.Value)) return kv.Value.Trim();
        }

        var name = System.IO.Path.GetFileNameWithoutExtension(filePath);
        var idx = name.LastIndexOf('_');
        if (idx > 0)
        {
            var head = name.Substring(0, idx);
            var idx2 = head.LastIndexOf('_');
            if (idx2 > 0) head = head.Substring(0, idx2);
            return head.Replace('_', ' ').Trim();
        }
        return name;
    }

    private static string DetectCarName(Dictionary<string, string> meta)
    {
        foreach (var kv in meta)
        {
            var k = kv.Key.ToLowerInvariant();
            if (k.Contains("vehicle") || k.Contains("car"))
                if (!string.IsNullOrWhiteSpace(kv.Value)) return kv.Value.Trim();
        }
        return "";
    }

    private static string DetectDate(Dictionary<string, string> meta, string filePath)
    {
        foreach (var kv in meta)
        {
            var k = kv.Key.ToLowerInvariant();
            if (k.Contains("date") || k.Contains("time"))
                if (!string.IsNullOrWhiteSpace(kv.Value)) return kv.Value.Trim();
        }
        var m = System.Text.RegularExpressions.Regex.Match(filePath, @"\d{4}-\d{2}-\d{2}");
        return m.Success ? m.Value : DateTime.Now.ToString("yyyy-MM-dd");
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

    private void Axis_Click(object sender, RoutedEventArgs e) => ApplyComparisonState();

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        _activeTab = RbTabCar.IsChecked == true ? "car"
                   : RbTabTyres.IsChecked == true ? "tyres"
                   : "brakes";
        OptionsPanel.Visibility = _activeTab == "car" ? Visibility.Visible : Visibility.Collapsed;
        ApplyComparisonState();
    }    

    private void RefreshSavedList()
    {
        _savedLaps = _store.LoadAll();   // lista completa (serve per il lookup in UpdateComparison)

        // Mostra solo i giri coerenti col tracciato corrente.
        // Se non c'è un tracciato di riferimento (nessun duckdb), mostra tutti.
        var visible = _savedLaps
            .Where(x => string.IsNullOrWhiteSpace(_currentTrackName)
                        || SavedLapStore.SameTrack(_currentTrackName, x.Lap.TrackName))
            .Select(x => new SavedLapListItem
            {
                FilePath = x.Path,
                TrackName = x.Lap.TrackName,
                CarName = x.Lap.CarName,
                DisplayDate = x.Lap.LapDateIso,
                LapNumber = x.Lap.LapNumber,
                LapTimeFormatted = TimeSpan.FromSeconds(x.Lap.LapTimeSeconds).ToString(@"hh\:mm\:ss\.fff")
            }).ToList();

        SavedList.ItemsSource = visible;
        _savedItems = visible;
        // I giri salvati vengono ricreati: scarta dal confronto quelli non più presenti
        _comparison.RemoveAll(o => o is SavedLapListItem s && !_savedItems.Contains(s));
        ApplyComparisonState();
    }

    // ---------- EXPORT CSV ----------
    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_allPoints.Count == 0)
        {
            MessageBox.Show("Please load a session first.", "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = "CSV separated by semicolon (*.csv)|*.csv",
            FileName = System.IO.Path.GetFileNameWithoutExtension(_selectedFile ?? "telemetry") + "_export.csv",
            Title = "Export Telemetry for Excel"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            StatusText.Text = "Exporting CSV...";

            var c = CultureInfo.CurrentCulture;
            var sb = new StringBuilder(1 << 20);

            sb.AppendLine("Time_s;Speed_kmh;Throttle_pct;Brake_pct;Steering;RPM;SoC_pct;ERS_pct_s;TyreTemp_FL;TyreTemp_FR;TyreTemp_RL;TyreTemp_RR;TyrePress_FL;TyrePress_FR;TyrePress_RL;TyrePress_RR;TyreWear_FL;TyreWear_FR;TyreWear_RL;TyreWear_RR;BrakeTemp_FL;BrakeTemp_FR;BrakeTemp_RL;BrakeTemp_RR;BrakeWear_FL;BrakeWear_FR;BrakeWear_RL;BrakeWear_RR;TyreRim_FL;TyreRim_FR;TyreRim_RL;TyreRim_RR;TyreIn_FL;TyreIn_FR;TyreIn_RL;TyreIn_RR;TyreCt_FL;TyreCt_FR;TyreCt_RL;TyreCt_RR;TyreOt_FL;TyreOt_FR;TyreOt_RL;TyreOt_RR");

            int lapIdx = 0;
            foreach (var p in _allPoints)
            {
                while (lapIdx + 1 < _allLaps.Count && _allLaps[lapIdx + 1].Time <= p.Time) lapIdx++;
                int lapNo = _allLaps.Count > 0 ? _allLaps[lapIdx].LapNumber : 0;

                sb.Append(p.Time.ToString("0.####", c)).Append(';')
                  .Append(lapNo).Append(';')
                  .Append(p.Speed.ToString("0.##", c)).Append(';')
                  .Append((p.Throttle * 100).ToString("0.##", c)).Append(';')
                  .Append((p.Brake * 100).ToString("0.##", c)).Append(';')
                  .Append(p.Steering.ToString("0.####", c)).Append(';')
                  .Append(p.RPM.ToString("0.#", c)).Append(';')
                  .Append((p.SoC * 100).ToString("0.##", c)).Append(';')
                  .Append(p.ErsPower.ToString("0.##", c)).Append(';')
                  .Append(p.TyreTempFL.ToString("0.##", c)).Append(';')
                  .Append(p.TyreTempFR.ToString("0.##", c)).Append(';')
                  .Append(p.TyreTempRL.ToString("0.##", c)).Append(';')
                  .Append(p.TyreTempRR.ToString("0.##", c)).Append(';')
                  .Append((p.TyreWearFL * 100).ToString("0.##", c)).Append(';')
                  .Append((p.TyreWearFR * 100).ToString("0.##", c)).Append(';')
                  .Append((p.TyreWearRL * 100).ToString("0.##", c)).Append(';')
                  .Append((p.TyreWearRR * 100).ToString("0.##", c)).Append(';')
                  .Append(p.BrakeWearRR.ToString("0.###", c)).Append(';')
                  .Append(p.BrakeTempFL.ToString("0.##", c)).Append(';')
                  .Append(p.BrakeTempFR.ToString("0.##", c)).Append(';')
                  .Append(p.BrakeTempRL.ToString("0.##", c)).Append(';')
                  .Append((p.BrakeWearFL * 100).ToString("0.##", c)).Append(';')
                  .Append((p.BrakeWearFR * 100).ToString("0.##", c)).Append(';')
                  .Append((p.BrakeWearRL * 100).ToString("0.##", c)).Append(';')
                  .Append(p.TyrePressRR.ToString("0.##", c)).Append(';')
                  .Append(p.TyreRimFL.ToString("0.##", c)).Append(';')
                  .Append(p.TyreRimFR.ToString("0.##", c)).Append(';')
                  .Append(p.TyreRimRL.ToString("0.##", c)).Append(';')
                  .Append(p.TyreRimRR.ToString("0.##", c)).Append(';')
                  .Append(p.TyreInFL.ToString("0.##", c)).Append(';')
                  .Append(p.TyreInFR.ToString("0.##", c)).Append(';')
                  .Append(p.TyreInRL.ToString("0.##", c)).Append(';')
                  .Append(p.TyreInRR.ToString("0.##", c)).Append(';')
                  .Append(p.TyreCtFL.ToString("0.##", c)).Append(';')
                  .Append(p.TyreCtFR.ToString("0.##", c)).Append(';')
                  .Append(p.TyreCtRL.ToString("0.##", c)).Append(';')
                  .Append(p.TyreCtRR.ToString("0.##", c)).Append(';')
                  .Append(p.TyreOtFL.ToString("0.##", c)).Append(';')
                  .Append(p.TyreOtFR.ToString("0.##", c)).Append(';')
                  .Append(p.TyreOtRL.ToString("0.##", c)).Append(';')
                  .Append(p.TyreOtRR.ToString("0.##", c)).AppendLine();
            }

            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);

            StatusText.Text = $"CSV exported: {System.IO.Path.GetFileName(dlg.FileName)}";

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusText.Text = "Error exporting";
            MessageBox.Show(ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }


    private void LapsList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var list = (ListBox)sender;
        var clicked = ItemAtPoint(list, e.GetPosition(list));
        if (clicked is null)
        {
            return;
        }

        e.Handled = true;   // blocca il click PRIMA che il controllo lo gestisca
        ToggleComparison(clicked);
    }

    private void ToggleComparison(object clicked)
    {

        // Click su un giro già nel confronto -> lo toglie
        if (_comparison.Contains(clicked))
        {
            _comparison.Remove(clicked);
            ApplyComparisonState();
            return;
        }

        // Terzo giro -> rifiutato
        if (_comparison.Count >= 2)
        {
            StatusText.Text = "Max 2 laps in comparison: deselect one before adding another.";
            return;
        }

        // Validazione tracciato per i giri salvati
        if (clicked is SavedLapListItem si)
        {
            var saved = _savedLaps.FirstOrDefault(x => x.Path == si.FilePath).Lap;
            if (saved is null)
            {
                return;
            }

            var refTrack = CurrentRefTrack();

            if (!string.IsNullOrWhiteSpace(refTrack)
                && !SavedLapStore.SameTrack(refTrack, saved.TrackName))
            {
                MessageBox.Show(
                    $"This saved lap is from a different track and will not be added to the comparison.\n\n" +
                    $"Reference: {refTrack}\n" +
                    $"Saved lap: {saved.TrackName}",
                    "Different track",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        _comparison.Add(clicked);
        ApplyComparisonState();
    }

    private string CurrentRefTrack()
    {
        if (!string.IsNullOrWhiteSpace(_currentTrackName)) return _currentTrackName;
        foreach (var o in _comparison)
            if (o is SavedLapListItem s) return s.TrackName;
        return "";
    }

    private static void SetRowBg(object src, SolidColorBrush brush)
    {
        if (src is LapInfo l) l.RowBg = brush;
        else if (src is SavedLapListItem s) s.RowBg = brush;
    }

    private void ApplyComparisonState()
    {
        // Barrette: reset e colori per posizione nel confronto
        foreach (var l in _lapInfos) l.RowBg = TransparentBrush;
        foreach (var s in _savedItems) s.RowBg = TransparentBrush;
        for (int gi = 0; gi < _comparison.Count && gi < 2; gi++)
            SetRowBg(_comparison[gi], new SolidColorBrush(LapColors[gi]));

        var entries = new List<ComparisonEntry>();

        for (int gi = 0; gi < _comparison.Count && gi < 2; gi++)
        {
            var src = _comparison[gi];

            if (src is LapInfo li)
            {
                var pts = _allPoints
                    .Where(p => p.Time >= li.StartTime && p.Time <= li.EndTime)
                    .Select(p => new TelemetryPoint
                    {
                        Time = p.Time - li.StartTime,
                        Speed = p.Speed, Throttle = p.Throttle, Brake = p.Brake,
                        Steering = p.Steering, RPM = p.RPM,
                        SoC = p.SoC,
                        ErsPower = p.ErsPower,
                        TyreTempFL = p.TyreTempFL, TyreTempFR = p.TyreTempFR,
                        TyreTempRL = p.TyreTempRL, TyreTempRR = p.TyreTempRR,
                        TyrePressFL = p.TyrePressFL, TyrePressFR = p.TyrePressFR,
                        TyrePressRL = p.TyrePressRL, TyrePressRR = p.TyrePressRR,
                        TyreWearFL = p.TyreWearFL, TyreWearFR = p.TyreWearFR,
                        TyreWearRL = p.TyreWearRL, TyreWearRR = p.TyreWearRR,
                        BrakeTempFL = p.BrakeTempFL, BrakeTempFR = p.BrakeTempFR,
                        BrakeTempRL = p.BrakeTempRL, BrakeTempRR = p.BrakeTempRR,
                        BrakeWearFL = p.BrakeWearFL, BrakeWearFR = p.BrakeWearFR,
                        BrakeWearRL = p.BrakeWearRL, BrakeWearRR = p.BrakeWearRR,
                        TyreRimFL = p.TyreRimFL, TyreRimFR = p.TyreRimFR, TyreRimRL = p.TyreRimRL, TyreRimRR = p.TyreRimRR,
                        TyreInFL = p.TyreInFL, TyreInFR = p.TyreInFR, TyreInRL = p.TyreInRL, TyreInRR = p.TyreInRR,
                        TyreCtFL = p.TyreCtFL, TyreCtFR = p.TyreCtFR, TyreCtRL = p.TyreCtRL, TyreCtRR = p.TyreCtRR,
                        TyreOtFL = p.TyreOtFL, TyreOtFR = p.TyreOtFR, TyreOtRL = p.TyreOtRL, TyreOtRR = p.TyreOtRR
                    }).ToList();

                ComputeDistance(pts);
                if (ChkHideShifts.IsChecked == true)
                {
                    MedianFilter(pts, p => p.Throttle * 100.0, (p, v) => p.Throttle = v / 100.0, 0.30);
                    MedianFilter(pts, p => p.Brake * 100.0, (p, v) => p.Brake = v / 100.0, 0.30);
                }

                entries.Add(new ComparisonEntry
                {
                    Label = $"L{li.LapNumber} • {li.LapTimeFormatted}",
                    TrackName = _currentTrackName,
                    CarName = "sessione corrente",
                    Points = pts,
                    IsSaved = false,
                    SourceItem = li
                });
            }
            else if (src is SavedLapListItem si)
            {
                var saved = _savedLaps.FirstOrDefault(x => x.Path == si.FilePath).Lap;
                if (saved == null) continue;

                var ptsSaved = saved.Points.Select(sp => new TelemetryPoint
                {
                    Time = sp.T, Speed = sp.S, Throttle = sp.Th, Brake = sp.Br,
                    Steering = sp.St, RPM = sp.R,
                    SoC = sp.SoC,
                    ErsPower = sp.Ers,
                    TyrePressFL = sp.Tp1, TyrePressFR = sp.Tp2, TyrePressRL = sp.Tp3, TyrePressRR = sp.Tp4,
                    TyreTempFL = sp.Tt1, TyreTempFR = sp.Tt2, TyreTempRL = sp.Tt3, TyreTempRR = sp.Tt4,
                    TyreWearFL = sp.Tw1, TyreWearFR = sp.Tw2, TyreWearRL = sp.Tw3, TyreWearRR = sp.Tw4,
                    BrakeTempFL = sp.Bt1, BrakeTempFR = sp.Bt2, BrakeTempRL = sp.Bt3, BrakeTempRR = sp.Bt4,
                    BrakeWearFL = sp.Bw1, BrakeWearFR = sp.Bw2, BrakeWearRL = sp.Bw3, BrakeWearRR = sp.Bw4,
                    TyreRimFL = sp.Rt1, TyreRimFR = sp.Rt2, TyreRimRL = sp.Rt3, TyreRimRR = sp.Rt4,
                    TyreInFL = sp.Zi1, TyreInFR = sp.Zi2, TyreInRL = sp.Zi3, TyreInRR = sp.Zi4,
                    TyreCtFL = sp.Zc1, TyreCtFR = sp.Zc2, TyreCtRL = sp.Zc3, TyreCtRR = sp.Zc4,
                    TyreOtFL = sp.Zo1, TyreOtFR = sp.Zo2, TyreOtRL = sp.Zo3, TyreOtRR = sp.Zo4
                }).ToList();

                ComputeDistance(ptsSaved);
                if (ChkHideShifts.IsChecked == true)
                {
                    MedianFilter(ptsSaved, p => p.Throttle * 100.0, (p, v) => p.Throttle = v / 100.0, 0.30);
                    MedianFilter(ptsSaved, p => p.Brake * 100.0, (p, v) => p.Brake = v / 100.0, 0.30);
                }

                entries.Add(new ComparisonEntry
                {
                    Label = $"L{saved.LapNumber} • {TimeSpan.FromSeconds(saved.LapTimeSeconds).ToString(@"hh\:mm\:ss\.fff")}",
                    TrackName = saved.TrackName,
                    CarName = saved.CarName,
                    Points = ptsSaved,
                    IsSaved = true,
                    SourceItem = si
                });
            }
        }

        DrawChart(entries);
        CursorSlider.Visibility = entries.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshChannelAvailability()
    {
        ChkSoC.IsEnabled       = _reader.AvailableChannels.Contains("SoC");
        ChkErs.IsEnabled       = _reader.AvailableChannels.Contains("ERS");
        bool tyreAvail = _reader.AvailableChannels.Contains("TyrePress")
                    || _reader.AvailableChannels.Contains("TyreZones")
                    || _reader.AvailableChannels.Contains("TyreRim");
        RbTabTyres.IsEnabled = tyreAvail;

        bool brakeAvail = _reader.AvailableChannels.Contains("BrakeTemp")
                    || _reader.AvailableChannels.Contains("BrakeWear");
        RbTabBrakes.IsEnabled = brakeAvail;
    }

    private void ChartCanvas_MouseLeave(object sender, MouseEventArgs e) => HideHover();

    private void HideHover()
    {
        HoverTip.Visibility = Visibility.Collapsed;
        HoverDot.Visibility = Visibility.Collapsed;
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

    private void ChartCanvas_MouseMove(object sender, MouseEventArgs e)
    {
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
}