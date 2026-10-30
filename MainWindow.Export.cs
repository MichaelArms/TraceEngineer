// MainWindow.Export.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;                 // File, Path
using System.Linq;
using System.Text;               // StringBuilder
using System.Windows;            // MessageBox, RoutedEventArgs
using Microsoft.Win32;           // ← SaveFileDialog, OpenFileDialog
using TraceEngineer.Models;      // ← TelemetryPoint


namespace TraceEngineer;

public partial class MainWindow : Window
{

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
            Title = "Export Session Telemetry"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            StatusText.Text = "Exporting CSV...";

            var c = CultureInfo.CurrentCulture;
            var sb = new StringBuilder(1 << 20);

            // Definizione colonne: (nome, extractor, formato)
            var cols = new (string Name, Func<TelemetryPoint, double> Get, string Fmt)[]
            {
                ("Time_s",       p => p.Time,                   "0.####"),
                ("Speed_kmh",    p => p.Speed,                  "0.##"),
                ("Throttle_pct", p => p.Throttle * 100,         "0.##"),
                ("Brake_pct",    p => p.Brake * 100,            "0.##"),
                ("Steering",     p => p.Steering,               "0.####"),
                ("RPM",          p => p.RPM,                    "0.#"),
                ("SoC_pct",      p => p.SoC * 100,              "0.##"),
                ("ERS_pct_s",    p => p.ErsPower,               "0.##"),

                // Tyres carcass
                ("TyreTempFL",   p => p.TyreTempFL, "0.##"),
                ("TyreTempFR",   p => p.TyreTempFR, "0.##"),
                ("TyreTempRL",   p => p.TyreTempRL, "0.##"),
                ("TyreTempRR",   p => p.TyreTempRR, "0.##"),

                // Tyres zones
                ("TyreInFL",     p => p.TyreInFL, "0.##"),
                ("TyreInFR",     p => p.TyreInFR, "0.##"),
                ("TyreInRL",     p => p.TyreInRL, "0.##"),
                ("TyreInRR",     p => p.TyreInRR, "0.##"),
                ("TyreCtFL",     p => p.TyreCtFL, "0.##"),
                ("TyreCtFR",     p => p.TyreCtFR, "0.##"),
                ("TyreCtRL",     p => p.TyreCtRL, "0.##"),
                ("TyreCtRR",     p => p.TyreCtRR, "0.##"),
                ("TyreOtFL",     p => p.TyreOtFL, "0.##"),
                ("TyreOtFR",     p => p.TyreOtFR, "0.##"),
                ("TyreOtRL",     p => p.TyreOtRL, "0.##"),
                ("TyreOtRR",     p => p.TyreOtRR, "0.##"),

                // Tyres rim + pressure + wear
                ("TyreRimFL",    p => p.TyreRimFL, "0.##"),
                ("TyreRimFR",    p => p.TyreRimFR, "0.##"),
                ("TyreRimRL",    p => p.TyreRimRL, "0.##"),
                ("TyreRimRR",    p => p.TyreRimRR, "0.##"),
                ("TyrePressFL",  p => p.TyrePressFL, "0.##"),
                ("TyrePressFR",  p => p.TyrePressFR, "0.##"),
                ("TyrePressRL",  p => p.TyrePressRL, "0.##"),
                ("TyrePressRR",  p => p.TyrePressRR, "0.##"),
                ("TyreWearFL",   p => p.TyreWearFL * 100, "0.##"),
                ("TyreWearFR",   p => p.TyreWearFR * 100, "0.##"),
                ("TyreWearRL",   p => p.TyreWearRL * 100, "0.##"),
                ("TyreWearRR",   p => p.TyreWearRR * 100, "0.##"),

                // Brakes
                ("BrakeTempFL",  p => p.BrakeTempFL, "0.##"),
                ("BrakeTempFR",  p => p.BrakeTempFR, "0.##"),
                ("BrakeTempRL",  p => p.BrakeTempRL, "0.##"),
                ("BrakeTempRR",  p => p.BrakeTempRR, "0.##"),
                ("BrakeWearFL",  p => p.BrakeWearFL * 100, "0.##"),
                ("BrakeWearFR",  p => p.BrakeWearFR * 100, "0.##"),
                ("BrakeWearRL",  p => p.BrakeWearRL * 100, "0.##"),
                ("BrakeWearRR",  p => p.BrakeWearRR * 100, "0.##")
            };

            // Header
            sb.AppendLine("Lap;" + string.Join(";", cols.Select(col => col.Name)));

            // Righe
            int lapIdx = 0;
            foreach (var p in _allPoints)
            {
                while (lapIdx + 1 < _allLaps.Count && _allLaps[lapIdx + 1].Time <= p.Time) lapIdx++;
                int lapNo = _allLaps.Count > 0 ? _allLaps[lapIdx].LapNumber : 0;

                sb.Append(lapNo);
                foreach (var col in cols)
                {
                    sb.Append(';').Append(col.Get(p).ToString(col.Fmt, c));
                }
                sb.AppendLine();
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

}