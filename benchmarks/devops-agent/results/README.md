# Results and visual evidence

Each runner invocation writes an isolated `results.csv` and `summary.json` under its selected output directory. Preserve the top-level CSV as the schema/template. Do not combine local mock runs with live Gemini/Azure OpenAI runs or mix demo and production.

Store screenshots in `screenshots/<environment>/<provider>/`. Include the Grafana time range and environment/provider labels in the capture. Add a short note linking each image to the benchmark `run_id`. Screenshots support the raw CSV and metric exports; they are not substitutes for them.

Human evaluators should fill `predicted_root_cause`, `rca_correct` (`true`/`false`), and `notes` after inspecting sanitized RCA output against the scenario ground truth. Keep failed, denied, timed-out, fallback, and provider-mismatch runs in the raw data and identify them in notes; state the inclusion rule used for summary statistics.

Use a fixed rubric: correct only when the RCA identifies the primary injected failure mechanism and does not assert a contradictory cause. Treat uncertain, missing, and abstained answers as incorrect unless the report separately labels abstentions. Ideally have a second reviewer adjudicate disagreements. Calculate accuracy from labeled incidents only and disclose the number of labeled/total eligible runs.
