using EcommerceCheckoutFlow.Adapters.Secondary.Persistence;
using EcommerceCheckoutFlow.Application.Handlers;
using EcommerceCheckoutFlow.Application.Ports;
using EcommerceCheckoutFlow.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventSourcingBankAccountWeb.Tests;

public sealed class PaymentRecoveryGuardrailTests : IDisposable
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"payment-recovery-{Guid.NewGuid():N}.db");
    private readonly OrderPlaced source = CreateOrder();
    private DbContextOptions<EcommerceDbContext> Options => new DbContextOptionsBuilder<EcommerceDbContext>()
        .UseSqlite($"Data Source={databasePath};Pooling=False;Default Timeout=2").Options;

    private async Task InitializeAsync()
    {
        await using var db = new EcommerceDbContext(Options);
        await db.Database.EnsureCreatedAsync();
    }

    // Every delivery opens a fresh context/provider/store, modelling a restarted
    // worker rather than relying on an in-memory cache to make retries safe.
    private async Task DeliverAsync(RecordingBus bus, string? fault = null, IPaymentPort? provider = null)
    {
        await using var db = new EcommerceDbContext(Options);
        var persistentProvider = new PersistentPaymentAdapter(db);
        IPaymentPort port = provider ?? new FaultingProvider(persistentProvider, fault);
        IPaymentProcessingStore store = new FaultingStore(new EfCorePaymentProcessingStore(db), fault);
        await new PaymentOnOrderPlacedHandler(port, bus, store,
            NullLogger<PaymentOnOrderPlacedHandler>.Instance).HandleAsync(source);
    }

    [Fact]
    public async Task Failure_before_authorization_does_not_complete_or_emit_a_business_failure()
    {
        await InitializeAsync();
        var bus = new RecordingBus();
        await Assert.ThrowsAsync<TimeoutException>(() => DeliverAsync(bus, "before-authorization"));
        await using (var db = new EcommerceDbContext(Options))
        {
            Assert.Empty(await db.PaymentProcessing.ToListAsync());
            Assert.Empty(await db.PaymentAuthorizations.ToListAsync());
        }
        Assert.Empty(bus.Attempts);
        await DeliverAsync(bus);
        Assert.IsType<PaymentAuthorized>(Assert.Single(bus.Attempts));
        await AssertSuccessfulStateAsync();
    }

    [Theory]
    [InlineData("after-authorization")]
    [InlineData("before-save")]
    public async Task Crash_after_provider_success_retries_the_same_durable_idempotency_key(string fault)
    {
        await InitializeAsync();
        var bus = new RecordingBus();
        await Assert.ThrowsAsync<TimeoutException>(() => DeliverAsync(bus, fault));
        Assert.Empty(bus.Attempts);
        await using (var db = new EcommerceDbContext(Options))
        {
            Assert.Single(await db.PaymentAuthorizations.ToListAsync());
            Assert.Empty(await db.PaymentProcessing.ToListAsync());
        }
        await DeliverAsync(bus);
        Assert.IsType<PaymentAuthorized>(Assert.Single(bus.Attempts));
        await AssertSuccessfulStateAsync();
    }

    [Theory]
    [InlineData("after-save")]
    [InlineData("before-complete")]
    [InlineData("publish-before-ack")]
    [InlineData("publish-after-ack")]
    public async Task Saved_outcome_is_republished_unchanged_without_authorizing_again(string fault)
    {
        await InitializeAsync();
        var bus = new RecordingBus { FailOnce = fault.StartsWith("publish-"), AcceptBeforeFailure = fault == "publish-after-ack" };
        await Assert.ThrowsAsync<TimeoutException>(() => DeliverAsync(bus, fault));
        PaymentProcessingState saved;
        await using (var db = new EcommerceDbContext(Options))
        {
            saved = (await new EfCorePaymentProcessingStore(db).FindAsync(source.EventId))!;
            Assert.False(saved.Completed);
        }
        var expected = Assert.IsType<PaymentAuthorized>(saved.Outcome);
        await DeliverAsync(bus, provider: new UnexpectedProvider());
        Assert.All(bus.Attempts, e => Assert.Equal(expected, Assert.IsType<PaymentAuthorized>(e)));
        Assert.All(bus.Partitions, key => Assert.Equal(source.OrderId, key));
        Assert.Equal(source.CorrelationId, expected.CorrelationId);
        Assert.Equal(source.EventId, expected.CausationId);
        Assert.Equal(source.SequenceNumber + 1, expected.SequenceNumber);
        Assert.Equal(fault is "publish-after-ack" or "before-complete" ? 2 : 1, bus.Accepted.Count);
        await AssertSuccessfulStateAsync();
    }

    [Fact]
    public async Task Completion_ack_loss_and_later_duplicate_do_not_publish_or_authorize_again()
    {
        await InitializeAsync();
        var bus = new RecordingBus();
        await Assert.ThrowsAsync<TimeoutException>(() => DeliverAsync(bus, "after-complete"));
        await DeliverAsync(bus, provider: new UnexpectedProvider());
        Assert.Single(bus.Attempts);
        await AssertSuccessfulStateAsync();
    }

    [Fact]
    public async Task Definitive_decline_is_saved_and_republished_as_the_same_failure()
    {
        await InitializeAsync();
        var bus = new RecordingBus { FailOnce = true };
        await Assert.ThrowsAsync<TimeoutException>(() => DeliverAsync(bus, provider: new DecliningProvider()));
        await DeliverAsync(bus, provider: new UnexpectedProvider());
        var failures = bus.Attempts.Cast<PaymentFailed>().ToArray();
        Assert.Equal(2, failures.Length);
        Assert.Equal(failures[0], failures[1]);
        Assert.Equal("card declined", failures[0].Reason);
        await using var db = new EcommerceDbContext(Options);
        Assert.Empty(await db.PaymentAuthorizations.ToListAsync());
        Assert.True((await new EfCorePaymentProcessingStore(db).FindAsync(source.EventId))!.Completed);
    }

    [Fact]
    public async Task Concurrent_redeliveries_keep_one_authorization_and_one_immutable_outcome()
    {
        await InitializeAsync();
        var bus = new RecordingBus();
        var attempts = Enumerable.Range(0, 8).Select(async _ =>
        {
            try { await DeliverAsync(bus); }
            // SQLite contention is retryable; never treat it as a payment decline.
            catch (SqliteException) { }
            catch (DbUpdateException) { }
        });
        await Task.WhenAll(attempts);
        await DeliverAsync(bus);
        await AssertSuccessfulStateAsync();
        Assert.NotEmpty(bus.Attempts);
        Assert.Single(bus.Attempts.Cast<PaymentAuthorized>().Select(x => x.EventId).Distinct());
    }

    [Fact]
    public async Task Provider_rejects_reusing_a_key_with_different_payment_details()
    {
        await InitializeAsync();
        await using var db = new EcommerceDbContext(Options);
        var provider = new PersistentPaymentAdapter(db);
        provider.Authorize(source, "key");
        Assert.Throws<InvalidOperationException>(() => provider.Authorize(source with { TotalAmount = 100m }, "key"));
        Assert.Single(await db.PaymentAuthorizations.ToListAsync());
    }

    [Fact]
    public async Task Schema_upgrade_is_idempotent_and_preserves_existing_database_data()
    {
        await using var db = new EcommerceDbContext(Options);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE legacy_data (Value TEXT); INSERT INTO legacy_data VALUES ('keep');");
        await PaymentProcessingSchema.EnsureCreatedAsync(db);
        await PaymentProcessingSchema.EnsureCreatedAsync(db);
        Assert.Empty(await db.PaymentProcessing.ToListAsync());
        Assert.Empty(await db.PaymentAuthorizations.ToListAsync());
        await using var command = db.Database.GetDbConnection().CreateCommand();
        await db.Database.OpenConnectionAsync();
        command.CommandText = "SELECT Value FROM legacy_data";
        Assert.Equal("keep", await command.ExecuteScalarAsync());
    }

    [Fact]
    public void Subscribers_are_discoverable_and_fan_out_to_distinct_groups()
    {
        var handlers = typeof(PaymentOnOrderPlacedHandler).Assembly.GetTypes()
            .Where(t => t.Namespace == "EcommerceCheckoutFlow.Application.Handlers")
            .SelectMany(t => t.GetMethods().SelectMany(m => m.GetCustomAttributes(typeof(DotNetCore.CAP.CapSubscribeAttribute), false)
                .Cast<DotNetCore.CAP.CapSubscribeAttribute>().Select(a => (Type: t, Attribute: a)))).ToArray();
        Assert.Equal(7, handlers.Length);
        Assert.All(handlers, h => Assert.True(typeof(DotNetCore.CAP.ICapSubscribe).IsAssignableFrom(h.Type)));
        Assert.All(handlers, h => Assert.Equal(h.Type.Name, h.Attribute.Group));
        Assert.Equal(handlers.Length, handlers.Select(h => (h.Attribute.Name, h.Attribute.Group)).Distinct().Count());
    }

    private async Task AssertSuccessfulStateAsync()
    {
        await using var db = new EcommerceDbContext(Options);
        var authorization = Assert.Single(await db.PaymentAuthorizations.ToListAsync());
        Assert.Equal($"{nameof(PaymentOnOrderPlacedHandler)}:{source.EventId}", authorization.IdempotencyKey);
        Assert.Equal(source.TotalAmount, authorization.Amount);
        Assert.True((await new EfCorePaymentProcessingStore(db).FindAsync(source.EventId))!.Completed);
        Assert.Equal(source.SequenceNumber + 1, (await db.OrderEventSequences.SingleAsync()).LastSequenceNumber);
    }

    private static OrderPlaced CreateOrder()
    {
        var m = EventMetadata.NewRoot(nameof(OrderPlaced), "order-recovery", 1);
        return new OrderPlaced(m.EventId, m.OccurredAt, m.CorrelationId, m.CausationId,
            m.EventType, m.OrderId, m.SequenceNumber, "customer", [new CartItem("sku", "Item", 1, 25m)], 25m);
    }

    private sealed class FaultingProvider(IPaymentPort inner, string? fault) : IPaymentPort
    {
        public void Authorize(OrderPlaced e, string key)
        {
            if (fault == "before-authorization") throw new TimeoutException("provider unavailable");
            inner.Authorize(e, key);
            if (fault == "after-authorization") throw new TimeoutException("authorization response lost");
        }
    }
    private sealed class UnexpectedProvider : IPaymentPort
    {
        public void Authorize(OrderPlaced e, string key) => throw new InvalidOperationException("Saved outcome must bypass provider");
    }
    private sealed class DecliningProvider : IPaymentPort
    {
        public void Authorize(OrderPlaced e, string key) => throw new PaymentDeclinedException("card declined");
    }
    private sealed class FaultingStore(IPaymentProcessingStore inner, string? fault) : IPaymentProcessingStore
    {
        public Task<PaymentProcessingState?> FindAsync(Guid id) => inner.FindAsync(id);
        public async Task<PaymentProcessingState> SaveOutcomeAsync(OrderPlaced source, string? reason)
        {
            if (fault == "before-save") throw new TimeoutException("crash before journal");
            var result = await inner.SaveOutcomeAsync(source, reason);
            if (fault == "after-save") throw new TimeoutException("journal acknowledgment lost");
            return result;
        }
        public async Task CompleteAsync(Guid id)
        {
            if (fault == "before-complete") throw new TimeoutException("crash before checkpoint");
            await inner.CompleteAsync(id);
            if (fault == "after-complete") throw new TimeoutException("checkpoint acknowledgment lost");
        }
    }
    private sealed class RecordingBus : IEventBus
    {
        private readonly object gate = new();
        public bool FailOnce { get; set; }
        public bool AcceptBeforeFailure { get; init; }
        public List<IDomainEvent> Attempts { get; } = [];
        public List<IDomainEvent> Accepted { get; } = [];
        public List<string> Partitions { get; } = [];
        public Task PublishAsync<T>(T e, CancellationToken ct = default) where T : IDomainEvent
            => PublishAsync(e, ((IEventEnvelope)e).GetPartitionKey(), ct);
        public Task PublishAsync<T>(T e, string partitionKey, CancellationToken ct = default) where T : IDomainEvent
        {
            lock (gate)
            {
                Attempts.Add(e);
                Partitions.Add(partitionKey);
                if (FailOnce)
                {
                    FailOnce = false;
                    if (AcceptBeforeFailure) Accepted.Add(e);
                    throw new TimeoutException("publish acknowledgment unavailable");
                }
                Accepted.Add(e);
                return Task.CompletedTask;
            }
        }
    }

    public void Dispose()
    {
        File.Delete(databasePath);
        File.Delete(databasePath + "-shm");
        File.Delete(databasePath + "-wal");
    }
}
