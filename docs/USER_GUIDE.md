# User Guide

## First run

1. Download the latest release zip and extract it anywhere.
2. Run `TraceEngineer.exe`. No .NET installation is required.

## Loading a session

1. Click **Open .duckdb file**.
2. Select a file from `Le Mans Ultimate/UserData/Telemetry/`.
3. The session is parsed automatically: the lap list appears on the left, with
   the detected **track name and date** shown above it.

## Selecting laps (the comparison model)

- **Click a lap** in either list to add it to the comparison: the row gets a
  **colored bar** (cyan = lap 1, orange = lap 2).
- **Click a barred lap again** to remove it from the comparison.
- The comparison holds **at most 2 laps**: clicking a third lap shows a message
  and does nothing. Deselect one first.
- The colored bars are the only selection indicator: what you see barred is
  exactly what is being compared.

## The four tabs

The chart area is organized in four tabs, each focused on one subsystem or
analysis mode:

### Car tab (default)
Vehicle dynamics and powertrain channels:
- **Speed, Throttle, Brake** (on by default)
- **Steering, RPM** (optional)
- **Hide shift cuts** (on by default): removes gear-shift throttle cuts and
  downshift blips for readability
- **SoC** (State of Charge) and **ERS power** (deploy/regen) for hybrid analysis

### Tyres tab
Automatically shows **four compound lanes**, one per wheel (FL, FR, RL, RR).
Each compound lane has two stacked panels:
- **Top panel — temperature (°C)**: carcass (thick white), inner (red),
  centre (yellow), outer (green), rim (dashed orange)
- **Bottom panel — pressure / wear**: pressure (cyan) and wear (dashed magenta)

This is the "one lane per tyre" view: everything about a single wheel lives
together, so you can correlate temperature, pressure and wear at a glance.
In comparison mode (2 laps) the top panel shows carcass for both laps
(cyan/orange) and the bottom panel shows pressure + wear for both laps.

### Brakes tab
- **Brake temp** per wheel (FL/FR/RL/RR, 4 overlapping lines)
- **Brake wear** per wheel (only when LMU logs dynamic wear)

Tabs that have no data in the current session are automatically disabled
(greyed out).

### Stint tab
Lap-by-lap analysis across the entire session, with one lane per metric
(Lap time, Tyre temp, Tyre pressure, Tyre wear, SoC, Brake temp, Speed).
See the dedicated [Stint Analysis](#stint-analysis-stint-tab) section below
for full details on metrics, filters and use cases.

## Vertical cursor

A slider below the chart controls a vertical cursor line:
- Drag the slider to move the line across all lanes simultaneously
- The label at the top shows the exact position in **metres** (Distance mode)
  or **seconds** (Time mode)
- Use this to align events (braking points, apexes, gear shifts) across all
  channels at once
- The cursor appears only when at least one lap is selected

## Hover probe

Move the mouse over any trace to read the exact value at that point:
- A colored dot snaps to the nearest trace and an overlay shows
  **lap · wheel · channel**, the X position (m or s) and the value with unit
- On compound lanes the probe identifies the specific series
  (e.g. `FL inner`, `RR pressure`)
- On the Δ Time lane the probe shows the signed gap (e.g. `+0.142 s`)
- Moving away from traces hides the probe

## Track Map

When a session with GPS data is loaded, a small track map appears in the top-right corner of the window. The map shows:

- **Track profile**: a polyline representing one complete lap, reconstructed from GPS coordinates
- **Start/Finish marker**: a white dot at the start of the lap
- **Lap position dots**: colored dots (one per compared lap) that move along the track in sync with the cursor slider

### How it works

The app automatically detects GPS tables in the DuckDB file. For Le Mans Ultimate, it reads `GPS Latitude` and `GPS Longitude` tables and applies a `cos(latitude)` correction to make X/Y proportional to metres.

The track profile is built from the longest complete lap in the session (excluding out-laps and pit laps). When comparing saved laps (which don't have GPS), the app uses the session lap profile as a fallback and positions the dots based on distance fraction.

### Use cases

- **Corner analysis**: drag the cursor to a specific corner and see where each lap is on the circuit
- **Sector comparison**: compare two laps and watch the dots diverge in sectors where one driver is faster
- **Pit stop detection**: in stint analysis, the dot will disappear during pit laps (if they're filtered out)

## Time vs Distance axis

- **Distance** (default): X-axis is metres from the start/finish line. Aligns
  braking points and corner exits across laps.
- **Time**: X-axis is seconds from lap start.

## Comparing two laps

- Each lap is drawn in its own color on every lane (cyan = lap 1,
  orange = lap 2), MoTeC-style.
- Temperature channels show **°C scale** on the left side of the lane.
- The legend under the chart shows a line sample and the lap time for each lap
  (e.g. `L5 • 00:01:44.812`).
- In **Distance** mode with 2 laps, an extra **Δ Time** lane (magenta) appears
  at the top: positive = lap 2 slower at that point; the header shows the final
  gap.

## Saving a reference lap

1. Click the session lap you want to save (it gets a colored bar). Keep only
   one session lap in the comparison for saving.
2. Click **Save selected lap**.
3. Confirm/edit track, car, class, date and notes, then click **Save**.
4. The lap is stored as JSON in `%APPDATA%/TraceEngineer/SavedLaps/` and appears
   in the **Saved laps** list, filtered to the current track. All channels
   (including hybrid, tyre and brake data) are preserved in the saved file.

## Deleting a saved lap

1. Click the saved lap(s) you want to delete (they get colored bars; max 2).
2. Click **Delete selected lap** and confirm. The JSON file is permanently
   removed.

## Stint Analysis (Stint tab)

The Stint tab provides a lap-by-lap view of the entire session, ideal for analyzing degradation, fuel consumption, and strategy.

### Available metrics

Enable one or more metrics with the checkboxes:

- **Lap time**: total time for each lap (white line)
- **Tyre temp**: average carcass temperature per lap, one line per wheel (FL=cyan, FR=yellow, RL=green, RR=red)
- **Tyre pressure**: average pressure per lap, one line per wheel
- **Tyre wear**: wear at end of lap, one line per wheel
- **SoC**: State of Charge at start and end of lap (purple=start, green=end)
- **Brake temp**: average brake temperature per lap, one line per wheel
- **Speed**: average and maximum speed per lap (yellow=avg, cyan=max)

### Lane layout

Each metric gets its own horizontal lane with:
- Independent Y-axis scale (shown on the left)
- Title and unit label
- Alternating background colors for visual separation
- Legend at the bottom showing color coding

### Exclude anomalous laps

Enable "Exclude anomalous" to filter out:
- Out-laps and in-laps (laps shorter than 50% of median lap time)
- Pit stops and warmup laps (laps longer than 115% of median lap time)

This leaves only "flying laps" for cleaner trend analysis.

### Hover probe

Move the mouse over any lane to see the exact value for that lap, including:
- Metric name and wheel position (e.g., "Tyre temp FL")
- Lap number
- Formatted value with unit (e.g., "96.4 °C" or "1:42.385")

### Use cases

**Tyre degradation**: enable "Tyre temp" and "Tyre wear" to see how temperatures and wear evolve over a stint. If temps rise while wear accelerates, the compound is overheating.

**Fuel effect on lap time**: enable "Lap time" and "SoC end" to see if lap times improve as fuel burns off (lighter car) or worsen due to tyre degradation.

**Brake management**: enable "Brake temp" to monitor brake temperatures across a stint. If temps climb lap after lap, consider brake duct adjustments or lifting points.   

## Export Session (CSV)

Click "Export Session (CSV)" in the toolbar to export the entire session as a semicolon-separated CSV file, optimized for Excel in European locales.

### Column structure (45 columns total)

| Group | Columns |
|-------|---------|
| Identifiers | `Lap`, `Time_s` |
| Dynamics | `Speed_kmh`, `Throttle_pct`, `Brake_pct`, `Steering`, `RPM` |
| Hybrid | `SoC_pct`, `ERS_pct_s` |
| Tyre carcass | `TyreTempFL/FR/RL/RR` |
| Tyre zones | `TyreIn/Ct/Ot` + `FL/FR/RL/RR` (12 columns) |
| Tyre rim | `TyreRimFL/FR/RL/RR` |
| Tyre pressure | `TyrePressFL/FR/RL/RR` |
| Tyre wear | `TyreWearFL/FR/RL/RR` |
| Brake temp | `BrakeTempFL/FR/RL/RR` |
| Brake wear | `BrakeWearFL/FR/RL/RR` |

### Excel usage

1. Open Excel → Data → From Text/CSV
2. Select the exported file
3. Set delimiter to semicolon (`;`)
4. Excel will recognize the structure and create a table

You can then use pivot tables, charts, or conditional formatting to analyze trends.

## Typical workflows

### Find where a setup change helped
Save a reference lap with the old setup, load the new-setup session, click both
laps and stay in Distance mode: the Δ Time lane shows exactly where the new
setup gains or loses. Use the cursor and the probe to pinpoint the exact corner
and the exact channel involved.

### Analyse a tyre (the Tyres tab)
Open the Tyres tab and read one compound lane top-to-bottom:
- **Carcass vs zones**: if inner runs much hotter than outer, camber is too
  negative; if centre is cooler than shoulders, pressure is too low.
- **Rim temp**: high rim temp with high carcass suggests brake heat transfer
  (check ducting), not just cornering load.
- **Pressure rise**: pressure climbing too much over the lap indicates cold
  pressure too high or excessive carcass heating.
- **Wear**: the dashed magenta line shows tread wear accumulating; correlate
  with temperature spikes to find abusive corners.

### Compare LMH vs LMDh
Save a lap from each class on the same track, click both, use Distance mode to
see where each prototype is stronger. Use the Tyres tab to compare thermal
management and the Car tab (SoC/ERS) to compare deployment strategy.

### Energy management (Hypercar)
In the Car tab enable SoC and ERS power: see where you deploy (positive ERS,
SoC dropping) and where you regen (negative ERS, SoC rising). Compare laps to
optimize deployment strategy and avoid running out of battery before the finish.