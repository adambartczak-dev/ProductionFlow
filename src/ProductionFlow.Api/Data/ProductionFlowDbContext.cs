
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

        // Historia zmian zleceń
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
    }

    // Automatyczny zapis historii przy SaveChanges().
    public override int SaveChanges(
        bool acceptAllChangesOnSuccess)
    {
        AddHistoryEntries();

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    // Automatyczny zapis historii przy SaveChangesAsync().
    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        AddHistoryEntries();

        return base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }

    private void AddHistoryEntries()
    {
        ChangeTracker.DetectChanges();

        // Pobieramy zmiany zleceń przed dodawaniem historii.
        var changedOrders = ChangeTracker
            .Entries<ProductionOrder>()
            .ToList();

        foreach (var entry in changedOrders)
        {
            if (entry.State == EntityState.Added)
            {
                OrderHistoryEntries.Add(
                    new OrderHistoryEntry(
                        entry.Entity.Id,
                        "OrderCreated",
                        $"Utworzono zlecenie {entry.Entity.Number}."));
            }
            else if (entry.State == EntityState.Modified &&
                     entry.Property(item => item.Status).IsModified)
            {
                var previousStatus = entry
                    .Property(item => item.Status)
                    .OriginalValue;

                var currentStatus = entry.Entity.Status;

                OrderHistoryEntries.Add(
                    new OrderHistoryEntry(
                        entry.Entity.Id,
                        "OrderStatusChanged",
                        $"Zmieniono status: " +
                        $"{previousStatus} -> {currentStatus}."));
            }
        }

        // Rejestrujemy dodane raporty produkcyjne.
        var addedReports = ChangeTracker
            .Entries<ExecutionReport>()
            .Where(entry => entry.State == EntityState.Added)
            .ToList();

        foreach (var entry in addedReports)
        {
            var report = entry.Entity;

            OrderHistoryEntries.Add(
                new OrderHistoryEntry(
                    report.ProductionOrderId,
                    "ExecutionReportAdded",
                    $"Dodano raport: dobre = {report.GoodQuantity}, " +
                    $"braki = {report.ScrapQuantity}.",
                    report.OperatorId));
        }
    }
}
