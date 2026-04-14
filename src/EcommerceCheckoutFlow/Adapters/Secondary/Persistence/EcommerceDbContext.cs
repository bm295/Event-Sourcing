using System.Text.Json;
using EcommerceCheckoutFlow.Domain;
using Microsoft.EntityFrameworkCore;

namespace EcommerceCheckoutFlow.Adapters.Secondary.Persistence;

public sealed class EcommerceDbContext(DbContextOptions<EcommerceDbContext> options) : DbContext(options)
{
    public DbSet<OrderRecord> Orders => Set<OrderRecord>();

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
