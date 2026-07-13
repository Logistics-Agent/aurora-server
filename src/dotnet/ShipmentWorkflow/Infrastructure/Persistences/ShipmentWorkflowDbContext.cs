using Microsoft.EntityFrameworkCore;
using Shared.Interceptors;
using Shared.Security;
using ShipmentEntity =
    global::ShipmentWorkflow.Domain.Entities.Shipment;

using CargoItemEntity =
    global::ShipmentWorkflow.Domain.Entities.CargoItem;

using ShipmentStatusHistoryEntity =
    global::ShipmentWorkflow.Domain.Entities.ShipmentStatusHistory;

using OutboxMessageEntity =
    global::ShipmentWorkflow.Domain.Entities.OutboxMessage;

namespace ShipmentWorkflow.Infrastructure.Persistences;

public sealed class ShipmentWorkflowDbContext(
    DbContextOptions<ShipmentWorkflowDbContext> options,
    ICurrentUserService currentUser,
    AuditSaveChangesInterceptor auditInterceptor) : DbContext(options)
{
    private readonly ICurrentUserService _currentUser = currentUser;
    private readonly AuditSaveChangesInterceptor _auditInterceptor = auditInterceptor;

    public DbSet<ShipmentEntity> Shipments => Set<ShipmentEntity>();
    public DbSet<CargoItemEntity> CargoItems => Set<CargoItemEntity>();

    public DbSet<ShipmentStatusHistoryEntity> ShipmentStatusHistories =>
        Set<ShipmentStatusHistoryEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

    protected override void OnConfiguring(
        DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(_auditInterceptor);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureShipment(modelBuilder);
        ConfigureCargoItem(modelBuilder);
        ConfigureShipmentStatusHistory(modelBuilder);
        ConfigureOutboxMessage(modelBuilder);
    }

    private void ConfigureShipment(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShipmentEntity>(entity =>
        {
            entity.ToTable("shipments");

            entity.HasKey(shipment => shipment.Id);

            entity.HasQueryFilter(shipment =>
                shipment.TenantId == _currentUser.TenantId);

            entity.HasIndex(shipment =>
                    new { shipment.TenantId, shipment.ShipmentNo })
                .IsUnique();

            entity.HasIndex(shipment =>
                new { shipment.TenantId, shipment.Status });

            entity.HasIndex(shipment =>
                new { shipment.TenantId, shipment.CreatedAt });

            entity.HasIndex(shipment => shipment.OrderId);

            entity.Property(shipment => shipment.ShipmentNo)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(shipment => shipment.OrderId)
                .HasMaxLength(100);

            entity.Property(shipment => shipment.CustomerName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(shipment => shipment.DestinationAddress)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(shipment => shipment.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.HasMany(shipment => shipment.CargoItems)
                .WithOne(cargoItem => cargoItem.Shipment)
                .HasForeignKey(cargoItem => cargoItem.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(shipment => shipment.StatusHistories)
                .WithOne(history => history.Shipment)
                .HasForeignKey(history => history.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private void ConfigureCargoItem(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CargoItemEntity>(entity =>
        {
            entity.ToTable("cargo_items");

            entity.HasKey(cargoItem => cargoItem.Id);

            entity.HasQueryFilter(cargoItem =>
                cargoItem.Shipment != null &&
                cargoItem.Shipment.TenantId == _currentUser.TenantId);

            entity.HasIndex(cargoItem => cargoItem.ShipmentId);

            entity.Property(cargoItem => cargoItem.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(cargoItem => cargoItem.Quantity)
                .IsRequired();

            entity.Property(cargoItem => cargoItem.WeightKg)
                .IsRequired();

            entity.Property(cargoItem => cargoItem.HsCode)
                .HasMaxLength(50);
        });
    }

    private void ConfigureShipmentStatusHistory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShipmentStatusHistoryEntity>(entity =>
        {
            entity.ToTable("shipment_status_histories");

            entity.HasKey(history => history.Id);

            entity.HasQueryFilter(history =>
                history.Shipment != null &&
                history.Shipment.TenantId == _currentUser.TenantId);

            entity.HasIndex(history =>
                new { history.ShipmentId, history.CreatedAt });

            entity.Property(history => history.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(history => history.Note)
                .HasMaxLength(500);
        });
    }

    private void ConfigureOutboxMessage(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessageEntity>(entity =>
        {
            entity.ToTable("outbox_messages");

            entity.HasKey(outboxMessage => outboxMessage.Id);

            entity.HasIndex(outboxMessage => outboxMessage.ProcessedAt);
            entity.HasIndex(outboxMessage => outboxMessage.CreatedAt);

            entity.Property(outboxMessage => outboxMessage.EventType)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(outboxMessage => outboxMessage.Payload)
                .IsRequired();

            entity.Property(outboxMessage => outboxMessage.Error);
        });
    }

}