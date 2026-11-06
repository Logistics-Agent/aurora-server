#!/usr/bin/env python3
"""Small, sequential benchmark runner for DevOps Agent's Alertmanager webhook."""

from __future__ import annotations

import argparse
import csv
import json
import math
import os
import re
import statistics
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from datetime import datetime, timezone
from pathlib import Path


CSV_FIELDS = [
    "run_id", "started_at_utc", "finished_at_utc", "environment",
    "provider_expected", "provider_observed", "model_observed", "scenario_id",
    "iteration", "correlation_id", "alert_signature", "delivery_attempts",
    "deduplicated_attempts", "deduplication_rate", "ingestion_latency_ms",
    "rca_completion_observed", "rca_completion_latency_ms",
    "rca_processing_duration_ms", "ground_truth_root_cause",
    "predicted_root_cause", "rca_correct", "notes",
]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="milliseconds")


def get_json(url: str, timeout: float) -> object:
    request = urllib.request.Request(url, headers={"Accept": "application/json"})
    with urllib.request.urlopen(request, timeout=timeout) as response:
        return json.loads(response.read().decode("utf-8"))


def get_text(url: str, timeout: float) -> str:
    request = urllib.request.Request(url, headers={"Accept": "text/plain"})
    with urllib.request.urlopen(request, timeout=timeout) as response:
        return response.read().decode("utf-8", errors="replace")


def post_json(url: str, token: str, payload: dict, timeout: float) -> tuple[int, object, float]:
    body = json.dumps(payload).encode("utf-8")
    request = urllib.request.Request(
        url, data=body, method="POST",
        headers={"Authorization": f"Bearer {token}", "Content-Type": "application/json"},
    )
    started = time.monotonic()
    with urllib.request.urlopen(request, timeout=timeout) as response:
        result = json.loads(response.read().decode("utf-8"))
        return response.status, result, (time.monotonic() - started) * 1000


def metric_samples(text: str, metric_name: str) -> list[tuple[dict[str, str], float]]:
    samples = []
    for line in text.splitlines():
        if not line or line.startswith("#"):
            continue
        # Parse Prometheus exposition labels without treating escaped commas as separators.
        head, _, raw_value = line.rpartition(" ")
        if not raw_value:
            continue
        if "{" in head:
            name, raw_labels = head.split("{", 1)
            if not raw_labels.endswith("}"):
                continue
            labels = {}
            for match in re.finditer(r'([a-zA-Z_][a-zA-Z0-9_]*)="((?:\\.|[^"\\])*)"(?:,|$)', raw_labels[:-1]):
                key, value = match.groups()
                labels[key] = value.replace(r'\"', '"').replace(r'\\', '\\')
        else:
            name, labels = head, {}
        if name == metric_name:
            try:
                samples.append((labels, float(raw_value)))
            except ValueError:
                pass
    return samples


def metric_total(text: str, metric_name: str, label_key: str | None = None,
                 label_value: str | None = None) -> float:
    return sum(value for labels, value in metric_samples(text, metric_name)
               if label_key is None or labels.get(label_key) == label_value)


def provider_observation(text: str) -> tuple[str, str]:
    matches = [(labels, value) for labels, value in metric_samples(text, "ai_gateway_requests_total")
               if labels.get("operation", "").lower() == "generate"
               and labels.get("callerServiceId", "").lower() == "devops-agent"]
    if not matches:
        return "", ""
    labels, _ = max(matches, key=lambda item: item[1])
    return labels.get("provider", ""), labels.get("model", "")


def percentile(values: list[float], q: float) -> float | None:
    if not values:
        return None
    ordered = sorted(values)
    pos = (len(ordered) - 1) * q
    lo, hi = math.floor(pos), math.ceil(pos)
    return ordered[lo] if lo == hi else ordered[lo] + (ordered[hi] - ordered[lo]) * (pos - lo)


def append_csv(path: Path, row: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    needs_header = not path.exists() or path.stat().st_size == 0
    with path.open("a", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=CSV_FIELDS, extrasaction="ignore")
        if needs_header:
            writer.writeheader()
        writer.writerow(row)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", required=True, type=Path)
    parser.add_argument("--scenario", required=True, type=Path)
    parser.add_argument("--runs", type=int, default=1, help="Unique incidents to create (default: 1)")
    parser.add_argument("--duplicate-replays", type=int, default=2,
                        help="Repeat each identical signature this many times (default: 2)")
    parser.add_argument("--output-dir", type=Path, default=Path("benchmarks/devops-agent/results/run"))
    parser.add_argument("--timeout-seconds", type=float, default=20)
    parser.add_argument("--rca-wait-seconds", type=int, default=300)
    parser.add_argument("--allow-production", action="store_true",
                        help="Required for prod profile; use only with explicit operational approval")
    args = parser.parse_args()

    if args.runs < 1 or args.duplicate_replays < 0:
        parser.error("--runs must be >= 1 and --duplicate-replays must be >= 0")
    profile = json.loads(args.profile.read_text(encoding="utf-8"))
    scenario = json.loads(args.scenario.read_text(encoding="utf-8"))
    environment = profile["environment"].lower()
    if environment == "prod" and not args.allow_production:
        parser.error("Production profile refused. Prefer demo; explicit --allow-production is required.")

    token_name = profile["webhook_token_env"]
    token = os.environ.get(token_name)
    if not token:
        parser.error(f"Set bearer token in environment variable {token_name}; do not put it in profile files.")
    agent_url = profile["agent_url"].rstrip("/")
    metrics_url = profile.get("agent_metrics_url", agent_url + "/actuator/prometheus")
    governance_metrics_url = profile.get("ai_governance_metrics_url", "")
    if "<" in agent_url or "<" in profile["prometheus_url"]:
        parser.error("Replace the profile's placeholder URLs before running.")

    # Fail early on connectivity and verify the metrics used by this harness exist.
    health = get_json(agent_url + "/actuator/health", args.timeout_seconds)
    if health.get("status") != "UP":
        parser.error(f"DevOps Agent health is not UP: {health}")
    prom_query = urllib.parse.quote(profile.get("prometheus_job_query", 'up{job="devops-agent"}'), safe="")
    prom_result = get_json(profile["prometheus_url"].rstrip("/") + "/api/v1/query?query=" + prom_query,
                           args.timeout_seconds)
    active_targets = [sample for sample in prom_result.get("data", {}).get("result", [])
                      if sample.get("value", [None, ""])[1] == "1"]
    if not active_targets:
        parser.error("Prometheus has no healthy DevOps Agent scrape target; check profile query/job label.")
    baseline_metrics = get_text(metrics_url, args.timeout_seconds)
    if not metric_samples(baseline_metrics, "devops_rca_duration_seconds_count"):
        parser.error("Agent metrics do not expose devops_rca_duration_seconds_count; check Actuator/scrape config.")

    run_id = uuid.uuid4().hex[:12]
    output_dir = args.output_dir
    output_dir.mkdir(parents=True, exist_ok=True)
    csv_path = output_dir / "results.csv"
    rca_latencies: list[float] = []
    ingest_latencies: list[float] = []
    total_attempts = 0
    total_dedup = 0
    successful_rca = 0
    invalid_runs: list[str] = []
    observed_providers: set[str] = set()
    rows = []

    for iteration in range(1, args.runs + 1):
        iteration_id = f"{run_id}-{iteration:03d}"
        severity_word = "Critical" if scenario["severity"].lower() == "critical" else "High"
        alertname = f"{scenario['alertname']}_{severity_word}_{iteration_id}"
        signature = f"{alertname}:{scenario['service']}:{environment}"
        before = get_text(metrics_url, args.timeout_seconds)
        before_rca = metric_total(before, "devops_rca_duration_seconds_count")
        before_rca_sum = metric_total(before, "devops_rca_duration_seconds_sum")
        before_governance = get_text(governance_metrics_url, args.timeout_seconds) if governance_metrics_url else ""
        started_at = utc_now()
        run_started = time.monotonic()
        payload = {
            "version": "4", "status": "firing", "receiver": "devops-agent-benchmark",
            "alerts": [{
                "status": "firing",
                "labels": {
                    "alertname": alertname, "service": scenario["service"],
                    "environment": environment, "severity": scenario["severity"],
                },
                "annotations": {"summary": scenario["summary"], "description": scenario["evidence"]},
                "startsAt": started_at, "fingerprint": iteration_id,
            }],
        }
        status, response, first_latency = post_json(
            agent_url + "/api/v1/alerts/prometheus", token, payload, args.timeout_seconds)
        if status != 202 or not response or response[0].get("duplicated"):
            raise RuntimeError(f"Expected a newly created incident (HTTP 202); got {status}: {response}")
        correlation_id = response[0].get("correlationId", "")
        attempts = 1
        duplicates = 0
        for _ in range(args.duplicate_replays):
            replay_status, replay_response, _ = post_json(
                agent_url + "/api/v1/alerts/prometheus", token, payload, args.timeout_seconds)
            attempts += 1
            if replay_status != 202 or not replay_response or not replay_response[0].get("duplicated"):
                raise RuntimeError(f"Expected replay to deduplicate: HTTP {replay_status}: {replay_response}")
            duplicates += 1

        deadline = time.monotonic() + args.rca_wait_seconds
        rca_latency = None
        rca_duration_ms = None
        while time.monotonic() < deadline:
            current = get_text(metrics_url, args.timeout_seconds)
            current_count = metric_total(current, "devops_rca_duration_seconds_count")
            current_sum = metric_total(current, "devops_rca_duration_seconds_sum")
            if current_count >= before_rca + 1:
                rca_latency = (time.monotonic() - run_started) * 1000
                rca_duration_ms = max(0.0, current_sum - before_rca_sum) * 1000
                break
            time.sleep(3)

        provider, model = ("", "")
        if governance_metrics_url:
            after_governance = get_text(governance_metrics_url, args.timeout_seconds)
            provider, model = provider_observation(after_governance)
            if provider:
                observed_providers.add(provider)
            if provider and provider.upper() != profile["provider_expected"].upper():
                invalid_runs.append(f"{iteration_id}: expected {profile['provider_expected']}, observed {provider}")
            elif not provider and profile.get("allow_live_provider_claim", False):
                invalid_runs.append(f"{iteration_id}: no AI Governance provider metric observed; provider benchmark is unverified")

        elapsed_ingest_ms = first_latency
        ingest_latencies.append(elapsed_ingest_ms)
        total_attempts += attempts
        total_dedup += duplicates
        if rca_latency is not None:
            successful_rca += 1
            rca_latencies.append(rca_latency)
        else:
            invalid_runs.append(f"{iteration_id}: RCA did not complete within {args.rca_wait_seconds}s")

        row = {
            "run_id": run_id, "started_at_utc": started_at, "finished_at_utc": utc_now(),
            "environment": environment, "provider_expected": profile["provider_expected"],
            "provider_observed": provider, "model_observed": model,
            "scenario_id": scenario["scenario_id"], "iteration": iteration,
            "correlation_id": correlation_id, "alert_signature": signature,
            "delivery_attempts": attempts, "deduplicated_attempts": duplicates,
            "deduplication_rate": duplicates / attempts if attempts else 0,
            "ingestion_latency_ms": round(elapsed_ingest_ms, 2),
            "rca_completion_observed": rca_latency is not None,
            "rca_completion_latency_ms": round(rca_latency, 2) if rca_latency is not None else "",
            "rca_processing_duration_ms": round(rca_duration_ms, 2) if rca_duration_ms is not None else "",
            "ground_truth_root_cause": scenario["ground_truth"]["root_cause"],
            "predicted_root_cause": "", "rca_correct": "",
            "notes": "Manual RCA scoring required; verify actual provider/model in AI Governance metrics.",
        }
        append_csv(csv_path, row)
        rows.append(row)
        print(json.dumps({"iteration": iteration, "correlation_id": correlation_id,
                          "ingestion_ms": round(first_latency, 2),
                          "rca_completion_observed_ms": round(rca_latency, 2) if rca_latency else None,
                          "dedup_rate": round(duplicates / attempts, 4)}, ensure_ascii=False))

    dedup_rate = total_dedup / total_attempts if total_attempts else 0
    summary = {
        "status": "completed_with_invalid_runs" if invalid_runs else "completed",
        "run_id": run_id, "environment": environment,
        "provider_expected": profile["provider_expected"],
        "provider_observed": sorted(observed_providers),
        "scenario_ids": [scenario["scenario_id"]], "runs": args.runs,
        "attempts": total_attempts, "deduplicated_attempts": total_dedup,
        "successful_rca_runs": successful_rca,
        "metrics": {
            "ingestion_latency_ms_median": round(statistics.median(ingest_latencies), 2) if ingest_latencies else None,
            "rca_completion_latency_ms_median": round(statistics.median(rca_latencies), 2) if rca_latencies else None,
            "rca_completion_latency_ms_p95": round(percentile(rca_latencies, 0.95), 2) if rca_latencies else None,
            "deduplication_rate": round(dedup_rate, 6),
            "root_cause_accuracy": None,
        },
        "invalid_or_failed_runs": invalid_runs,
        "limitations": [
            "RCA completion is inferred from aggregate successful-duration histogram changes and polling; isolate the service and run sequentially.",
            "Histogram delta may include unrelated RCA if the environment is shared.",
            "Root cause accuracy requires human scoring; this service currently does not persist a structured predicted root cause.",
            "Expected provider is profile metadata; observed provider labels are required to validate provider comparison.",
        ],
        "results_csv": str(csv_path),
    }
    (output_dir / "summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, indent=2, ensure_ascii=False))
    return 1 if invalid_runs else 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace")
        raise SystemExit(f"HTTP {exc.code} from benchmark target: {body}")
    except (urllib.error.URLError, TimeoutError, OSError, ValueError, KeyError, RuntimeError) as exc:
        raise SystemExit(f"Benchmark failed: {exc}")
