# TraceEngineer

Desktop telemetry analysis tool for sim racing.

It reads DuckDB telemetry logs (as produced by Le Mans Ultimate and other
simulators), renders lap-by-lap channel traces in MoTeC-style lanes, lets you
build a personal library of reference laps, and compares laps across
sessions and cars — either against time or against distance, with automatic
time-delta computation.

> ⚠️ **Disclaimer:** this is an unofficial, community-made tool.
> It is not affiliated with, endorsed by, or supported by any game developer.
> Use at your own risk.

## Features

### Session analysis
- Opens LMU `.duckdb` telemetry files directly (no export step needed)
- Automatic session parsing with lap list and lap times (`hh:mm:ss.fff`)
- Session header showing detected track name and date

### Visualization
- MoTeC-style lane chart (Speed, Throttle, Brake, Steering, RPM) drawn with
  pure WPF — no charting dependencies, no native-renderer pitfalls
- **Time or Distance X-axis**: switch between time-based and distance-based
  analysis. Distance mode aligns braking points and corner exits across laps
- Peak-preserving downsampling (min/max bucketing): brake spikes and rev
  limiter cuts are never lost in the rendered trace

### Telemetry channels

**Standard channels:**
- Speed (km/h)
- Throttle (%)
- Brake (%)
- Steering (raw)
- RPM

**Hybrid system (Hypercar only):**
- SoC (State of Charge, 0–100%)
- ERS power (derived from SoC slope: positive = deploy, negative = regen)

**Per-wheel channels:**
- Tyre temperature (carcass temp, °C) - FL/FR/RL/RR
- Tyre wear (tread wear) - FL/FR/RL/RR
- Brake temperature (disc/pad temp, °C) - FL/FR/RL/RR
- Brake wear (thickness, auto-detected when available) - FL/FR/RL/RR

**Display features:**
- Hide shift cuts filter (removes gear-shift throttle cuts and downshift blips)
- °C scale on temperature lanes
- Overlapping wheel traces with color blending (higher saturation = coincident lines)
- Wheel labels (FL/FR/RL/RR) at line start for easy identification

### Lap comparison
- Compare up to 2 laps: session vs session, session vs saved, saved vs saved
- **Automatic time-delta trace** (distance mode): shows where lap 2 gains or
  loses time relative to lap 1, with final gap readout
- Track-name validation when mixing laps from different sources
- Click-to-toggle lap selection with colored bars; a third lap is rejected
  with clear feedback (no hidden selection state)
- Vertical cursor with time/distance readout for precise event alignment

### Reference lap library
- Save reference laps to a local JSON library with track / car / class / date
  metadata
- Saved-laps list automatically filtered to the current track
- Delete saved laps you no longer need

### Export
- Full-resolution CSV export (Excel-friendly, `;` separator)
- All channels included: standard telemetry + hybrid (SoC, ERS) + per-wheel
  (temperatures, wear) for comprehensive offline analysis
- Compatible with Excel pivot tables, custom charts, and third-party tools

## Typical workflows

**Find where a setup change helped:** save a reference lap with the old setup,
load the new session, click both laps, switch to Distance mode, and use the
vertical cursor to pinpoint exactly where you gain or lose time.

**Compare LMH vs LMDh:** save a lap from each class on the same track, click
both, use Distance mode to see where each prototype is stronger. Use Tyre temp
and Brake temp to compare thermal management.

**Analyse energy management (Hypercar):** enable SoC and ERS power to see where
you deploy (positive ERS, SoC dropping) and where you regen (negative ERS, SoC
rising). Compare laps to optimize deployment strategy.

**Check tyre thermal windows:** enable Tyre temp to verify your tyres stay in
the optimal range (typically 90–110 °C carcass for slicks). The per-wheel
breakdown shows left/right imbalances that indicate setup issues.

See the [docs folder](docs/) for the full documentation:
- [DuckDB schema](docs/LMU_DUCKDB_SCHEMA.md)
- [Analysis techniques](docs/ANALYSIS_TECHNIQUES.md)
- [User guide](docs/USER_GUIDE.md)

## Requirements

- Windows 10 / 11 (64-bit)
- .NET 10 SDK (to build) **or** just download the release zip (no .NET needed)
- Le Mans Ultimate (to generate telemetry)

## Download (no build required)

Go to [Releases](https://github.com/MichaelArms/LMUTelemetry/releases), download
the latest `LMUTelemetry-v0.4.0-win-x64.zip`, extract it anywhere and run
`TraceEngineer.exe`. The build is self-contained: no .NET installation
required.

## Build from source

```bash
git clone https://github.com/MichaelArms/LMUTelemetry.git
cd LMUTelemetry
dotnet build
dotnet run