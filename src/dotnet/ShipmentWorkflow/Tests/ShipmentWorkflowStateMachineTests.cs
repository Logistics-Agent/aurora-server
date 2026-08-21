using ShipmentEntity = global::ShipmentWorkflow.Domain.Entities.Shipment;
using ShipmentWorkflow.Domain.Enums;

namespace ShipmentWorkflow.Tests;

public sealed class ShipmentWorkflowStateMachineTests
{
    [Fact]
    public void Allowed_lifecycle_transitions_update_status_history_and_milestones()
    {
        var actorId = Guid.NewGuid();
        var shipment = CreateShipment();
        var tenantId = shipment.TenantId;

        shipment.Submit(actorId);
        shipment.StartPlanning(actorId);
        shipment.StartNegotiation(actorId);
        shipment.Confirm(actorId);
        shipment.MarkPickedUp(actorId, DateTimeOffset.Parse("2026-07-14T01:00:00Z"));
        shipment.MarkInTransit(actorId);
        shipment.MarkDelivered(actorId, DateTimeOffset.Parse("2026-07-14T05:00:00Z"));
        shipment.Complete(actorId);

        Assert.Equal(ShipmentStatus.Completed, shipment.Status);
        Assert.Equal(tenantId, shipment.TenantId);
        Assert.Equal(8, shipment.StatusHistories.Count);
        Assert.Equal(8, shipment.Milestones.Count);
        Assert.Equal(DateTimeOffset.Parse("2026-07-14T01:00:00Z"), shipment.ActualPickupTime);
        Assert.Equal(DateTimeOffset.Parse("2026-07-14T05:00:00Z"), shipment.ActualDeliveryTime);
        Assert.All(shipment.Milestones, milestone => Assert.Equal(tenantId, milestone.TenantId));
    }

    [Fact]
    public void Customs_processing_can_return_to_in_transit_then_deliver()
    {
        var shipment = CreateShipmentInTransit();

        shipment.StartCustomsProcessing();
        shipment.MarkInTransit();
        shipment.MarkDelivered();

        Assert.Equal(ShipmentStatus.Delivered, shipment.Status);
        Assert.Contains(shipment.StatusHistories, history =>
            history.Status == ShipmentStatus.CustomsProcessing);
    }

    [Fact]
    public void Invalid_transition_is_rejected()
    {
        var shipment = CreateShipment();

        Assert.Throws<InvalidOperationException>(() => shipment.MarkDelivered());
        Assert.Equal(ShipmentStatus.Draft, shipment.Status);
        Assert.Empty(shipment.StatusHistories);
        Assert.Empty(shipment.Milestones);
    }

    [Fact]
    public void Completed_and_cancelled_are_terminal()
    {
        var completed = CreateShipmentInTransit();
        completed.MarkDelivered();
        completed.Complete();

        Assert.Throws<InvalidOperationException>(() => completed.Cancel("late cancellation"));
        Assert.Throws<InvalidOperationException>(() => completed.MarkInTransit());

        var cancelled = CreateShipment();
        cancelled.Cancel("customer request");

        Assert.Throws<InvalidOperationException>(() => cancelled.Submit());
    }

    [Theory]
    [InlineData(ShipmentStatus.Draft)]
    [InlineData(ShipmentStatus.Submitted)]
    [InlineData(ShipmentStatus.Planning)]
    [InlineData(ShipmentStatus.Negotiating)]
    [InlineData(ShipmentStatus.Confirmed)]
    [InlineData(ShipmentStatus.PickedUp)]
    [InlineData(ShipmentStatus.InTransit)]
    [InlineData(ShipmentStatus.CustomsProcessing)]
    public void Cancellation_is_allowed_only_before_terminal_delivery_states(
        ShipmentStatus status)
    {
        var shipment = CreateShipmentAt(status);

        shipment.Cancel("no longer needed", Guid.NewGuid());

        Assert.Equal(ShipmentStatus.Cancelled, shipment.Status);
        Assert.Contains(shipment.StatusHistories, history =>
            history.Status == ShipmentStatus.Cancelled &&
            history.Note!.Contains("no longer needed", StringComparison.Ordinal));
        Assert.Contains(shipment.Milestones, milestone =>
            milestone.Status == ShipmentStatus.Cancelled);
    }

    [Theory]
    [InlineData(ShipmentStatus.Delivered)]
    [InlineData(ShipmentStatus.Completed)]
    [InlineData(ShipmentStatus.Cancelled)]
    public void Cancellation_is_rejected_from_invalid_states(ShipmentStatus status)
    {
        var shipment = CreateShipmentAt(status);

        Assert.Throws<InvalidOperationException>(() =>
            shipment.Cancel("not allowed"));
    }

    [Fact]
    public void Assign_route_and_vehicle_validate_references()
    {
        var shipment = CreateShipment();

        shipment.AssignRoute(" route-1 ");
        shipment.AssignVehicle(" vehicle-1 ");

        Assert.Equal("route-1", shipment.RouteId);
        Assert.Equal("vehicle-1", shipment.VehicleId);
        Assert.Throws<ArgumentException>(() => shipment.AssignRoute(" "));
        Assert.Throws<ArgumentException>(() => shipment.AssignVehicle(" "));
    }

    [Fact]
    public void Created_status_is_compatible_with_draft_state_machine()
    {
        var shipment = CreateShipment();
        shipment.Status = ShipmentStatus.Created;

        shipment.Submit();

        Assert.Equal(ShipmentStatus.Submitted, shipment.Status);
    }

    private static ShipmentEntity CreateShipment()
    {
        return ShipmentEntity.Create(
            Guid.NewGuid(),
            $"SHP-TEST-{Guid.CreateVersion7():N}",
            orderId: "ORD-1",
            customerName: "Acme",
            destinationAddress: "Warehouse 9");
    }

    private static ShipmentEntity CreateShipmentInTransit()
    {
        return CreateShipmentAt(ShipmentStatus.InTransit);
    }

    private static ShipmentEntity CreateShipmentAt(ShipmentStatus status)
    {
        var shipment = CreateShipment();

        switch (status)
        {
            case ShipmentStatus.Draft:
                break;
            case ShipmentStatus.Submitted:
                shipment.Submit();
                break;
            case ShipmentStatus.Planning:
                shipment.Submit();
                shipment.StartPlanning();
                break;
            case ShipmentStatus.Negotiating:
                shipment.Submit();
                shipment.StartPlanning();
                shipment.StartNegotiation();
                break;
            case ShipmentStatus.Confirmed:
                shipment.Submit();
                shipment.StartPlanning();
                shipment.StartNegotiation();
                shipment.Confirm();
                break;
            case ShipmentStatus.PickedUp:
                shipment.Submit();
                shipment.StartPlanning();
                shipment.StartNegotiation();
                shipment.Confirm();
                shipment.MarkPickedUp();
                break;
            case ShipmentStatus.InTransit:
                shipment.Submit();
                shipment.StartPlanning();
                shipment.StartNegotiation();
                shipment.Confirm();
                shipment.MarkPickedUp();
                shipment.MarkInTransit();
                break;
            case ShipmentStatus.CustomsProcessing:
                shipment.Submit();
                shipment.StartPlanning();
                shipment.StartNegotiation();
                shipment.Confirm();
                shipment.MarkPickedUp();
                shipment.MarkInTransit();
                shipment.StartCustomsProcessing();
                break;
            case ShipmentStatus.Delivered:
                shipment.Submit();
                shipment.StartPlanning();
                shipment.StartNegotiation();
                shipment.Confirm();
                shipment.MarkPickedUp();
                shipment.MarkInTransit();
                shipment.MarkDelivered();
                break;
            case ShipmentStatus.Completed:
                shipment.Submit();
                shipment.StartPlanning();
                shipment.StartNegotiation();
                shipment.Confirm();
                shipment.MarkPickedUp();
                shipment.MarkInTransit();
                shipment.MarkDelivered();
                shipment.Complete();
                break;
            case ShipmentStatus.Cancelled:
                shipment.Cancel("test");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        return shipment;
    }
}
