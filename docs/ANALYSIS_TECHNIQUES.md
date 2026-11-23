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
`Ground Speed` (km/h, full rate, converted to m/s by dividing by 3.6) over time
with the trapezoidal rule:

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