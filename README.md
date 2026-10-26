# RPSP research source

RAN Privacy Sentinel Protocol (RPSP) studies local, passive, privacy-minimizing radio-access anomaly indicators from user-equipment observation records. This repository contains the research implementation, deterministic synthetic inputs, tests, experiment manifest and analysis code.

The evidence concerns defined replay scenarios and synthetic stress traces. It does not establish real-world attack detection, field false-positive rates, attacker attribution, or deployment readiness. The Android project is an adapter skeleton, not a validated device acquisition application. No participant data or live radio traces are included.

## Contents

- `src/`: Core, Evaluation, CLI, Android adapter, Privacy and in-memory Persistence.
- `tests/`: unit, golden-report, replay, stress and experiment checks.
- `docs/evaluation/`: experiment manifest and protocol. The proposed physical-device study in the protocol has not been completed.
- `tools/article/analyze_experiment_results.py`: deterministic aggregation and bootstrap calculations.
- `source-manifest.json`: SHA-256 of the unchanged computational source files.

This is a curated source snapshot. Private manuscripts, author and university documents, internal audits, access credentials, build caches and private Git history are excluded. The internal label `v10` in experiment filenames identifies the frozen experiment definition; it is not the current manuscript version.

## Reproduce

Use .NET SDK 10.0.400 (the validated runtime was .NET 10.0.11 on Windows) and Python 3. The analysis script uses the Python standard library. NuGet dependencies are recorded in `packages.lock.json`. `global.json` permits later patches; use the exact validated SDK when comparing environments.

Run from the repository root in PowerShell. The result directory must be new; do not overwrite an earlier run.

```powershell
dotnet --info
dotnet restore RPSP.sln --locked-mode
dotnet build RPSP.sln -c Release --no-restore
dotnet test RPSP.sln -c Release --no-build -m:1
dotnet src/Rpsp.Cli/bin/Release/net10.0/Rpsp.Cli.dll run-scenario StationaryStableServingCell
dotnet src/Rpsp.Cli/bin/Release/net10.0/Rpsp.Cli.dll run-stress LongStableUrbanBaseline
$out = Join-Path (Get-Location) ('results-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
if (Test-Path -LiteralPath $out) { throw 'Choose a new output directory' }
$env:RPSP_GIT_COMMIT = (git rev-parse HEAD).Trim()
$env:RPSP_DOTNET_SDK = (dotnet --version).Trim()
dotnet src/Rpsp.Cli/bin/Release/net10.0/Rpsp.Cli.dll run-experiment docs/evaluation/computers-security-v10-experiment-manifest.json $out
dotnet src/Rpsp.Cli/bin/Release/net10.0/Rpsp.Cli.dll validate-results docs/evaluation/computers-security-v10-experiment-manifest.json $out
python tools/article/analyze_experiment_results.py $out
```

The fixed design uses 36 replay scenarios, 25 stress traces, 30 replicates, three scales and eight profiles, producing 196,560 records. Base seed: 20260905; analysis bootstrap seed: 20260905; 1,000 bootstrap resamples. Timing is disabled, so these outputs are not performance benchmarks. Unspecified labels are excluded from label-agreement denominators. Bootstrap intervals describe the resampled finite constructed run set; they do not establish population generalization or field accuracy.

Exported revision and runtime metadata must describe the actual rerun. They will differ from the earlier private snapshot metadata even when scientific result columns agree. The source manifest permits file-level comparison without mislabeling the new run as an old commit.

The preparation environment previously experienced intermittent test-host process failures as well as a complete passing run. Retain failure logs and verify the suite locally; no cross-platform reliability guarantee is asserted.

## Availability

The repository is public by the corresponding author's instruction. No reuse license has been selected. Public visibility is not a representation that an open-source license has been granted. No manuscript acceptance, institutional clearance or DOI is asserted.
