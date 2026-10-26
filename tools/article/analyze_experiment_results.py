#!/usr/bin/env python3
"""Aggregate RPSP constructed-label experiments with deterministic bootstrap CIs."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import random
from collections import defaultdict
from pathlib import Path
from typing import Any, Iterable


BOOTSTRAP_RESAMPLES = 1000
BOOTSTRAP_SEED = 20260905


def _ratio(numerator: int, denominator: int) -> float | None:
    return None if denominator == 0 else numerator / denominator


def _metrics(records: Iterable[dict[str, Any]]) -> dict[str, Any]:
    tp = tn = fp = fn = suppressed = total = 0

    for record in records:
        total += 1
        suppressed += record["suppressionState"] == "suppressed"
        label = record["constructedLabel"]
        predicted_positive = float(record["riskScore"]) >= 0.5

        if label == "positive":
            if predicted_positive:
                tp += 1
            else:
                fn += 1
        elif label == "negative":
            if predicted_positive:
                fp += 1
            else:
                tn += 1

    sensitivity = _ratio(tp, tp + fn)
    specificity = _ratio(tn, tn + fp)
    balanced_accuracy = (
        None
        if sensitivity is None or specificity is None
        else (sensitivity + specificity) / 2
    )
    denominator = math.sqrt((tp + fp) * (tp + fn) * (tn + fp) * (tn + fn))
    mcc = None if denominator == 0 else ((tp * tn) - (fp * fn)) / denominator

    return {
        "labeledRows": tp + tn + fp + fn,
        "truePositive": tp,
        "trueNegative": tn,
        "falsePositive": fp,
        "falseNegative": fn,
        "sensitivity": sensitivity,
        "specificity": specificity,
        "balancedAccuracy": balanced_accuracy,
        "mcc": mcc,
        "suppressionRate": _ratio(suppressed, total),
    }


def _percentile(values: list[float], probability: float) -> float | None:
    if not values:
        return None
    ordered = sorted(values)
    index = (len(ordered) - 1) * probability
    lower = math.floor(index)
    upper = math.ceil(index)
    if lower == upper:
        return ordered[lower]
    weight = index - lower
    return ordered[lower] * (1 - weight) + ordered[upper] * weight


def _bootstrap(records: list[dict[str, Any]], seed: int) -> dict[str, list[float | None]]:
    clusters: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for record in records:
        clusters[record["runId"]].append(record)

    cluster_ids = sorted(clusters)
    rng = random.Random(seed)
    samples: dict[str, list[float]] = defaultdict(list)

    if not cluster_ids:
        return {}

    for _ in range(BOOTSTRAP_RESAMPLES):
        sampled_records: list[dict[str, Any]] = []
        for _ in cluster_ids:
            sampled_records.extend(clusters[rng.choice(cluster_ids)])
        result = _metrics(sampled_records)
        for name in ("sensitivity", "specificity", "balancedAccuracy", "mcc", "suppressionRate"):
            value = result[name]
            if value is not None:
                samples[name].append(value)

    return {
        name: [_percentile(values, 0.025), _percentile(values, 0.975)]
        for name, values in samples.items()
    }


def _format(value: float | None) -> str:
    return "n/a" if value is None else f"{value:.3f}"


def _write_checksum_manifest(directory: Path) -> None:
    entries = []
    for path in sorted(directory.iterdir(), key=lambda candidate: candidate.name):
        if not path.is_file() or path.name == "checksums.sha256":
            continue
        digest = hashlib.sha256(path.read_bytes()).hexdigest().upper()
        entries.append(f"{digest}  {path.name}")
    (directory / "checksums.sha256").write_text("\n".join(entries) + "\n", encoding="utf-8")


def analyze(result_directory: Path) -> None:
    result_path = result_directory / "experiment-results.json"
    payload = json.loads(result_path.read_text(encoding="utf-8"))
    records = payload["records"]
    groups: dict[tuple[str, str, float], list[dict[str, Any]]] = defaultdict(list)
    for record in records:
        groups[(record["ablationProfile"], record["detectorFamily"], float(record["boundaryScale"]))].append(record)

    rows = []
    for index, ((profile, detector, scale), group_records) in enumerate(sorted(groups.items())):
        metrics = _metrics(group_records)
        intervals = _bootstrap(group_records, BOOTSTRAP_SEED + index)
        row = {
            "ablationProfile": profile,
            "detectorFamily": detector,
            "boundaryScale": scale,
            **metrics,
            "confidenceIntervals95": intervals,
            "bootstrapUnit": "runId",
            "bootstrapResamples": BOOTSTRAP_RESAMPLES,
            "bootstrapSeed": BOOTSTRAP_SEED + index,
        }
        rows.append(row)

    analysis = {
        "schemaVersion": "1.0",
        "experimentId": payload["manifest"]["experimentId"],
        "configurationHash": payload["provenance"]["configurationHash"],
        "interpretation": (
            "Constructed-label synthetic scenario agreement only; not field accuracy, "
            "attack confirmation, or actor attribution."
        ),
        "rows": rows,
    }
    (result_directory / "experiment-analysis.json").write_text(
        json.dumps(analysis, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )

    csv_fields = [
        "detector_family",
        "ablation_profile",
        "boundary_scale",
        "labeled_rows",
        "true_positive",
        "true_negative",
        "false_positive",
        "false_negative",
        "sensitivity",
        "specificity",
        "balanced_accuracy",
        "mcc",
        "suppression_rate",
        "bootstrap_resamples",
        "bootstrap_seed",
    ]
    with (result_directory / "experiment-analysis.csv").open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=csv_fields)
        writer.writeheader()
        for row in rows:
            writer.writerow(
                {
                    "detector_family": row["detectorFamily"],
                    "ablation_profile": row["ablationProfile"],
                    "boundary_scale": f'{row["boundaryScale"]:.2f}',
                    "labeled_rows": row["labeledRows"],
                    "true_positive": row["truePositive"],
                    "true_negative": row["trueNegative"],
                    "false_positive": row["falsePositive"],
                    "false_negative": row["falseNegative"],
                    "sensitivity": _format(row["sensitivity"]),
                    "specificity": _format(row["specificity"]),
                    "balanced_accuracy": _format(row["balancedAccuracy"]),
                    "mcc": _format(row["mcc"]),
                    "suppression_rate": _format(row["suppressionRate"]),
                    "bootstrap_resamples": row["bootstrapResamples"],
                    "bootstrap_seed": row["bootstrapSeed"],
                }
            )

    lines = [
        "# Synthetic Sensitivity Analysis",
        "",
        analysis["interpretation"],
        "",
        "| Profile | Detector | Scale | Labeled rows | Sensitivity | Specificity | Balanced accuracy | MCC | Suppression rate |",
        "|---|---|---:|---:|---:|---:|---:|---:|---:|",
    ]
    for row in rows:
        lines.append(
            "| {profile} | {detector} | {scale:.2f} | {labeled} | {sensitivity} | {specificity} | "
            "{balanced} | {mcc} | {suppression} |".format(
                profile=row["ablationProfile"],
                detector=row["detectorFamily"],
                scale=row["boundaryScale"],
                labeled=row["labeledRows"],
                sensitivity=_format(row["sensitivity"]),
                specificity=_format(row["specificity"]),
                balanced=_format(row["balancedAccuracy"]),
                mcc=_format(row["mcc"]),
                suppression=_format(row["suppressionRate"]),
            )
        )
    lines.extend(
        [
            "",
            f"Confidence intervals are stored in the JSON output and use {BOOTSTRAP_RESAMPLES} deterministic cluster resamples with runId as the sampling unit.",
        ]
    )
    (result_directory / "experiment-analysis.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    _write_checksum_manifest(result_directory)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("result_directory", type=Path)
    args = parser.parse_args()
    analyze(args.result_directory.resolve())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
