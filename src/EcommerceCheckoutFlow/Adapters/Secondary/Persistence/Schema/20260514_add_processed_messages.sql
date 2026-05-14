CREATE TABLE IF NOT EXISTS processed_messages (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    consumer_name TEXT NOT NULL,
    event_id TEXT NOT NULL,
    processed_at_utc TEXT NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_processed_messages_consumer_event
    ON processed_messages (consumer_name, event_id);

CREATE INDEX IF NOT EXISTS ix_processed_messages_processed_at_utc
    ON processed_messages (processed_at_utc);
