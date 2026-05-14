CREATE TABLE IF NOT EXISTS order_event_sequences (
    order_id TEXT PRIMARY KEY,
    last_sequence_number INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS consumer_order_sequences (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    consumer_name TEXT NOT NULL,
    order_id TEXT NOT NULL,
    last_sequence_number INTEGER NOT NULL,
    UNIQUE(consumer_name, order_id)
);

-- If using outbox/events table, enforce uniqueness per stream.
CREATE UNIQUE INDEX IF NOT EXISTS ux_outbox_order_sequence
ON cap_published(order_id, sequence_number);
