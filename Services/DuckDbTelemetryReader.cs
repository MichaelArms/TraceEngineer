using System.IO;
using DuckDB.NET.Data;
using TraceEngineer.Models;

namespace TraceEngineer.Services;

public class DuckDbTelemetryReader
{
    public TelemetrySession ReadSession(string filePath)
    {
        var session = new TelemetrySession();

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Il file non esiste: {filePath}");

        var connectionString = $"Data Source={filePath}";

        using var connection = new DuckDBConnection(connectionString);
        connection.Open();

        // DEBUG info
        var debugInfo = new System.Text.StringBuilder();
        var tablesToCheck = new[] { "GPS Time", "Ground Speed", "Steering Pos", "Engine RPM", "Throttle Pos", "Brake Pos" };

        foreach (var table in tablesToCheck)
        {
            using var cmdCount = connection.CreateCommand();
            cmdCount.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
            var count = Convert.ToInt64(cmdCount.ExecuteScalar());
            debugInfo.AppendLine($"{table}: {count} righe");
        }

        // GPS Time
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"SELECT value FROM ""GPS Time"" ORDER BY rowid";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                session.Points.Add(new TelemetryPoint
                {
                    Time = reader.IsDBNull(0) ? 0 : Convert.ToDouble(reader.GetValue(0))
                });
            }
        }
        debugInfo.AppendLine($"Punti GPS Time caricati: {session.Points.Count}");

        // Ground Speed
        var speedValues = new List<double>();
        using (var cmdSpeed = connection.CreateCommand())
        {
            cmdSpeed.CommandText = @"SELECT value FROM ""Ground Speed"" ORDER BY rowid";
            using var r = cmdSpeed.ExecuteReader();
            while (r.Read())
            {
                speedValues.Add(r.IsDBNull(0) ? 0 : Convert.ToDouble(r.GetValue(0)));
            }
        }
        for (int i = 0; i < session.Points.Count && i < speedValues.Count; i++)
            session.Points[i].Speed = speedValues[i];

        // Steering
        var steeringValues = new List<double>();
        using (var cmdSteer = connection.CreateCommand())
        {
            cmdSteer.CommandText = @"SELECT value FROM ""Steering Pos"" ORDER BY rowid";
            using var r = cmdSteer.ExecuteReader();
            while (r.Read())
            {
                steeringValues.Add(r.IsDBNull(0) ? 0 : Convert.ToDouble(r.GetValue(0)));
            }
        }
        for (int i = 0; i < session.Points.Count && i < steeringValues.Count; i++)
            session.Points[i].Steering = steeringValues[i];

        // RPM
        var rpmValues = new List<double>();
        using (var cmdRpm = connection.CreateCommand())
        {
            cmdRpm.CommandText = @"SELECT value FROM ""Engine RPM"" ORDER BY rowid";
            using var r = cmdRpm.ExecuteReader();
            while (r.Read())
            {
                rpmValues.Add(r.IsDBNull(0) ? 0 : Convert.ToDouble(r.GetValue(0)));
            }
        }
        for (int i = 0; i < session.Points.Count && i < rpmValues.Count; i++)
            session.Points[i].RPM = rpmValues[i];

        // Throttle
        using (var cmdThrottle = connection.CreateCommand())
        {
            cmdThrottle.CommandText = @"SELECT value FROM ""Throttle Pos"" ORDER BY rowid";
            using var r = cmdThrottle.ExecuteReader();
            while (r.Read())
            {
                session.ThrottleValues.Add(r.IsDBNull(0) ? 0 : Convert.ToDouble(r.GetValue(0)));
            }
        }

        // Brake
        using (var cmdBrake = connection.CreateCommand())
        {
            cmdBrake.CommandText = @"SELECT value FROM ""Brake Pos"" ORDER BY rowid";
            using var r = cmdBrake.ExecuteReader();
            while (r.Read())
            {
                session.BrakeValues.Add(r.IsDBNull(0) ? 0 : Convert.ToDouble(r.GetValue(0)));
            }
        }

        // Lap markers
        try
        {
            using var cmdLap = connection.CreateCommand();
            cmdLap.CommandText = @"SELECT ts, value FROM ""Lap"" ORDER BY ts";
            using var r = cmdLap.ExecuteReader();
            while (r.Read())
            {
                session.Laps.Add(new LapMarker
                {
                    Time = Convert.ToDouble(r.GetValue(0)),
                    LapNumber = Convert.ToInt32(r.GetValue(1))
                });
            }
        }
        catch { }

        EnrichHybridAndWear(session.Points, filePath);
        session.DebugInfo = debugInfo.ToString();
        return session;
    }

    public Dictionary<string, string> ReadMetadata(string filePath)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(filePath)) return result;

        try
        {
            using var connection = new DuckDBConnection($"Data Source={filePath}");
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT key, value FROM metadata";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var k = r.GetValue(0)?.ToString() ?? "";
                var v = r.GetValue(1)?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(k)) result[k] = v;
            }
        }
        catch { }

        return result;
    }

    public HashSet<string> AvailableChannels { get; } = new();

    // Allinea un canale a frequenza inferiore sulla timeline master
    // tramite interpolazione lineare sull'indice (campionamento uniforme).
    private static double[] Align(List<double> raw, int masterCount)
    {
        var result = new double[masterCount];
        int n = raw.Count;
        if (n == 0) return result;
        if (n == 1) { for (int i = 0; i < masterCount; i++) result[i] = raw[0]; return result; }

        for (int m = 0; m < masterCount; m++)
        {
            double f = (double)m * (n - 1) / (masterCount - 1);
            int i0 = (int)Math.Floor(f);
            int i1 = Math.Min(n - 1, i0 + 1);
            double frac = f - i0;
            result[m] = raw[i0] * (1 - frac) + raw[i1] * frac;
        }
        return result;
    }

    private static List<double>[]? ReadWheelTable(string connStr, string table, int masterCount)
    {
        try
        {
            using var conn = new DuckDBConnection(connStr);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT value1, value2, value3, value4 FROM \"{table}\" ORDER BY rowid";
            using var r = cmd.ExecuteReader();

            var cols = new[] { new List<double>(), new List<double>(), new List<double>(), new List<double>() };
            while (r.Read())
                for (int k = 0; k < 4; k++)
                    cols[k].Add(Convert.ToDouble(r.GetValue(k)));

            if (cols[0].Count == 0) return null;
            for (int k = 0; k < 4; k++)
            {
                var aligned = Align(cols[k], masterCount);
                cols[k] = new List<double>(aligned);
            }
            return cols;
        }
        catch 
        {
            return null;
        }
    }

    private static double[]? ReadSingleTable(string connStr, string table, int masterCount)
    {
        try
        {
            using var conn = new DuckDBConnection(connStr);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT value FROM \"{table}\" ORDER BY rowid";
            using var r = cmd.ExecuteReader();

            var raw = new List<double>();
            while (r.Read()) raw.Add(Convert.ToDouble(r.GetValue(0)));
            if (raw.Count == 0) return null;
            return Align(raw, masterCount);
        }
        catch 
        {
            return null;
        }
    }

    // Arricchisce i punti master con SoC/ERS e canali per-ruota
    public void EnrichHybridAndWear(List<TelemetryPoint> points, string filePath)
    {
        if (points.Count < 2) return;
        string connStr = $"Data Source={filePath}";
        int n = points.Count;

        var soc = ReadSingleTable(connStr, "SoC", n);
        if (soc != null)
        {
            AvailableChannels.Add("SoC");
            AvailableChannels.Add("ERS");
            for (int i = 0; i < n; i++)
            {
                points[i].SoC = soc[i];
                if (i > 0)
                {
                    double dt = points[i].Time - points[i - 1].Time;
                    points[i].ErsPower = dt > 1e-9 ? -(soc[i] - soc[i - 1]) / dt : 0.0;
                }
            }
        }

        var tt = ReadWheelTable(connStr, "TyresCarcassTemp", n);
        if (tt != null)
        {
            AvailableChannels.Add("TyreTemp");
            for (int i = 0; i < n; i++)
            {
                points[i].TyreTempFL = tt[0][i]; points[i].TyreTempFR = tt[1][i];
                points[i].TyreTempRL = tt[2][i]; points[i].TyreTempRR = tt[3][i];
            }
        }

        var tw = ReadWheelTable(connStr, "Tyres Wear", n);
        if (tw != null)
        {
            AvailableChannels.Add("TyreWear");
            for (int i = 0; i < n; i++)
            {
                points[i].TyreWearFL = tw[0][i]; points[i].TyreWearFR = tw[1][i];
                points[i].TyreWearRL = tw[2][i]; points[i].TyreWearRR = tw[3][i];
            }
        }

        var bw = ReadWheelTable(connStr, "Brake Thickness", n);
        if (bw != null)
        {
            // Verifica se il canale è realmente dinamico in questa sessione
            bool dynamic = false;
            for (int i = 1; i < n && !dynamic; i++)
            {
                if (Math.Abs(bw[0][i] - bw[0][0]) > 1e-6 ||
                    Math.Abs(bw[1][i] - bw[1][0]) > 1e-6 ||
                    Math.Abs(bw[2][i] - bw[2][0]) > 1e-6 ||
                    Math.Abs(bw[3][i] - bw[3][0]) > 1e-6)
                    dynamic = true;
            }

            if (dynamic)
            {
                AvailableChannels.Add("BrakeWear");
                for (int i = 0; i < n; i++)
                {
                    points[i].BrakeWearFL = bw[0][i]; points[i].BrakeWearFR = bw[1][i];
                    points[i].BrakeWearRL = bw[2][i]; points[i].BrakeWearRR = bw[3][i];
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    "[BrakeWear] static channel: LMU does not simulate brake wear in-session");
            }
        }

        var bt = ReadWheelTable(connStr, "Brakes Temp", n);
        if (bt != null)
        {
            bool dyn = false;
            for (int i = 1; i < n && !dyn; i++)
            {
                if (Math.Abs(bt[0][i] - bt[0][0]) > 1e-6 || Math.Abs(bt[1][i] - bt[1][0]) > 1e-6 ||
                    Math.Abs(bt[2][i] - bt[2][0]) > 1e-6 || Math.Abs(bt[3][i] - bt[3][0]) > 1e-6)
                    dyn = true;
            }

            if (dyn)
            {
                AvailableChannels.Add("BrakeTemp");
                for (int i = 0; i < n; i++)
                {
                    points[i].BrakeTempFL = bt[0][i]; points[i].BrakeTempFR = bt[1][i];
                    points[i].BrakeTempRL = bt[2][i]; points[i].BrakeTempRR = bt[3][i];
                }
                
            }
            
        }

        var tp = ReadWheelTable(connStr, "TyresPressure", n);
        if (tp != null)
        {
            AvailableChannels.Add("TyrePress");
            for (int i = 0; i < n; i++)
            {
                points[i].TyrePressFL = tp[0][i]; points[i].TyrePressFR = tp[1][i];
                points[i].TyrePressRL = tp[2][i]; points[i].TyrePressRR = tp[3][i];
            }
        } 

        var rim = ReadWheelTable(connStr, "TyresRimTemp", n);
        if (rim != null)
        {
            AvailableChannels.Add("TyreRim");
            for (int i = 0; i < n; i++)
            {
                points[i].TyreRimFL = rim[0][i]; points[i].TyreRimFR = rim[1][i];
                points[i].TyreRimRL = rim[2][i]; points[i].TyreRimRR = rim[3][i];
            }
        }

        var zIn = ReadWheelTable(connStr, "TyresTempLeft", n);
        var zCt = ReadWheelTable(connStr, "TyresTempCentre", n);
        var zOt = ReadWheelTable(connStr, "TyresTempRight", n);
        if (zIn != null && zCt != null && zOt != null)
        {
            AvailableChannels.Add("TyreZones");
            for (int i = 0; i < n; i++)
            {
                points[i].TyreInFL = zIn[0][i]; points[i].TyreInFR = zIn[1][i];
                points[i].TyreInRL = zIn[2][i]; points[i].TyreInRR = zIn[3][i];
                points[i].TyreCtFL = zCt[0][i]; points[i].TyreCtFR = zCt[1][i];
                points[i].TyreCtRL = zCt[2][i]; points[i].TyreCtRR = zCt[3][i];
                points[i].TyreOtFL = zOt[0][i]; points[i].TyreOtFR = zOt[1][i];
                points[i].TyreOtRL = zOt[2][i]; points[i].TyreOtRR = zOt[3][i];
            }
        } 

    }
}