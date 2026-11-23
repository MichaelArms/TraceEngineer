# Le Mans Ultimate — DuckDB Telemetry Schema (reverse engineered)

Le Mans Ultimate writes one [DuckDB](https://duckdb.org) database per session:

<SteamLibrary>/steamapps/common/Le Mans Ultimate/UserData/Telemetry/<Track>_<date>.duckdb


Example file name:
`Autódromo José Carlos Pace_P_2026-09-20T15_17_52Z.duckdb`

The database uses a **long format**: each telemetry channel is stored in its
**own table**, sampled at its own frequency. A typical session contains ~100
tables.

---

## Design overview

- **One table per channel.** Each table holds the samples for a single signal.
- **Per-channel sampling rate.** Fast signals (speed, steering) are sampled at
  full rate; slow signals (ambient temperature) at ~1 Hz. This keeps files
  compact.
- **Master clock.** The `GPS Time` table is the time reference for the
  highest-rate signals.
- **Event tables.** Some tables store sparse *events* (lap boundaries, gear
  shifts, flags) with an explicit `ts` timestamp column.

Two table layouts exist:

| Layout | Columns | Used for |
|--------|---------|----------|
| **Continuous single-value** | `value` | Speed, RPM, throttle, ... |
| **Continuous multi-value** | `value1..value4` | Per-wheel / per-corner data |
| **Event / state** | `ts`, `value` | Lap markers, gear changes, flags, best times |

---

## Sampling tiers

Sample counts from a real ~317 s session (3 laps). The tiers are consistent
ratios of the full rate (~100 Hz):

| Rows   | ~Rate   | Example tables |
|--------|---------|----------------|
| 31719  | ~100 Hz | GPS Time, Ground Speed, Steering Pos, Engine RPM, Turbo Boost Pressure, Susp Pos, RideHeights, FFB Output, Regen Rate, SoC-related high-rate channels |
| 15860  | ~50 Hz  | Throttle Pos, Brake Pos, Clutch Pos, Brakes Temp/Force/Air Temp, TyresRimTemp |
| 6344   | ~20 Hz  | Fuel Level, SoC, Virtual Energy |
| 3172   | ~10 Hz  | GPS Speed, GPS Latitude/Longitude, Lap Dist, Total Dist, Track Edge, Path Lateral, G Force Lat/Long/Vert, TyresPressure, TyresRubberTemp, Tyres Wear |
| 3171   | ~10 Hz  | Brake Thickness |
| 2226   | ~7 Hz   | Engine Oil Temp, Engine Water Temp |
| 1586   | ~5 Hz   | TyresCarcassTemp, SurfaceTypes |
| 635    | ~2 Hz   | OverheatingState, Time Behind Next |
| 318    | ~1 Hz   | Ambient Temperature, Track Temperature, Wind Speed, Wind Heading |
| few    | event   | Lap, Gear, Lap Time, Sector flags, metadata, ... |

> Rates are inferred from row counts over the session duration and are
> approximate. The full-rate tier tracks the simulation/frame rate.

---

## Key tables

### `GPS Time` — master clock
- Columns: `value` (DOUBLE, seconds)
- Full-rate time reference. Continuous high-rate tables align to it **by row
  order** (row index). This tool aligns by index/rowid rather than by an
  explicit timestamp, because full-rate tables have no `ts` column.

### `Lap` — lap boundaries
- Columns: `ts` (DOUBLE), `value` (USMALLINT)
- One row per completed lap start; `value` is the lap number.

### `metadata` — session metadata
- Columns: `key` (VARCHAR), `value` (VARCHAR)
- ~11 rows of key/value pairs (session-level info).

### `channelsList` — channel registry
- Columns: `channelName` (VARCHAR), `frequency` (INTEGER), `unit` (VARCHAR)
- Lists the available channels with their sampling frequency and unit. Useful
  to discover the exact rate/unit of any channel programmatically.

### `eventsList` — event registry
- Columns: `eventName` (VARCHAR), `unit` (VARCHAR)
- ~40 rows describing the event-type tables.

---

## Full table reference

Grouped by category. **Rows** are from the sample session above.

### Drivetrain & engine
| Table | Rows | Columns |
|-------|------|---------|
| Engine RPM | 31719 | value FLOAT |
| Engine Max RPM | 1 | ts, value FLOAT |
| Clutch RPM | 31719 | value FLOAT |
| Turbo Boost Pressure | 31719 | value FLOAT |
| Engine Oil Temp | 2226 | value FLOAT |
| Engine Water Temp | 2226 | value FLOAT |
| Fuel Level | 6344 | value FLOAT |
| FuelMixtureMap | 1 | ts, value UINTEGER |
| Gear | 274 | ts, value TINYINT |
| Clutch Pos | 15860 | value FLOAT |
| Clutch Pos Unfiltered | 15860 | value FLOAT |

### Driver inputs
| Table | Rows | Columns |
|-------|------|---------|
| Throttle Pos | 15860 | value FLOAT |
| Throttle Pos Unfiltered | 15860 | value FLOAT |
| Brake Pos | 15860 | value FLOAT |
| Brake Pos Unfiltered | 15860 | value FLOAT |
| Steering Pos | 31719 | value FLOAT |
| Steering Pos Unfiltered | 31719 | value FLOAT |
| Steering Shaft Torque | 31719 | value FLOAT |
| Brake Bias Rear | 2 | ts, value FLOAT |
| Brake Migration | 1 | ts, value FLOAT |

### Motion & position
| Table | Rows | Columns |
|-------|------|---------|
| GPS Time | 31719 | value DOUBLE |
| Ground Speed | 31719 | value FLOAT |
| GPS Speed | 3172 | value FLOAT |
| GPS Latitude | 3172 | value FLOAT |
| GPS Longitude | 3172 | value FLOAT |
| Lap Dist | 3172 | value FLOAT |
| Total Dist | 3172 | value FLOAT |
| Track Edge | 3172 | value FLOAT |
| Path Lateral | 3172 | value FLOAT |
| G Force Lat | 3172 | value FLOAT |
| G Force Long | 3172 | value FLOAT |
| G Force Vert | 3172 | value FLOAT |

### Suspension & aero
| Table | Rows | Columns |
|-------|------|---------|
| Susp Pos | 31719 | value1..4 FLOAT |
| RideHeights | 31719 | value1..4 FLOAT |
| FrontRideHeight | 31719 | value FLOAT |
| RearRideHeight | 31719 | value FLOAT |
| Front3rdDeflection | 31719 | value FLOAT |
| Rear3rdDeflection | 31719 | value FLOAT |
| FrontFlapActivated | 1 | ts, value BOOLEAN |
| RearFlapActivated | 1 | ts, value BOOLEAN |
| RearFlapLegalStatus | 1 | ts, value BOOLEAN |

### Tyres (per-wheel: value1..value4)
| Table | Rows | Columns |
|-------|------|---------|
| TyresPressure | 3172 | value1..4 FLOAT |
| Tyres Wear | 3172 | value1..4 FLOAT |
| TyresRubberTemp | 3172 | value1..4 FLOAT |
| TyresCarcassTemp | 1586 | value1..4 FLOAT |
| TyresRimTemp | 15860 | value1..4 FLOAT |
| TyresTempLeft | 31719 | value1..4 FLOAT |
| TyresTempCentre | 31719 | value1..4 FLOAT |
| TyresTempRight | 31719 | value1..4 FLOAT |
| TyresCompound | 1 | ts, value1..4 UINTEGER |
| SurfaceTypes | 1586 | value1..4 UTINYINT |
| Wheel Speed | 31719 | value1..4 FLOAT |
| WheelsDetached | 1 | ts, value1..4 UTINYINT |

> *Assumed* wheel order for value1..4: FL, FR, RL, RR (unverified).

### Brakes (per-wheel: value1..value4)
| Table | Rows | Columns |
|-------|------|---------|
| Brakes Temp | 15860 | value1..4 FLOAT |
| Brakes Force | 15860 | value1..4 FLOAT |
| Brakes Air Temp | 15860 | value1..4 FLOAT |
| Brake Thickness | 3171 | value1..4 FLOAT |

### Hybrid / energy
| Table | Rows | Columns |
|-------|------|---------|
| SoC | 6344 | value FLOAT |
| Virtual Energy | 6344 | value FLOAT |
| Regen Rate | 31719 | value FLOAT |

### Timing & laps
| Table | Rows | Columns |
|-------|------|---------|
| Lap | 3 | ts, value USMALLINT |
| Lap Time | 2 | ts, value FLOAT |
| Current LapTime | 2 | ts, value FLOAT |
| Best LapTime | 1 | ts, value FLOAT |
| Current Sector | 9 | ts, value UTINYINT |
| Current Sector1 | 3 | ts, value FLOAT |
| Current Sector2 | 3 | ts, value FLOAT |
| Last Sector1 | 3 | ts, value FLOAT |
| Last Sector2 | 2 | ts, value FLOAT |
| Best Sector1 | 1 | ts, value FLOAT |
| Best Sector2 | 1 | ts, value FLOAT |
| Sector1 Flag | 5 | ts, value UTINYINT |
| Sector2 Flag | 3 | ts, value UTINYINT |
| Sector3 Flag | 1 | ts, value UTINYINT |
| Time Behind Next | 635 | value FLOAT |

### Electronics & assists
| Table | Rows | Columns |
|-------|------|---------|
| TC | 31719 | value BOOLEAN |
| TCLevel | 1 | ts, value UTINYINT |
| TCCut | 1 | ts, value UTINYINT |
| TCSlipAngle | 1 | ts, value UTINYINT |
| ABS | 1 | ts, value BOOLEAN |
| ABSLevel | 1 | ts, value UTINYINT |
| LaunchControlActive | 1 | ts, value BOOLEAN |
| AntiStall Activated | 1 | ts, value BOOLEAN |
| Speed Limiter | 2 | ts, value BOOLEAN |
| OverheatingState | 635 | value BOOLEAN |

### Environment & race state
| Table | Rows | Columns |
|-------|------|---------|
| Ambient Temperature | 318 | value FLOAT |
| Track Temperature | 318 | value FLOAT |
| Wind Speed | 318 | value FLOAT |
| Wind Heading | 318 | value FLOAT |
| CloudDarkness | 1 | ts, value BOOLEAN |
| Minimum Path Wetness | 1 | ts, value FLOAT |
| OffpathWetness | 1 | ts, value BOOLEAN |
| Yellow Flag State | 1 | ts, value UTINYINT |
| In Pits | 2 | ts, value UTINYINT |
| Finish Status | 1 | ts, value UTINYINT |
| Headlights State | 1 | ts, value BOOLEAN |
| LastImpactMagnitude | 8 | ts, value BOOLEAN |

### Force feedback & misc
| Table | Rows | Columns |
|-------|------|---------|
| FFB Output | 31719 | value FLOAT |
| metadata | 11 | key, value VARCHAR |
| channelsList | 58 | channelName, frequency, unit |
| eventsList | 40 | eventName, unit |

---

## Units

| Quantity | Table | Unit |
|----------|-------|------|
| Speed | Ground Speed, GPS Speed | km/h (Ground Speed confirmed; GPS Speed assumed same) |
| Throttle / Brake / Clutch | * Pos | 0..1 |
| Temperatures | * Temp | °C *(assumed)* |
| Pressures | TyresPressure, Turbo Boost | kPa *(assumed)* |
| Steering | Steering Pos | raw *(assumed rad; unverified)* |
| Distance | Lap Dist, Total Dist | m *(assumed)* |
| Time / timestamps | GPS Time, ts | s |

> Units marked *assumed* are inferred, not officially documented.

---

## Alignment strategy (used by this tool)

1. Read `GPS Time` ordered by row index → master timeline.
2. Read each full-rate channel ordered by row index → align **by index**.
3. Read half-rate channels (Throttle/Brake, 15860 rows) → each value covers
   two master ticks (`i → 2i, 2i+1`).
4. Lap boundaries from `Lap.ts`.
5. Distance is then derived by integrating speed (see
   [ANALYSIS_TECHNIQUES.md](ANALYSIS_TECHNIQUES.md)).

---

## Reading the files yourself

DuckDB files can be opened by any DuckDB client. Example with the DuckDB CLI:

```sql
-- list tables
SHOW TABLES;

-- inspect one channel
SELECT * FROM "Ground Speed" LIMIT 20;

-- channel registry
SELECT * FROM channelsList;


---

## 3. `docs/ANALYSIS_TECHNIQUES.md`

```markdown
# Analysis Techniques

How TraceEngineer processes the raw DuckDB data.

## 1. Multi-rate alignment

LMU samples each channel at its own rate. To plot channels together, the tool
aligns them onto the full-rate timeline:

- **Full-rate channels** (31719 samples) are aligned by row index against
  `GPS Time`.
- **Half-rate channels** (Throttle, Brake — 15860 samples) are expanded so each
  sample covers two consecutive master ticks.

## 2. Distance integration

Instead of using the low-rate `Lap Dist` table (~10 Hz), the tool integrates
`Ground Speed` (m/s, full rate) over time with the trapezoidal rule:

dist[i] = dist[i-1] + 0.5 * (speed[i-1] + speed[i]) * dt


This yields a per-sample distance accurate to within a few centimetres, used as
the X-axis in **distance mode**. Distance mode aligns braking points and corner
exits across laps even when drivers brake at different spots.

## 3. Peak-preserving downsampling

Rendering 30k+ points per channel is wasteful and slow. Naive decimation,
however, can drop narrow spikes (brake peaks, rev-limiter cuts). The tool uses
**min/max bucketing**:

- The trace is split into N buckets (default ~2200).
- For each bucket, both the minimum and maximum samples are kept, in time order.

This preserves the envelope of the signal — including sharp peaks — while
reducing the point count by more than an order of magnitude.

## 4. Lap time delta

When two laps are compared in distance mode, the tool computes the time delta
as a function of distance:

delta(d) = t_lap2(d) - t_lap1(d)


Both laps' time-vs-distance curves are linearly interpolated onto a common
distance axis (limited to the shorter lap's length).

- **Positive** values → lap 2 is slower at that point.
- **Negative** values → lap 2 is faster.
- The **final sample** gives the total lap-time gap.

The delta is drawn as a dedicated lane with a zero reference line.