using Microsoft.EntityFrameworkCore;
using Shared.Interceptors;
using Shared.Security;
using ShipmentEntity =
    global::ShipmentWorkflow.Domain.Entities.Shipment;

using CargoItemEntity =
    global::ShipmentWorkflow.Domain.Entities.CargoItem;

using ShipmentLocationEntity =
    global::ShipmentWorkflow.Domain.Entities.ShipmentLocation;

using ShipmentDocumentEntity =
    global::ShipmentWorkflow.Domain.Entities.ShipmentDocument;

using ShipmentMilestoneEntity =
    global::ShipmentWorkflow.Domain.Entities.ShipmentMilestone;

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
    public DbSet<ShipmentLocationEntity> ShipmentLocations => Set<ShipmentLocationEntity>();
    public DbSet<ShipmentDocumentEntity> ShipmentDocuments => Set<ShipmentDocumentEntity>();
    public DbSet<ShipmentMilestoneEntity> ShipmentMilestones => Set<ShipmentMilestoneEntity>();

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
        ConfigureShipmentLocation(modelBuilder);
        ConfigureShipmentDocument(modelBuilder);
        ConfigureShipmentMilestone(modelBuilder);
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

            entity.HasIndex(shipment =>
                new { shipment.TenantId, shipment.CustomerId });

            entity.HasIndex(shipment =>
                new { shipment.TenantId, shipment.RouteId });

            entity.HasIndex(shipment =>
                new { shipment.TenantId, shipment.VehicleId });

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

            entity.Property(shipment => shipment.Priority)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(shipment => shipment.TransportMode)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(shipment => shipment.RouteId)
                .HasMaxLength(100);

            entity.Property(shipment => shipment.VehicleId)
                .HasMaxLength(100);

            entity.Property(shipment => shipment.Notes)
                .HasMaxLength(2_000);

            entity.HasMany(shipment => shipment.CargoItems)
                .WithOne(cargoItem => cargoItem.Shipment)
                .HasForeignKey(cargoItem => cargoItem.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(shipment => shipment.Locations)
                .WithOne(location => location.Shipment)
                .HasForeignKey(location => location.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(shipment => shipment.Documents)
                .WithOne(document => document.Shipment)
                .HasForeignKey(document => document.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(shipment => shipment.Milestones)
                .WithOne(milestone => milestone.Shipment)
                .HasForeignKey(milestone => milestone.ShipmentId)
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

    private void ConfigureShipmentLocation(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShipmentLocationEntity>(entity =>
        {
            entity.ToTable("shipment_locations");

            entity.HasKey(location => location.Id);

            entity.HasQueryFilter(location =>
                location.TenantId == _currentUser.TenantId);

            entity.HasIndex(location =>
                    new { location.TenantId, location.ShipmentId, location.Sequence })
                .IsUnique();

            entity.Property(location => location.Type)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(location => location.Name)
                .HasMaxLength(ShipmentLocationEntity.NameMaxLength)
                .IsRequired();

            entity.Property(location => location.Address)
                .HasMaxLength(ShipmentLocationEntity.AddressMaxLength)
                .IsRequired();

            entity.Property(location => location.ContactName)
                .HasMaxLength(ShipmentLocationEntity.ContactNameMaxLength);

            entity.Property(location => location.ContactPhone)
                .HasMaxLength(ShipmentLocationEntity.ContactPhoneMaxLength);

            entity.Property(location => location.Sequence)
                .IsRequired();
        });
    }

    private void ConfigureShipmentDocument(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShipmentDocumentEntity>(entity =>
        {
            entity.ToTable("shipment_documents");

            entity.HasKey(document => document.Id);

            entity.HasQueryFilter(document =>
                document.TenantId == _currentUser.TenantId);

            entity.HasIndex(document =>
                new { document.TenantId, document.ShipmentId, document.DocumentType });

            entity.HasIndex(document =>
                new { document.TenantId, document.OCRStatus });

            entity.Property(document => document.FileName)
                .HasMaxLength(ShipmentDocumentEntity.FileNameMaxLength)
                .IsRequired();

            entity.Property(document => document.DocumentType)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(document => document.StorageUrl)
                .HasMaxLength(ShipmentDocumentEntity.StorageUrlMaxLength)
                .IsRequired();

            entity.Property(document => document.OCRStatus)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(document => document.OCRConfidence)
                .HasPrecision(5, 4);

            entity.Property(document => document.UploadedAt)
                .IsRequired();

            entity.Property(document => document.ExtractedDataJson)
                .HasColumnType("jsonb");
        });
    }

    private void ConfigureShipmentMilestone(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShipmentMilestoneEntity>(entity =>
        {
            entity.ToTable("shipment_milestones");

            entity.HasKey(milestone => milestone.Id);

            entity.HasQueryFilter(milestone =>
                milestone.TenantId == _currentUser.TenantId);

            entity.HasIndex(milestone =>
                new { milestone.TenantId, milestone.ShipmentId, milestone.RecordedAt });

            entity.HasIndex(milestone =>
                new { milestone.TenantId, milestone.RecordedAt });

            entity.Property(milestone => milestone.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(milestone => milestone.Description)
                .HasMaxLength(ShipmentMilestoneEntity.DescriptionMaxLength);

            entity.Property(milestone => milestone.RecordedAt)
                .IsRequired();

            entity.Property(milestone => milestone.Source)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();
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
