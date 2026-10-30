// MainWindow.Selection.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;      // ← per MouseButtonEventArgs
using System.Windows.Media; 
using TraceEngineer.Models;
using TraceEngineer.Services;

namespace TraceEngineer;

public partial class MainWindow : Window
{

    private readonly SavedLapStore _store = new();
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
                        MapX = p.MapX, MapY = p.MapY,
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
            CursorSlider.Visibility = (_activeTab != "stint" && entries.Count > 0)
                                    ? Visibility.Visible
                                    : Visibility.Collapsed;           
        }

        DrawChart(entries);
        CursorSlider.Visibility = (_activeTab != "stint" && entries.Count > 0)
                                ? Visibility.Visible
                                : Visibility.Collapsed;
        RefreshTrackMap();
    }

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

    private static void SetRowBg(object src, SolidColorBrush brush)
    {
        if (src is LapInfo l) l.RowBg = brush;
        else if (src is SavedLapListItem s) s.RowBg = brush;
    }

    private string CurrentRefTrack()
    {
        if (!string.IsNullOrWhiteSpace(_currentTrackName)) return _currentTrackName;
        foreach (var o in _comparison)
            if (o is SavedLapListItem s) return s.TrackName;
        return "";
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

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        _activeTab = RbTabCar.IsChecked == true ? "car"
                   : RbTabTyres.IsChecked == true ? "tyres"
                   : RbTabBrakes.IsChecked == true ? "brakes"
                   : "stint";
        OptionsPanel.Visibility = _activeTab == "car" ? Visibility.Visible : Visibility.Collapsed;
        StintOptionsPanel.Visibility = _activeTab == "stint" ? Visibility.Visible : Visibility.Collapsed;
        ApplyComparisonState();
    }
}
    