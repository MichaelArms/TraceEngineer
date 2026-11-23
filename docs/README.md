# TraceEngineer — Documentation

This folder contains the technical documentation for TraceEngineer.

| Document | Description |
|----------|-------------|
| [LMU_DUCKDB_SCHEMA.md](LMU_DUCKDB_SCHEMA.md) | Full reverse-engineered schema of the Le Mans Ultimate DuckDB telemetry files: table layout, sampling tiers, data types, units and alignment strategy. |
| [ANALYSIS_TECHNIQUES.md](ANALYSIS_TECHNIQUES.md) | How the tool processes the data: distance integration, peak-preserving downsampling and lap time-delta computation. |
| [USER_GUIDE.md](USER_GUIDE.md) | Step-by-step guide to using the application. |

## Quick orientation

- **New to the tool?** Start with the [User Guide](USER_GUIDE.md).
- **Want to read LMU telemetry yourself** (Python, MATLAB, MoTeC, ...)? The
  [DuckDB schema](LMU_DUCKDB_SCHEMA.md) is what you need.
- **Curious about the math** behind distance mode and the delta trace? See
  [Analysis Techniques](ANALYSIS_TECHNIQUES.md).

> The schema documentation is the result of reverse engineering and is provided
> as-is. Some details (marked *assumed* or *unverified*) may change with game
> updates.