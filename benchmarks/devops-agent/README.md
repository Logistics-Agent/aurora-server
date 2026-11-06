# DevOps Agent benchmark

Repeatable benchmark harness for the repository's actual path:

`Alertmanager webhook -> DevOps Agent -> Redis dedup -> PostgreSQL incident -> scheduled RCA -> AI Governance -> Gemini or Azure OpenAI`

This harness sends synthetic Alertmanager webhook payloads to the existing `POST /api/v1/alerts/prometheus` endpoint and reads the existing Micrometer counters/histograms. It does not generate infrastructure faults itself: choose an isolated scenario and inject its stated symptom separately. The synthetic webhook scenarios are useful for RCA and dedup measurements; they do not prove that Kubernetes or a database actually failed.

## Layout

- `scenarios/`: synthetic incident inputs and ground truth for scoring.
- `profiles/{local,demo,prod}/{gemini,azure-openai}.json`: non-secret endpoint/provider metadata per environment/provider. `provider_label` is the expected provider label; verify actual selection in AI Governance metrics for every run.
- `scripts/run_benchmark.py`: standard-library-only runner; appends one CSV row per incident attempt and writes a summary JSON.
- `results/`: result template and evidence inventory. Never commit tokens or raw customer data.

## Metrics and limits

- **Ingestion latency:** webhook POST start to HTTP response.
- **RCA completion latency:** webhook POST start to the next successful `devops_rca_duration_seconds_count` observation. This is scrape/poll bounded and should be interpreted as an observed end-to-end upper bound, not exact model time. It can be ambiguous if other workloads generate RCA concurrently; run isolated and sequentially.
- **RCA processing p95:** query the Agent's `devops_rca_duration_seconds_bucket`; it excludes worker queue delay and only records successful RCA.
- **Deduplication rate:** `deduplicated attempts / total attempts`. The Agent's Redis key is derived from normalized alert signature and expires after 300 seconds. The runner sends each unique incident once, then repeats the same signature `duplicate_replays` times.
- **Root cause accuracy:** manually score the model's RCA against `ground_truth.root_cause` using a documented rubric. Current Agent persistence does not store a structured predicted root cause, so this harness leaves `rca_correct` blank. Do not publish an accuracy percentage until output capture and human adjudication are in place.
- A duplicate does not create a new incident/RCA. The runner waits for RCA only after the first, non-duplicate delivery.
- A configured provider profile does not force routing. Confirm actual provider/model labels in AI Governance metrics/audit; mark the run invalid for provider comparison if it used fallback or another provider.

## Configure and run

Copy the relevant profile to a private file or edit its non-secret URLs. Set the webhook token in the environment variable named by the profile. Never put secrets in a profile committed to Git.

Local mock/wiring check (does **not** benchmark a real LLM):

```powershell
docker compose -f deploy/local/docker-compose.yml --profile ai --profile notification-java up -d --build ai-governance devops-agent prometheus alertmanager grafana notification-java
$env:DEVOPS_ALERT_WEBHOOK_TOKEN = 'local-dev-only-token'
python benchmarks/devops-agent/scripts/run_benchmark.py --profile benchmarks/devops-agent/profiles/local/gemini.json --scenario benchmarks/devops-agent/scenarios/database-timeout.json --runs 5 --output-dir benchmarks/devops-agent/results/local/gemini
```

Use a separate output directory for each provider/profile, for example `results/local/azure-openai`, `results/demo/gemini`, and `results/demo/azure-openai`. The profile's provider label is metadata only; AI Governance's actual provider label is authoritative.

Example after demo port-forwards are ready (run once per provider, with governance routing restricted to that provider):

```powershell
python benchmarks/devops-agent/scripts/run_benchmark.py --profile benchmarks/devops-agent/profiles/demo/gemini.json --scenario benchmarks/devops-agent/scenarios/crashloopbackoff.json --runs 5 --duplicate-replays 2 --output-dir benchmarks/devops-agent/results/demo/gemini
python benchmarks/devops-agent/scripts/run_benchmark.py --profile benchmarks/devops-agent/profiles/demo/azure-openai.json --scenario benchmarks/devops-agent/scenarios/crashloopbackoff.json --runs 5 --duplicate-replays 2 --output-dir benchmarks/devops-agent/results/demo/azure-openai
```

Repeat with the other scenario files. For a fair provider comparison, keep the scenario, prompt/evidence, governance policy, output token budget, replica count, and workload the same; record actual provider/model and report fallback or provider mismatch separately.

For real LLM results, configure AI Governance provider slots/pools and capability `devops.rca` for exactly the provider under test, provide its credentials through the deployment's secret mechanism, and confirm selected provider/model on each run. Gemini and Azure OpenAI are the provider names supported by this repository. A local default/demo key may resolve to deterministic demo output; label those runs `demo/mock`, never `live`.

### Demo

First complete the Alertmanager, Key Vault token, internal DNS, and Prometheus/Grafana setup described in `docs/technical/devops-agent-benchmark-and-cv-review.md`. The runner needs a reachable Agent URL and a Prometheus URL that can scrape the Agent. From the workstation, use approved port-forwards or a private test network. Use synthetic inputs only.

### Production

Production profiles are deliberately disabled by default: the script refuses them unless `--allow-production` is passed. Even then, only use an explicitly approved low-volume maintenance window, a production test signature that cannot match real alerts, and a designated on-call owner. The harness is not a DDoS tool and must not be used to create outages or send uncontrolled traffic. Prefer demo for all fault injection and load tests.

## Fault injection

Keep faults separate from alert submissions so the timestamp and ground truth are clear. In local/demo only, choose one controlled fault at a time: scale a non-critical demo dependency to zero briefly, deploy an intentionally bad image/readiness probe and roll back, or use a test database endpoint with a short connection timeout. Do not stop DevOps Agent, Redis, or the shared database when measuring RCA; doing so disables the measurement path. Record fault start/end and verify recovery before the next run. For alert-storm/dedup measurements, replay one synthetic signature at a low fixed rate; do not flood endpoints.

## Evidence to retain

| Evidence | Purpose | Priority |
|---|---|---|
| `README.md`, profile, scenario, runner version/commit | Reproducibility and measurement definitions | Very high |
| `results.csv` and `summary.json` per environment/provider | Raw runs plus aggregates and failures | Very high |
| Agent + AI Governance Prometheus query exports around the run | Verify actual provider, p95, errors, dedup and RCA | Very high |
| Sanitized incident/RCA output with correlation IDs | Human ground-truth scoring and traceability | High; synthetic data only |
| Grafana screenshots with time range and dashboard variables visible | Visual evidence supporting the exported data | Medium |
| Short demo recording | Shows end-to-end workflow; supplementary evidence | Optional |

Keep `results.csv` headers from the template. Record screenshots only after removing tokens, account identifiers, and personal data.
