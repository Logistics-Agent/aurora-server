using GpsTracking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Interceptors;
using Shared.Security;

namespace GpsTracking.Infrastructure.Persistences;

public sealed class GpsTrackingDbContext(
    DbContextOptions<GpsTrackingDbContext> options,
    ICurrentUserService currentUser,
    AuditSaveChangesInterceptor auditInterceptor) : DbContext(options)
{
    private readonly ICurrentUserService _currentUser = currentUser;
    private readonly AuditSaveChangesInterceptor _auditInterceptor = auditInterceptor;

    public DbSet<GpsPosition> Positions => Set<GpsPosition>();
    public DbSet<CurrentLocation> CurrentLocations => Set<CurrentLocation>();
    public DbSet<VehicleShipmentAssignment> VehicleShipmentAssignments => Set<VehicleShipmentAssignment>();
    public DbSet<ShipmentTrackingState> ShipmentTrackingStates => Set<ShipmentTrackingState>();
    public DbSet<Geofence> Geofences => Set<Geofence>();
    public DbSet<GeofencePresence> GeofencePresences => Set<GeofencePresence>();
    public DbSet<MonitoringAlert> MonitoringAlerts => Set<MonitoringAlert>();
    public DbSet<ConsumedIntegrationEvent> ConsumedIntegrationEvents => Set<ConsumedIntegrationEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.AddInterceptors(_auditInterceptor);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigurePosition(modelBuilder);
        ConfigureCurrentLocation(modelBuilder);
        ConfigureAssignment(modelBuilder);
        ConfigureShipmentTrackingState(modelBuilder);
        ConfigureGeofence(modelBuilder);
        ConfigureGeofencePresence(modelBuilder);
        ConfigureMonitoringAlert(modelBuilder);
        ConfigureConsumedEvent(modelBuilder);
        ConfigureOutbox(modelBuilder);
    }

    private void ConfigurePosition(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GpsPosition>(entity =>
        {
            entity.ToTable("gps_positions");
            entity.HasKey(item => item.Id);
            entity.HasQueryFilter(item => item.TenantId == _currentUser.TenantId);
            entity.HasIndex(item => new { item.TenantId, item.DeviceId, item.ExternalReadingId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.VehicleId, item.RecordedAt, item.Id });
            entity.HasIndex(item => new { item.TenantId, item.ShipmentId, item.RecordedAt, item.Id });
            entity.Property(item => item.ExternalReadingId).HasMaxLength(150).IsRequired();
            entity.Property(item => item.DeviceId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.VehicleId).HasMaxLength(100).IsRequired();
            ConfigureCoordinates(entity.Property(item => item.Latitude), entity.Property(item => item.Longitude));
            entity.Property(item => item.SpeedKph).HasPrecision(8, 3);
            entity.Property(item => item.HeadingDegrees).HasPrecision(6, 2);
            entity.Property(item => item.AccuracyMeters).HasPrecision(10, 2);
            entity.Property(item => item.RecordedAt).IsRequired();
            entity.Property(item => item.ReceivedAt).IsRequired();
        });
    }

    private void ConfigureCurrentLocation(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CurrentLocation>(entity =>
        {
            entity.ToTable("current_locations");
            entity.HasKey(item => item.Id);
            entity.HasQueryFilter(item => item.TenantId == _currentUser.TenantId);
            entity.HasIndex(item => new { item.TenantId, item.VehicleId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ShipmentId });
            entity.HasIndex(item => new { item.TenantId, item.RecordedAt });
            entity.HasIndex(item => new { item.TenantId, item.StationarySince });
            entity.Property(item => item.DeviceId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.VehicleId).HasMaxLength(100).IsRequired();
            ConfigureCoordinates(entity.Property(item => item.Latitude), entity.Property(item => item.Longitude));
            entity.Property(item => item.SpeedKph).HasPrecision(8, 3);
            entity.Property(item => item.HeadingDegrees).HasPrecision(6, 2);
            entity.Property(item => item.AccuracyMeters).HasPrecision(10, 2);
            entity.HasOne<GpsPosition>()
                .WithMany()
                .HasForeignKey(item => item.PositionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private void ConfigureAssignment(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VehicleShipmentAssignment>(entity =>
        {
            entity.ToTable("vehicle_shipment_assignments");
            entity.HasKey(item => item.Id);
            entity.HasQueryFilter(item => item.TenantId == _currentUser.TenantId);
            entity.HasIndex(item => new { item.TenantId, item.VehicleId, item.EndedAt });
            entity.HasIndex(item => new { item.TenantId, item.ShipmentId, item.EndedAt });
            entity.HasIndex(item => new { item.TenantId, item.VehicleId })
                .IsUnique()
                .HasFilter("\"EndedAt\" IS NULL");
            entity.HasIndex(item => new { item.TenantId, item.ShipmentId })
                .IsUnique()
                .HasFilter("\"EndedAt\" IS NULL");
            entity.Property(item => item.RouteId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.VehicleId).HasMaxLength(100).IsRequired();
            entity.Ignore(item => item.IsActive);
        });
    }

    private void ConfigureGeofence(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Geofence>(entity =>
        {
            entity.ToTable("geofences");
            entity.HasKey(item => item.Id);
            entity.HasQueryFilter(item => item.TenantId == _currentUser.TenantId);
            entity.HasIndex(item => new { item.TenantId, item.IsActive });
            entity.HasIndex(item => new { item.TenantId, item.VehicleId, item.IsActive });
            entity.HasIndex(item => new { item.TenantId, item.ShipmentId, item.IsActive });
            entity.Property(item => item.Name).HasMaxLength(150).IsRequired();
            entity.Property(item => item.VehicleId).HasMaxLength(100);
            ConfigureCoordinates(entity.Property(item => item.Latitude), entity.Property(item => item.Longitude));
            entity.Property(item => item.RadiusMeters).HasPrecision(12, 2);
        });
    }

    private void ConfigureShipmentTrackingState(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShipmentTrackingState>(entity =>
        {
            entity.ToTable("shipment_tracking_states");
            entity.HasKey(item => item.Id);
            entity.HasQueryFilter(item => item.TenantId == _currentUser.TenantId);
            entity.HasIndex(item => new { item.TenantId, item.ShipmentId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.IsClosed, item.LastEventAt });
        });
    }

    private void ConfigureGeofencePresence(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GeofencePresence>(entity =>
        {
            entity.ToTable("geofence_presences");
            entity.HasKey(item => item.Id);
            entity.HasQueryFilter(item => item.TenantId == _currentUser.TenantId);
            entity.HasIndex(item => new { item.TenantId, item.GeofenceId, item.VehicleId }).IsUnique();
            entity.Property(item => item.VehicleId).HasMaxLength(100).IsRequired();
            entity.HasOne<Geofence>()
                .WithMany()
                .HasForeignKey(item => item.GeofenceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private void ConfigureMonitoringAlert(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MonitoringAlert>(entity =>
        {
            entity.ToTable("monitoring_alerts");
            entity.HasKey(item => item.Id);
            entity.HasQueryFilter(item => item.TenantId == _currentUser.TenantId);
            entity.HasIndex(item => new { item.TenantId, item.Status, item.OccurredAt });
            entity.HasIndex(item => new { item.TenantId, item.VehicleId, item.OccurredAt });
            entity.HasIndex(item => new { item.TenantId, item.DeduplicationKey })
                .IsUnique()
                .HasFilter("\"Status\" = 'Active'");
            entity.Property(item => item.AlertType).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(item => item.VehicleId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.DeduplicationKey).HasMaxLength(300).IsRequired();
            entity.Property(item => item.Message).HasMaxLength(1000).IsRequired();
            entity.HasOne<Geofence>()
                .WithMany()
                .HasForeignKey(item => item.GeofenceId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<GpsPosition>()
                .WithMany()
                .HasForeignKey(item => item.PositionId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private void ConfigureConsumedEvent(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConsumedIntegrationEvent>(entity =>
        {
            entity.ToTable("consumed_integration_events");
            entity.HasKey(item => item.Id);
            entity.HasQueryFilter(item => item.TenantId == _currentUser.TenantId);
            entity.HasIndex(item => new { item.SourceEventType, item.SourceEventId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ReceivedAt });
            entity.Property(item => item.SourceEventType).HasMaxLength(256).IsRequired();
        });
    }

    private void ConfigureOutbox(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(item => item.Id);
            entity.HasQueryFilter(item => item.TenantId == _currentUser.TenantId);
            entity.HasIndex(item => item.EventId).IsUnique();
            entity.HasIndex(item => new { item.ProcessedAt, item.RetryCount, item.OccurredAt });
            entity.HasIndex(item => new { item.TenantId, item.OccurredAt });
            entity.Property(item => item.EventType).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Content).IsRequired();
            entity.Property(item => item.Error).HasMaxLength(2000);
        });
    }

    private static void ConfigureCoordinates(
        Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<decimal> latitude,
        Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<decimal> longitude)
    {
        latitude.HasPrecision(9, 6);
        longitude.HasPrecision(10, 6);
    }
}
