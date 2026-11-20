# AI Event Intelligence MVP

This read-only capability explains the existing bank-account event stream using an LLM, rather than changing event replay or appending events.

## Configuration

Set `EventAI__Endpoint` to a trusted chat-completions HTTPS endpoint, `EventAI__Model` to the model identifier, and `EventAI__ApiKey` using secret management. Never commit keys. The provider must support JSON response format. Do not enable the endpoint for untrusted users without authentication, authorization per aggregate, rate limits, and a data-handling review.

## API

`POST /api/ai/events/explain`

```json
{"aggregateId":"bank-account-demo","question":"Why was the withdrawal rejected?","upToVersion":10}
```

Returns `summary`, `evidenceEventIds`, `confidence`, `limitations`, `analyzedEvents`. A missing stream returns 404, malformed input returns 400, unavailable model configuration returns 503, and ungrounded citations return 502.

## Design constraints

- Read events through `IEventStore.ReadStream`, which checks stored signatures; never write from the LLM path.
- Send only a compact event timeline; cap at 200 events and 2,000 question characters.
- Reject citations not present in the retrieved event stream; do not silently accept fabricated IDs.
- A cited event supports traceability, not necessarily causation. The model must state uncertainty.
- The in-memory demo store resets on process restart; this MVP does not add persistence, embeddings, or a vector database.
- For production: enforce per-aggregate authorization, redact personal information, add telemetry for latency/tokens/cost, and test prompt injection and model response failure cases.
