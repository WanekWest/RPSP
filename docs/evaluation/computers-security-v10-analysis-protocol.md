# RPSP Computers and Security v10 Evaluation Protocol

Status: `PROTOCOL_FIXED_SYNTHETIC_RUNNABLE_PLATFORM_STUDY_BLOCKED`.

## Purpose and evidence boundary

This protocol evaluates deterministic behavior of the existing four-detector RPSP pipeline under controlled replay and synthetic perturbations. It does not estimate field detection accuracy, identify a network actor, establish that an attack occurred, or validate deployment-scale effectiveness.

The synthetic experiment is executable from the repository. The benign Android platform study is specified below but must not begin until the authors have documented an institutional ethics or exemption determination, participant consent, and the availability of the required devices.

## Frozen synthetic design

The machine-readable source of truth is `computers-security-v10-experiment-manifest.json`.

- Sources: all 36 replay scenarios and all 25 synthetic stress traces present in the catalog.
- Replication: 30 deterministic seed replicates for each condition.
- Conditions: boundary scales 0.75, 1.00, and 1.25.
- Timing transformation: original offsets are divided by the boundary scale, then bounded deterministic jitter is applied while preserving strict chronological order.
- Signal transformation: RSRP distance from the fixed -80 dBm reference is scaled and bounded deterministic noise is added. Values are clamped to the physically plausible representation range used by the test harness.
- Categorical cell sequences, motion states, service states, baseline membership, and neighbor-graph membership are not relabeled or silently changed.
- Constructed labels are derived only from the explicit expectations already present in replay and stress catalog records. Rows without an explicit expectation remain `unspecified` and are excluded from label-agreement denominators.
- A score at or above 0.5 is the predeclared positive synthetic decision threshold. This is an analysis convention for constructed scenarios, not a clinical or operational threshold.
- Timing collection is disabled in the deterministic evidence run. A separate benchmark run must set `captureTiming` to true and must be reported as machine-dependent performance evidence.

The primary outcomes are positive-label agreement, negative-label agreement, score distribution, warning band distribution, suppression rate, and observation count. Any confidence interval must use deterministic resampling with the run as the sampling unit and must report its seed and number of resamples.

## Frozen ablations

The manifest executes the following profiles without changing the production detector algorithms or their default options:

1. `full`: the unchanged production pipeline.
2. `stationary-only`, `rat-only`, `unknown-cell-only`, and `neighbor-graph-only`: one detector contribution is retained before composite scoring.
3. `no-motion-suppression`: the configured benign-motion suppression sets are empty for the experiment only.
4. `reduced-evidence-gates`: minimum observation, duration, history, and baseline gates are reduced to their minimum valid configuration. This is deliberately not described as complete removal because internal detector validity checks remain active.
5. `no-composite-boost`: correlation boost values are zero for the experiment only.

Results must be grouped by profile. A single-detector profile measures pipeline behavior after contribution filtering; it does not reimplement or retrain a detector.

## Benign platform observability study

Minimum design:

- Three personally controlled stock Android devices.
- At least two device manufacturers and two Android major versions.
- Four benign contexts: stationary indoor, stationary outdoor, pedestrian, and passenger in a vehicle.
- Ten sessions of at least ten minutes for every device-context combination.
- The participant in a vehicle must not be the driver.

Required measures are API-field availability, missingness, update interval, permission failures, detector activation, benign alerts per hour, suppression rate, median and p95 processing latency, CPU, memory, and battery impact.

The study must not create interference, operate a rogue or fake base station, manipulate a public network, collect precise location, or collect persistent subscriber or device identifiers. Technical capture failures defined before collection may be excluded; all other sessions must remain in the analysis, including sessions with missing observations or detector misses.

Raw traces must remain outside Git in controlled storage. Repository artifacts are limited to the schema, data dictionary, protocol, pseudonymized session aliases, deidentified aggregates, exclusion log, environment metadata, and cryptographic checksums.

## Submission gates

The platform component remains `NOT VERIFIED` until all of the following exist:

- author-approved ethics or exemption record;
- author-approved consent text;
- device inventory meeting the minimum design;
- complete session manifest and exclusion log;
- reproducible aggregate analysis;
- privacy review confirming the absence of prohibited identifiers.

Failure to meet a gate leaves the manuscript status `NOT_READY_FOR_SUBMISSION`; it must not be replaced by fabricated or inferred data.
