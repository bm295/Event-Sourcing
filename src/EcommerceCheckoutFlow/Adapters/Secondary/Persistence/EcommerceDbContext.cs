using System.Text.Json;
using EcommerceCheckoutFlow.Domain;
using Microsoft.EntityFrameworkCore;

namespace EcommerceCheckoutFlow.Adapters.Secondary.Persistence;

public sealed class EcommerceDbContext(DbContextOptions<EcommerceDbContext> options) : DbContext(options)
{
    public DbSet<OrderRecord> Orders => Set<OrderRecord>();
    public DbSet<ProcessedMessageRecord> ProcessedMessages => Set<ProcessedMessageRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderRecord>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(order => order.OrderId);
            entity.Property(order => order.OrderId).HasColumnName("order_id");
            entity.Property(order => order.CustomerId).HasColumnName("customer_id");
            entity.Property(order => order.ItemsJson).HasColumnName("items_json");
            entity.Property(order => order.TotalAmount).HasColumnName("total_amount");
            entity.Property(order => order.CreatedAtUtc).HasColumnName("created_at_utc");
        });

        modelBuilder.Entity<ProcessedMessageRecord>(entity =>
        {
            entity.ToTable("processed_messages");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.Property(record => record.ConsumerName).HasColumnName("consumer_name").HasColumnType("TEXT");
            entity.Property(record => record.EventId).HasColumnName("event_id").HasColumnType("TEXT");
            entity.Property(record => record.ProcessedAtUtc).HasColumnName("processed_at_utc").HasColumnType("TEXT");
            entity.HasIndex(record => new { record.ConsumerName, record.EventId }).IsUnique().HasDatabaseName("ux_processed_messages_consumer_event");
            entity.HasIndex(record => record.ProcessedAtUtc).HasDatabaseName("ix_processed_messages_processed_at_utc");
        });
    }
}

public sealed class OrderRecord
{
    public required string OrderId { get; init; }
    public required string CustomerId { get; init; }
    public required string ItemsJson { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }

    public static OrderRecord From(Order order)
    {
        return new OrderRecord
        {
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            ItemsJson = JsonSerializer.Serialize(order.Items),
            TotalAmount = order.TotalAmount,
            CreatedAtUtc = order.CreatedAt
        };
    }
}
