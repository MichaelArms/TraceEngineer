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
            // Calcola distanza percorsa per tutti i punti (necessario per lunghezza tracciato e analisi stint)
            ComputeDistance(_allPoints);            

            // Calcola info giri
            for (int i = 0; i < _allLaps.Count; i++)
            {
                var lap = _allLaps[i];
                double startTime = lap.Time;
                double endTime = (i + 1 < _allLaps.Count) ? _allLaps[i + 1].Time : _allPoints[^1].Time;
                double lapTime = endTime - startTime;

                // Calcola distanza percorsa nel giro
                var startPt = _allPoints.FirstOrDefault(p => p.Time >= startTime);
                var endPt = _allPoints.LastOrDefault(p => p.Time < endTime);
                double distance = (endPt != null && startPt != null)
                    ? endPt.Distance - startPt.Distance
                    : 0;

                _lapInfos.Add(new LapInfo
                {
                    LapNumber = lap.LapNumber,
                    StartTime = startTime,
                    EndTime = endTime,
                    LapTime = lapTime,
                    Distance = distance
                });
            }

            LapList.ItemsSource = _lapInfos;
            ComputeLapStats();             

            // Costruzione mappa GPS al load
            _sessionMapRaw = null;
            _mapProfile = null;

            // 1. Trova il giro con la distanza massima (il giro "buono" più completo, esclude in/out-lap)
            var bestLapInfo = _lapInfos.OrderByDescending(l => l.Distance).FirstOrDefault();
            if (bestLapInfo != null)
            {
                var bestPoints = _allPoints.Where(p => p.Time >= bestLapInfo.StartTime && p.Time < bestLapInfo.EndTime)
                                           .Where(p => p.MapX != 0 || p.MapY != 0)
                                           .ToList();

                if (bestPoints.Count >= 100)
                {
                    _sessionMapRaw = bestPoints;
                    System.Diagnostics.Debug.WriteLine($"[Map] Using best lap {bestLapInfo.LapNumber} with {bestPoints.Count} GPS pts");

                    // 2. Trova l'indice del punto con distanza minima (la linea di start/finish)
                    int minDistIndex = 0;
                    double minDist = bestPoints[0].Distance;
                    for (int i = 1; i < bestPoints.Count; i++)
                    {
                        if (bestPoints[i].Distance < minDist)
                        {
                            minDist = bestPoints[i].Distance;
                            minDistIndex = i;
                        }
                    }

                    // 3. RUOTA la lista dei punti in modo che inizi e finisca alla linea di start/finish.
                    // Questo chiude il circuito visivamente e allinea perfettamente le frazioni di distanza per TUTTI i giri.
                    var rotatedPoints = bestPoints.Skip(minDistIndex).Concat(bestPoints.Take(minDistIndex)).ToList();

                    // 4. Campiona per il disegno (max ~600 punti per performance)
                    var raw = new List<(double X, double Y)>();
                    int step = Math.Max(1, rotatedPoints.Count / 600);
                    for (int i = 0; i < rotatedPoints.Count; i += step)
                    {
                        raw.Add((rotatedPoints[i].MapX, rotatedPoints[i].MapY));
                    }

                    _mapRawMinX = raw.Min(p => p.X); _mapRawMaxX = raw.Max(p => p.X);
                    _mapRawMinY = raw.Min(p => p.Y); _mapRawMaxY = raw.Max(p => p.Y);

                    if (_mapRawMaxX - _mapRawMinX > 1e-9 && _mapRawMaxY - _mapRawMinY > 1e-9)
                    {
                        // Dimensioni di riferimento per il calcolo iniziale
                        double w = 170, h = 120; 
                        double pad = 6;
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
            }    

            // Rileva tracciato
            var meta = await Task.Run(() => _reader.ReadMetadata(filePath));
            if (meta is null) meta = new Dictionary<string, string>();
            _currentTrackName = DetectTrackName(meta, filePath);
            _currentSessionDate = DetectDate(meta, filePath);

            TrackRefText.Text = _currentTrackName != ""
                ? $"Reference track: {_currentTrackName}"
                : "Track: not detected";

            SessionHeader.Text = _currentTrackName != ""
                ? $"{_currentTrackName}  •  {_currentSessionDate}"
                : _currentSessionDate;
            SessionHeader.Text += "   •   " + DetectCarName(_reader.ReadMetadata(_selectedFile ?? ""));
            
            // Popola info sessione: circuito • numero giri • data
            int lapCount = _lapInfos?.Count ?? 0;
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(_currentTrackName)) parts.Add(_currentTrackName);
            parts.Add($"{lapCount} {(lapCount == 1 ? "lap" : "laps")}");
            string dateTxt = FormatSessionDate(_currentSessionDate);
                if (!string.IsNullOrWhiteSpace(dateTxt)) parts.Add(dateTxt);
            SessionInfo.Text = string.Join("  •  ", parts);

            BtnSaveLap.IsEnabled = (_lapInfos?.Count ?? 0) > 0;
            RefreshSavedList();

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
        