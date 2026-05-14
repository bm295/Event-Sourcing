# processed_messages retention policy

## Idempotency window

- Keep records for at least **35 days**.
- This 35-day TTL must be greater than or equal to the longest replay/redelivery window agreed with upstream brokers/consumers.

## Cleanup job

Run a periodic cleanup (daily off-peak), deleting rows older than TTL using `processed_at_utc`:

```sql
DELETE FROM processed_messages
WHERE processed_at_utc < datetime('now', '-35 days');
```

## Safety / operations

- Execute cleanup in small batches (for example 5,000 rows/iteration) if table growth is high.
- Emit metrics for deleted row count, total row count, and oldest `processed_at_utc` value.
- Alert if oldest row is newer than TTL target (cleanup too aggressive) or table size grows unexpectedly.
