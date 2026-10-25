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

## The three tabs

The chart area is organized in three tabs, each focused on one subsystem:

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

## Exporting to Excel

Click **Export CSV (Excel)** to write the full-resolution session to a CSV file
(`;` separator). All channels are exported: standard telemetry, hybrid
(SoC, ERS), per-wheel tyre data (temperature carcass/inner/centre/outer, rim
temp, pressure, wear) and per-wheel brake data (temp, wear). Useful for custom
analysis, pivot tables or overlay charts.

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