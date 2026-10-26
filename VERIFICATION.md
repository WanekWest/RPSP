# Source release verification

Verified locally on 26 October 2026 using .NET SDK 10.0.400 and runtime 10.0.11 on Windows. All 191 original computational files match the SHA-256 values in `source-manifest.json`.

- Locked restore: passed.
- Release build: passed, zero warnings and zero errors.
- First full-suite attempt: the Evaluation test-host process aborted; the other five suites passed. This attempt was not a complete pass.
- Repeat full-suite attempt: 364 passed, zero failed, zero skipped (Core 99, Android 17, Privacy 4, Persistence 20, Evaluation 212, CLI 12).
- Replay smoke: `StationaryStableServingCell`, passed.
- Stress smoke: `LongStableUrbanBaseline`, passed.

Earlier isolated numerical reproduction of these unchanged computational sources produced 196,560 records matching the archived deterministic exports. This release validation reran restore, build, tests and smoke checks; it did not repeat the full numerical experiment. Public reruns should use their own actual revision/runtime provenance. Internal path-bearing logs are retained privately; the observed test-host instability remains unresolved and must not be reported as three consecutive complete passes.
