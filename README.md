# TraceEngineer

Desktop telemetry analysis tool for sim racing.

It reads DuckDB telemetry logs (as produced by Le Mans Ultimate and other
simulators), renders lap-by-lap channel traces in MoTeC-style lanes, lets you
build a personal library of reference laps, and compares laps across sessions
and cars — either against time or against distance, with automatic time-delta
computation.

> ⚠️ **Disclaimer:** this is an unofficial, community-made tool.
> It is not affiliated with, endorsed by, or supported by any game developer.
> Use at your own risk.

## Features

### Session analysis
- Opens DuckDB telemetry files directly (no export step needed)
- Automatic session parsing with lap list and lap times (`hh:mm:ss.fff`)
- Session header showing detected track name, date and car name

### Four-tab layout
- **Car**: speed, throttle, brake, steering, RPM, hide shift cuts, SoC, ERS power
- **Tyres**: four compound lanes (one per wheel), each with a temperature
  panel (carcass + inner/centre/outer zones + rim temp) and a pressure/wear panel
- **Brakes**: per-wheel brake temp and brake wear
- **Stint**: lap-by-lap analysis across the entire session (Lap time, Tyre temp, 
  Tyre pressure, Tyre wear, SoC, Brake temp, Speed)
- Tabs with unavailable channels are automatically disabled

### Visualization
- MoTeC-style lane chart drawn with pure WPF — no charting dependencies,
  no native-renderer pitfalls
- **Time or Distance X-axis**: switch between time-based and distance-based
  analysis (Distance is the default). Distance mode aligns braking points
  and corner exits across laps
- **Vertical cursor**: slider-driven reference line across all lanes with
  metres/seconds readout for precise event alignment
- **Track map**: GPS-based track reconstruction in the top-right corner with
  colored dots (one per compared lap) that move along the track in sync with
  the cursor. Automatic detection of GPS tables (lat/lon or multi-axis).
  Fallback to session lap profile when comparing saved laps (no GPS available)
- **Hover probe**: move the mouse over any trace to read the exact value
  at that point, with a snapping dot and overlay showing lap/wheel/channel
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

**Per-wheel tyre channels:**
- Tyre temperature (carcass temp, °C) - FL/FR/RL/RR
- Tyre zones: inner / centre / outer (°C) - FL/FR/RL/RR
- Tyre rim temperature (°C) - FL/FR/RL/RR
- Tyre pressure - FL/FR/RL/RR (unit auto-detected: psi / bar / kPa)
- Tyre wear (tread wear) - FL/FR/RL/RR

**Per-wheel brake channels:**
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

### Reference lap library
- Save reference laps to a local JSON library with track / car / class / date
  metadata
- Saved-laps list automatically filtered to the current track
- Delete saved laps you no longer need
- Saved laps are stored in `%APPDATA%\TraceEngineer\SavedLaps` and are
  **automatically migrated** from the previous location on first launch

### Export
- **Export Session (CSV)**: exports the entire session with 45 columns
- Correct column alignment (Lap, Time, Speed, Throttle, Brake, Steering, RPM, SoC, ERS, then all tyre/brake data grouped by metric)
- Semicolon-separated for Excel compatibility in European locales
- UTF-8 encoding with proper formatting (0.## for most values, 0.#### for time/steering)

### UI polish
- Thin (6px) scrollbars that appear on hover over lap lists, invisible at rest for a cleaner UI
- Custom dark theme matching the app's palette
- Stint metrics use checkbox selection instead of dropdown for faster toggling

## Typical workflows

**Find where a setup change helped:** save a reference lap with the old setup,
load the new session, click both laps, stay in Distance mode, and use the
vertical cursor + hover probe to pinpoint exactly where you gain or lose time.

**Analyse a tyre (Tyres tab):** read one compound lane top-to-bottom.
If inner runs much hotter than outer, camber is too negative; if centre is
cooler than shoulders, pressure is too low. High rim temp with high carcass
suggests brake heat transfer (check ducting), not just cornering load.
Pressure climbing too much over the lap indicates cold pressure too high or
excessive carcass heating. Correlate wear with temperature spikes to find
abusive corners.

**Compare LMH vs LMDh:** save a lap from each class on the same track, click
both, use Distance mode to see where each prototype is stronger. Use the
Tyres tab to compare thermal management and the Car tab (SoC/ERS) to compare
deployment strategy.

**Energy management (Hypercar):** enable SoC and ERS power in the Car tab to
see where you deploy (positive ERS, SoC dropping) and where you regen
(negative ERS, SoC rising). Compare laps to optimize deployment strategy.

**Analyse a stint (Stint tab):** load a session, switch to Stint tab, enable "Tyre temp" and "Tyre pressure" to see how temperatures and pressures evolve lap-by-lap. Use "Exclude anomalous" to filter out out-laps and pit stops. Hover over any point to read exact values for that lap.

**Track position analysis:** enable the track map (appears automatically when GPS data is available). Drag the cursor slider to see where each lap is on the circuit. Compare two laps to see if one driver brakes earlier or carries more speed through specific corners.

See the [docs folder](docs/) for the full documentation:
- [DuckDB schema](docs/LMU_DUCKDB_SCHEMA.md)
- [Analysis techniques](docs/ANALYSIS_TECHNIQUES.md)
- [User guide](docs/USER_GUIDE.md)

## Requirements

- Windows 10 / 11 (64-bit)
- .NET 10 SDK (to build) **or** just download the release zip (no .NET needed)
- A simulator that exports DuckDB telemetry (e.g. Le Mans Ultimate)

## Download (no build required)

Go to [Releases](https://github.com/MichaelArms/TraceEngineer/releases),
download the latest `TraceEngineer-vX.Y.Z-win-x64.zip`, extract it anywhere
and run `TraceEngineer.exe`. The build is self-contained: no .NET
installation required.

**Upgrading from LMU Telemetry:** on first launch, TraceEngineer
automatically moves your saved laps from
`%APPDATA%\LmuTelemetry\SavedLaps` to `%APPDATA%\TraceEngineer\SavedLaps`.
No action needed on your side.

## Build from source

```bash
git clone https://github.com/MichaelArms/TraceEngineer.git
cd TraceEngineer
dotnet build
dotnet run