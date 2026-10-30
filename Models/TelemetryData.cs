using System.Windows.Media;
using System.ComponentModel;

namespace TraceEngineer.Models;

public class TelemetrySession
{
    public List<TelemetryPoint> Points { get; set; } = new();
    public List<double> ThrottleValues { get; set; } = new();
    public List<double> BrakeValues { get; set; } = new();
    public List<LapMarker> Laps { get; set; } = new();
    public string DebugInfo { get; set; } = string.Empty;
}

public class TelemetryPoint
{
    public double Time { get; set; }
    public double Distance { get; set; }
    public double Speed { get; set; }
    public double Steering { get; set; }
    public double RPM { get; set; }
    public double Throttle { get; set; }
    public double Brake { get; set; }
    public double SoC { get; set; }
    public double ErsPower { get; set; }        // %/s: positivo = deploy, negativo = regen

    public double TyreTempFL { get; set; }
    public double TyreTempFR { get; set; }
    public double TyreTempRL { get; set; }
    public double TyreTempRR { get; set; }

    public double TyreWearFL { get; set; }
    public double TyreWearFR { get; set; }
    public double TyreWearRL { get; set; }
    public double TyreWearRR { get; set; }

    public double BrakeWearFL { get; set; }
    public double BrakeWearFR { get; set; }
    public double BrakeWearRL { get; set; }
    public double BrakeWearRR { get; set; }
    public double BrakeTempFL { get; set; }
    public double BrakeTempFR { get; set; }
    public double BrakeTempRL { get; set; }
    public double BrakeTempRR { get; set; }
    public double TyrePressFL { get; set; }
    public double TyrePressFR { get; set; }
    public double TyrePressRL { get; set; }
    public double TyrePressRR { get; set; }
        // Rim temperature
    public double TyreRimFL { get; set; }
    public double TyreRimFR { get; set; }
    public double TyreRimRL { get; set; }
    public double TyreRimRR { get; set; }
    // Battistrada: inner / centre / outer (da TyresTempLeft/Centre/Right)
    public double TyreInFL { get; set; }
    public double TyreInFR { get; set; }
    public double TyreInRL { get; set; }
    public double TyreInRR { get; set; }
    public double TyreCtFL { get; set; }
    public double TyreCtFR { get; set; }
    public double TyreCtRL { get; set; }
    public double TyreCtRR { get; set; }
    public double TyreOtFL { get; set; }
    public double TyreOtFR { get; set; }
    public double TyreOtRL { get; set; }
    public double TyreOtRR { get; set; }
    // Coordinate mondo proiettabili sulla track map (popolate dal reader)
    public double MapX { get; set; }
    public double MapY { get; set; }    
}

public class LapMarker
{
    public double Time { get; set; }
    public int LapNumber { get; set; }
}



// ---- Giro salvato su disco (JSON) ----
public class SavedLap
{
    public int Version { get; set; } = 1;
    public string TrackName { get; set; } = "";
    public string CarName { get; set; } = "";
    public string CarClass { get; set; } = "";
    public int LapNumber { get; set; }
    public double LapTimeSeconds { get; set; }
    public string LapDateIso { get; set; } = "";
    public string SavedAtIso { get; set; } = "";
    public string Note { get; set; } = "";
    public List<SavedPoint> Points { get; set; } = new();
}

public class SavedPoint
{
    public double T { get; set; }
    public double S { get; set; }
    public double Th { get; set; }
    public double Br { get; set; }
    public double St { get; set; }
    public double R { get; set; }
    public double SoC { get; set; }
    public double Ers { get; set; }

    public double Tt1 { get; set; }
    public double Tt2 { get; set; }
    public double Tt3 { get; set; }
    public double Tt4 { get; set; }

    public double Tw1 { get; set; }
    public double Tw2 { get; set; }
    public double Tw3 { get; set; }
    public double Tw4 { get; set; }

    public double Bw1 { get; set; }
    public double Bw2 { get; set; }
    public double Bw3 { get; set; }
    public double Bw4 { get; set; }
    public double Bt1 { get; set; }
    public double Bt2 { get; set; }
    public double Bt3 { get; set; }
    public double Bt4 { get; set; }
    public double Tp1 { get; set; }
    public double Tp2 { get; set; }
    public double Tp3 { get; set; }
    public double Tp4 { get; set; } 
    public double Rt1 { get; set; }
    public double Rt2 { get; set; }
    public double Rt3 { get; set; }
    public double Rt4 { get; set; }
    public double Zi1 { get; set; }
    public double Zi2 { get; set; }
    public double Zi3 { get; set; }
    public double Zi4 { get; set; }
    public double Zc1 { get; set; }
    public double Zc2 { get; set; }
    public double Zc3 { get; set; }
    public double Zc4 { get; set; }
    public double Zo1 { get; set; }
    public double Zo2 { get; set; }
    public double Zo3 { get; set; }
    public double Zo4 { get; set; }       
}
public class ComparisonEntry
{
    public string Label { get; set; } = "";
    public string TrackName { get; set; } = "";
    public string CarName { get; set; } = "";
    public List<TelemetryPoint> Points { get; set; } = new();
    public bool IsSaved { get; set; }
    public object? SourceItem { get; set; }
}

public class LapInfo : INotifyPropertyChanged
{
    public int LapNumber { get; set; }
    public double LapTime { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }

    public string LapTimeFormatted =>
        TimeSpan.FromSeconds(LapTime).ToString(@"hh\:mm\:ss\.fff");

    private SolidColorBrush _rowBg = new SolidColorBrush(Colors.Transparent);
    public SolidColorBrush RowBg
    {
        get => _rowBg;
        set
        {
            _rowBg = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowBg)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class SavedLapListItem : INotifyPropertyChanged
{
    public string FilePath { get; set; } = "";
    public string TrackName { get; set; } = "";
    public string CarName { get; set; } = "";
    public string DisplayDate { get; set; } = "";
    public string LapTimeFormatted { get; set; } = "";
    public int LapNumber { get; set; }

    private SolidColorBrush _rowBg = new SolidColorBrush(Colors.Transparent);
    public SolidColorBrush RowBg
    {
        get => _rowBg;
        set
        {
            _rowBg = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowBg)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}