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

    private List<TelemetryPoint> _allPoints = new();
    private List<LapMarker> _allLaps = new();
    private List<LapInfo> _lapInfos = new();
    private List<LapStats> _lapStats = new();
    private List<HoverStintLane> _hoverStintLanes = new();    
    private const double AnomalousFactor = 1.15;    
    private const double IncompleteFactor = 0.5;    
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
    private List<Point>? _mapOutline;
    private List<double> _mapCumLen = new();
    private double _mapTotalLen;
    private double _mapRawMinX, _mapRawMaxX, _mapRawMinY, _mapRawMaxY;
    private bool _mapHasGps;
    private List<Ellipse> _mapDots = new();
    private List<TelemetryPoint>? _sessionMapRaw;
    private List<Point>? _mapProfile;   // Profilo già proiettato su canvas        

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
    private sealed class LapStats
    {
        public int LapNumber;
        public double LapTime;
        public double AvgSpeed, MaxSpeed;
        public double TyreTempFL, TyreTempFR, TyreTempRL, TyreTempRR;
        public double TyrePressFL, TyrePressFR, TyrePressRL, TyrePressRR;
        public double TyreWearFL, TyreWearFR, TyreWearRL, TyreWearRR;
        public double SoCStart, SoCEnd;
        public double BrakeTempFL, BrakeTempFR, BrakeTempRL, BrakeTempRR;
        public double BrakeWearFL, BrakeWearFR, BrakeWearRL, BrakeWearRR;
    }
    private sealed class HoverStintLane
    {
        public string LaneName = "";
        public string Unit = "";
        public double TraceTop, TraceH;
        public double YMin, YSpan;
        public List<StintSeries> Series = new();
        public List<int> LapNumbers = new();
        public List<double> Xs = new();
    }    
    private sealed class StintLane
    {
        public string Name = "";
        public string Unit = "";
        public List<StintSeries> Series = new();
    }
    private sealed class StintSeries
    {
        public string Name = "";
        public Color Color;
        public List<double> Values = new();
    }    

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

    private void Channel_Click(object sender, RoutedEventArgs e) => ApplyComparisonState();
    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyComparisonState();
   
    private static SolidColorBrush Brush(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

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

    private void Axis_Click(object sender, RoutedEventArgs e) => ApplyComparisonState();

    private void StintExclude_Changed(object sender, RoutedEventArgs e) => RedrawStint();


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


    private static readonly Color[] StintWheelColors =
    {
        Color.FromRgb(0, 214, 255),   // FL ciano
        Color.FromRgb(255, 205, 60),  // FR giallo
        Color.FromRgb(150, 255, 130), // RL verde
        Color.FromRgb(255, 82, 82)    // RR rosso
    };
}