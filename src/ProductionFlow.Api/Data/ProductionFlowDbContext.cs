using Microsoft.EntityFrameworkCore;
using ProductionFlow.Api.Domain;

namespace ProductionFlow.Api.Data;

public sealed class ProductionFlowDbContext : DbContext
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<WorkCenter> WorkCenters => Set<WorkCenter>();

    public DbSet<ProductionOrder> ProductionOrders
        => Set<ProductionOrder>();

    public DbSet<ExecutionReport> ExecutionReports
        => Set<ExecutionReport>();

    public DbSet<OrderHistoryEntry> OrderHistoryEntries
        => Set<OrderHistoryEntry>();

    public DbSet<OutboxMessage> OutboxMessages
        => Set<OutboxMessage>();

    public DbSet<ProcessedMessage> ProcessedMessages
        => Set<ProcessedMessage>();

    // NOWE: podsumowania obliczane przez Workera.
    public DbSet<OrderProductionSummary> OrderProductionSummaries
        => Set<OrderProductionSummary>();

    public ProductionFlowDbContext(
        DbContextOptions<ProductionFlowDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Produkty
        var product = modelBuilder.Entity<Product>();

        product.ToTable("Products");
        product.HasKey(item => item.Id);

        product.Property(item => item.Id)
            .ValueGeneratedNever();

        product.Property(item => item.Code)
            .IsRequired();

        product.Property(item => item.Name)
            .IsRequired();

        product.HasIndex(item => item.Code)
            .IsUnique();

        // Stanowiska produkcyjne
        var workCenter = modelBuilder.Entity<WorkCenter>();

        workCenter.ToTable("WorkCenters");
        workCenter.HasKey(item => item.Id);

        workCenter.Property(item => item.Id)
            .ValueGeneratedNever();

        workCenter.Property(item => item.Code)
            .IsRequired();

        workCenter.Property(item => item.Name)
            .IsRequired();

        workCenter.HasIndex(item => item.Code)
            .IsUnique();

        // Zlecenia produkcyjne
        var order = modelBuilder.Entity<ProductionOrder>();

        order.ToTable("ProductionOrders");
        order.HasKey(item => item.Id);

        order.Property(item => item.Id)
            .ValueGeneratedNever();

        order.Property(item => item.Number)
            .IsRequired();

        order.HasIndex(item => item.Number)
            .IsUnique();

        order.HasOne<Product>()
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        order.HasOne<WorkCenter>()
            .WithMany()
            .HasForeignKey(item => item.WorkCenterId)
            .OnDelete(DeleteBehavior.Restrict);

        // Raporty wykonania
        var report = modelBuilder.Entity<ExecutionReport>();

        report.ToTable("ExecutionReports");
        report.HasKey(item => item.Id);

        report.Property(item => item.Id)
            .ValueGeneratedNever();

        report.Property(item => item.OperatorId)
            .IsRequired();

        report.Property(item => item.CreatedAtUtc)
            .IsRequired();

        report.HasOne<ProductionOrder>()
            .WithMany(item => item.ExecutionReports)
            .HasForeignKey(item => item.ProductionOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        order.Navigation(item => item.ExecutionReports)
            .HasField("_executionReports")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Historia zmian
        var history = modelBuilder.Entity<OrderHistoryEntry>();

        history.ToTable("OrderHistoryEntries");
        history.HasKey(item => item.Id);

        history.Property(item => item.Id)
            .ValueGeneratedNever();

        history.Property(item => item.EventType)
            .HasMaxLength(50)
            .IsRequired();

        history.Property(item => item.Details)
            .HasMaxLength(500)
            .IsRequired();

        history.Property(item => item.Actor)
            .HasMaxLength(100);

        history.Property(item => item.OccurredAtUtc)
            .IsRequired();

        history.HasOne<ProductionOrder>()
            .WithMany()
            .HasForeignKey(item => item.ProductionOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        history.HasIndex(item => new
        {
            item.ProductionOrderId,
            item.OccurredAtUtc
        });

        // Transactional Outbox — Dzień 40
        var outbox = modelBuilder.Entity<OutboxMessage>();

        outbox.ToTable("OutboxMessages");
        outbox.HasKey(item => item.Id);

        outbox.Property(item => item.Id)
            .ValueGeneratedNever();

        outbox.Property(item => item.OrderId)
            .IsRequired();

        outbox.Property(item => item.EventType)
            .HasMaxLength(100)
            .IsRequired();

        outbox.Property(item => item.Payload)
            .HasColumnType("jsonb")
            .IsRequired();

        outbox.Property(item => item.OccurredAtUtc)
            .IsRequired();

        outbox.Property(item => item.PublishedAtUtc);

        outbox.HasIndex(item => new
        {
            item.PublishedAtUtc,
            item.OccurredAtUtc
        });

        // Idempotent Consumer — Dzień 42
        var processed = modelBuilder.Entity<ProcessedMessage>();

        processed.ToTable("ProcessedMessages");
        processed.HasKey(item => item.EventId);

        processed.Property(item => item.EventId)
            .ValueGeneratedNever();

        processed.Property(item => item.OrderId)
            .IsRequired();

        processed.Property(item => item.EventType)
            .HasMaxLength(100)
            .IsRequired();

        processed.Property(item => item.ProcessedAtUtc)
            .IsRequired();

        processed.HasIndex(item => item.ProcessedAtUtc);

        // NOWE: podsumowania produkcyjne — Dzień 44
        var summary = modelBuilder.Entity<OrderProductionSummary>();

        summary.ToTable("OrderProductionSummaries");

        summary.HasKey(item => item.OrderId);

        summary.Property(item => item.OrderId)
            .ValueGeneratedNever();

        summary.Property(item => item.Number)
            .HasMaxLength(50)
            .IsRequired();

        summary.Property(item => item.Status)
            .HasConversion<int>()
            .IsRequired();

        summary.Property(item => item.PlannedQuantity)
            .IsRequired();

        summary.Property(item => item.GoodQuantity)
            .IsRequired();

        summary.Property(item => item.ScrapQuantity)
            .IsRequired();

        summary.Property(item => item.ReportsCount)
            .IsRequired();

        summary.Property(item => item.UpdatedAtUtc)
            .IsRequired();

        summary.HasOne<ProductionOrder>()
            .WithOne()
            .HasForeignKey<OrderProductionSummary>(
                item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public override int SaveChanges(
        bool acceptAllChangesOnSuccess)
    {
        AddHistoryAndOutboxEntries();

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        AddHistoryAndOutboxEntries();

        return base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }

    private void AddHistoryAndOutboxEntries()
    {
        ChangeTracker.DetectChanges();

        var changedOrders = ChangeTracker
            .Entries<ProductionOrder>()
            .ToList();

        foreach (var entry in changedOrders)
        {
            if (entry.State == EntityState.Added)
            {
                RegisterOrderEvent(
                    entry.Entity.Id,
                    "OrderCreated",
                    $"Utworzono zlecenie {entry.Entity.Number}.");
            }
            else if (entry.State == EntityState.Modified)
            {
                if (entry.Property(item => item.Status).IsModified)
                {
                    var previousStatus = entry
                        .Property(item => item.Status)
                        .OriginalValue;

                    var currentStatus = entry.Entity.Status;

                    RegisterOrderEvent(
                        entry.Entity.Id,
                        "OrderStatusChanged",
                        $"Zmieniono status: " +
                        $"{previousStatus} -> {currentStatus}.");
                }
                else if (entry.Properties.Any(
                    property => property.IsModified))
                {
                    RegisterOrderEvent(
                        entry.Entity.Id,
                        "OrderUpdated",
                        $"Zaktualizowano zlecenie " +
                        $"{entry.Entity.Number}.");
                }
            }
        }

        var addedReports = ChangeTracker
            .Entries<ExecutionReport>()
            .Where(entry => entry.State == EntityState.Added)
            .ToList();

        foreach (var entry in addedReports)
        {
            var report = entry.Entity;

            RegisterOrderEvent(
                report.ProductionOrderId,
                "ExecutionReportAdded",
                $"Dodano raport: dobre = {report.GoodQuantity}, " +
                $"braki = {report.ScrapQuantity}.",
                report.OperatorId);
        }
    }

    private void RegisterOrderEvent(
        Guid orderId,
        string eventType,
        string details,
        string? actor = null)
    {
        var historyEntry = new OrderHistoryEntry(
            orderId,
            eventType,
            details,
            actor);

        OrderHistoryEntries.Add(historyEntry);

        var outboxMessage = OutboxMessage.Create(
            orderId,
            eventType,
            historyEntry.OccurredAtUtc);

        OutboxMessages.Add(outboxMessage);
    }
}