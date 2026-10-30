using System.Windows;
using Microsoft.Win32;
using TraceEngineer.Models;

namespace TraceEngineer;

public partial class MainWindow : Window
{

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
            ComputeLapStats();

            // Costruzione mappa GPS al load (un giro completo)
            _sessionMapRaw = null;
            _mapProfile = null;
            var lapCandidates = new List<(int Index, int Count, List<TelemetryPoint> Points)>();
            for (int i = 0; i < _lapInfos.Count; i++)
            {
                var lap = _lapInfos[i];
                var pts = _allPoints.Where(p => p.Time >= lap.StartTime && p.Time < lap.EndTime)
                                    .Where(p => p.MapX != 0 || p.MapY != 0)
                                    .ToList();
                if (pts.Count >= 100)
                    lapCandidates.Add((i, pts.Count, pts));
            }
            if (lapCandidates.Count > 0)
            {
                // Preferisci giri "chiusi" (inizio ≈ fine = linea S/F) → giri lanciati.
                // Out/in-lap partono o finiscono in pit e non si chiudono.
                double spanX = 0, spanY = 0;
                foreach (var c in lapCandidates)
                {
                    double mx = c.Points.Max(p => p.MapX), mnx = c.Points.Min(p => p.MapX);
                    double my = c.Points.Max(p => p.MapY), mny = c.Points.Min(p => p.MapY);
                    spanX = Math.Max(spanX, mx - mnx);
                    spanY = Math.Max(spanY, my - mny);
                }
                double closeTh = 0.05 * Math.Max(spanX, spanY);
                var closedLaps = lapCandidates
                    .Where(x =>
                    {
                        var a = x.Points[0]; var b = x.Points[^1];
                        double d = Math.Sqrt((a.MapX - b.MapX) * (a.MapX - b.MapX)
                                           + (a.MapY - b.MapY) * (a.MapY - b.MapY));
                        return d < closeTh;
                    })
                    .OrderByDescending(x => x.Count)
                    .ToList();
                var best = closedLaps.Count > 0
                    ? closedLaps[0]
                    : lapCandidates.OrderByDescending(x => x.Count).First();
                _sessionMapRaw = best.Points;
                System.Diagnostics.Debug.WriteLine(
                    $"[Map] cached lap {best.Index + 1}: {best.Count} pts with GPS");

                // Costruisci profilo proiettato
                var raw = new List<(double X, double Y)>();
                int step = Math.Max(1, best.Count / 600);
                for (int i = 0; i < best.Count; i += step)
                    raw.Add((best.Points[i].MapX, best.Points[i].MapY));

                _mapRawMinX = raw.Min(p => p.X); _mapRawMaxX = raw.Max(p => p.X);
                _mapRawMinY = raw.Min(p => p.Y); _mapRawMaxY = raw.Max(p => p.Y);

                if (_mapRawMaxX - _mapRawMinX > 1e-9 && _mapRawMaxY - _mapRawMinY > 1e-9)
                {
                    // Proiezione: uso dimensioni fisse (170x120) perché il canvas non è ancora renderizzato
                    double w = 170, h = 120, pad = 6;
                    double sx = (w - 2 * pad) / (_mapRawMaxX - _mapRawMinX);
                    double sy = (h - 2 * pad) / (_mapRawMaxY - _mapRawMinY);
                    double s = Math.Min(sx, sy);
                    double ox = pad + ((w - 2 * pad) - (_mapRawMaxX - _mapRawMinX) * s) / 2;
                    double oy = pad + ((h - 2 * pad) - (_mapRawMaxY - _mapRawMinY) * s) / 2;

                    _mapProfile = raw.Select(p => new Point(
                        ox + (p.X - _mapRawMinX) * s,
                        oy + (_mapRawMaxY - p.Y) * s  // Y flip: nord in alto
                    )).ToList();
                    _mapOutline = _mapProfile;

                    // Calcola cumLen per PointAtArcFraction
                    _mapCumLen = new List<double> { 0 };
                    double acc = 0;
                    for (int i = 1; i < _mapProfile.Count; i++)
                    {
                        acc += Point.Subtract(_mapProfile[i], _mapProfile[i - 1]).Length;
                        _mapCumLen.Add(acc);
                    }
                    _mapTotalLen = acc;
                    _mapHasGps = true;
                }
            }    

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
}
        