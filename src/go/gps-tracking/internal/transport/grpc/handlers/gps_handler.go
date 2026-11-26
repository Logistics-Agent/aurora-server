package handlers

import (
	"context"
	"strings"

	"aurora/gps-tracking/internal/domain"
	"aurora/gps-tracking/internal/infrastructure/persistence/postgres"
	"aurora/gps-tracking/proto/gpstrackingpb"

	"github.com/google/uuid"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/metadata"
	"google.golang.org/grpc/status"
	"google.golang.org/protobuf/types/known/timestamppb"
)

type GpsHandler struct {
	gpstrackingpb.UnimplementedGpsTrackingServiceServer
	repo *postgres.GpsRepository
}

func NewGpsHandler(repo *postgres.GpsRepository) *GpsHandler {
	return &GpsHandler{repo: repo}
}

func (h *GpsHandler) extractTenant(ctx context.Context) (uuid.UUID, error) {
	md, ok := metadata.FromIncomingContext(ctx)
	if !ok {
		return uuid.Nil, status.Error(codes.Unauthenticated, "missing metadata")
	}
	vals := md.Get("x-tenant-id")
	if len(vals) == 0 || strings.TrimSpace(vals[0]) == "" {
		return uuid.Nil, status.Error(codes.InvalidArgument, "x-tenant-id metadata header is required")
	}
	parsed, err := uuid.Parse(vals[0])
	if err != nil {
		return uuid.Nil, status.Error(codes.InvalidArgument, "invalid x-tenant-id UUID")
	}
	return parsed, nil
}

func (h *GpsHandler) IngestPosition(ctx context.Context, req *gpstrackingpb.IngestPositionRequest) (*gpstrackingpb.PositionResponse, error) {
	tenantId, err := h.extractTenant(ctx)
	if err != nil {
		return nil, err
	}

	if req.ExternalReadingId == "" || req.DeviceId == "" || req.VehicleId == "" {
		return nil, status.Error(codes.InvalidArgument, "external_reading_id, device_id, and vehicle_id are required")
	}
	if req.Latitude < -90.0 || req.Latitude > 90.0 || req.Longitude < -180.0 || req.Longitude > 180.0 {
		return nil, status.Error(codes.InvalidArgument, "coordinates outside valid range")
	}
	if req.RecordedAt == nil {
		return nil, status.Error(codes.InvalidArgument, "recorded_at is required")
	}

	pos, err := h.repo.IngestPosition(
		ctx,
		tenantId,
		req.DeviceId,
		req.VehicleId,
		req.ExternalReadingId,
		req.Latitude,
		req.Longitude,
		req.SpeedKph,
		req.HeadingDegrees,
		req.AccuracyMeters,
		req.RecordedAt.AsTime(),
	)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "ingest failed: %v", err)
	}

	return mapPositionResponse(pos), nil
}

func (h *GpsHandler) GetCurrentLocation(ctx context.Context, req *gpstrackingpb.GetCurrentLocationRequest) (*gpstrackingpb.CurrentLocationResponse, error) {
	tenantId, err := h.extractTenant(ctx)
	if err != nil {
		return nil, err
	}

	vehicleId := req.GetVehicleId()
	shipmentId := req.GetShipmentId()
	if vehicleId == "" && shipmentId == "" {
		return nil, status.Error(codes.InvalidArgument, "either vehicle_id or shipment_id must be provided")
	}

	current, err := h.repo.GetCurrentLocation(ctx, tenantId, vehicleId, shipmentId)
	if err != nil {
		return nil, status.Error(codes.NotFound, "current location not found")
	}

	res := &gpstrackingpb.CurrentLocationResponse{
		PositionId:     current.PositionId.String(),
		VehicleId:      current.VehicleId,
		Latitude:       current.Latitude,
		Longitude:      current.Longitude,
		SpeedKph:       current.SpeedKph,
		HeadingDegrees: current.HeadingDegrees,
		AccuracyMeters: current.AccuracyMeters,
		RecordedAt:     timestamppb.New(current.RecordedAt),
		ReceivedAt:     timestamppb.New(current.ReceivedAt),
	}
	if current.ShipmentId != nil {
		res.ShipmentId = current.ShipmentId.String()
	}
	return res, nil
}

func (h *GpsHandler) ListPositionHistory(ctx context.Context, req *gpstrackingpb.ListPositionHistoryRequest) (*gpstrackingpb.ListPositionHistoryResponse, error) {
	tenantId, err := h.extractTenant(ctx)
	if err != nil {
		return nil, err
	}

	vehicleId := req.GetVehicleId()
	shipmentId := req.GetShipmentId()

	var from, to timestamppb.Timestamp
	if req.From != nil {
		from = *req.From
	}
	if req.To != nil {
		to = *req.To
	}

	list, total, err := h.repo.ListPositionHistory(
		ctx,
		tenantId,
		vehicleId,
		shipmentId,
		from.AsTime(),
		to.AsTime(),
		int(req.Page),
		int(req.PageSize),
	)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "list history failed: %v", err)
	}

	res := &gpstrackingpb.ListPositionHistoryResponse{
		Page:       req.Page,
		PageSize:   req.PageSize,
		TotalItems: int32(total),
	}
	for _, p := range list {
		res.Positions = append(res.Positions, mapPositionResponse(&p))
	}
	return res, nil
}

func (h *GpsHandler) CreateGeofence(ctx context.Context, req *gpstrackingpb.CreateGeofenceRequest) (*gpstrackingpb.GeofenceResponse, error) {
	tenantId, err := h.extractTenant(ctx)
	if err != nil {
		return nil, err
	}

	var sId *uuid.UUID
	if req.ShipmentId != nil && *req.ShipmentId != "" {
		parsed, pErr := uuid.Parse(*req.ShipmentId)
		if pErr == nil {
			sId = &parsed
		}
	}

	fence, err := h.repo.CreateGeofence(ctx, tenantId, req.Name, req.Latitude, req.Longitude, req.RadiusMeters, sId, req.VehicleId)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "create geofence failed: %v", err)
	}

	return mapGeofenceResponse(fence), nil
}

func (h *GpsHandler) ListGeofences(ctx context.Context, req *gpstrackingpb.ListGeofencesRequest) (*gpstrackingpb.ListGeofencesResponse, error) {
	tenantId, err := h.extractTenant(ctx)
	if err != nil {
		return nil, err
	}

	list, err := h.repo.ListGeofences(ctx, tenantId, req.IncludeInactive)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "list geofences failed: %v", err)
	}

	res := &gpstrackingpb.ListGeofencesResponse{}
	for _, f := range list {
		res.Geofences = append(res.Geofences, mapGeofenceResponse(&f))
	}
	return res, nil
}

func (h *GpsHandler) SetGeofenceActive(ctx context.Context, req *gpstrackingpb.SetGeofenceActiveRequest) (*gpstrackingpb.GeofenceResponse, error) {
	tenantId, err := h.extractTenant(ctx)
	if err != nil {
		return nil, err
	}

	fenceId, err := uuid.Parse(req.Id)
	if err != nil {
		return nil, status.Error(codes.InvalidArgument, "invalid geofence id")
	}

	fence, err := h.repo.SetGeofenceActive(ctx, tenantId, fenceId, req.IsActive)
	if err != nil {
		return nil, status.Error(codes.NotFound, "geofence not found")
	}

	return mapGeofenceResponse(fence), nil
}

func (h *GpsHandler) ListMonitoringAlerts(ctx context.Context, req *gpstrackingpb.ListMonitoringAlertsRequest) (*gpstrackingpb.ListMonitoringAlertsResponse, error) {
	tenantId, err := h.extractTenant(ctx)
	if err != nil {
		return nil, err
	}

	alerts, total, err := h.repo.ListMonitoringAlerts(ctx, tenantId, int(req.Page), int(req.PageSize))
	if err != nil {
		return nil, status.Errorf(codes.Internal, "list alerts failed: %v", err)
	}

	res := &gpstrackingpb.ListMonitoringAlertsResponse{
		Page:       req.Page,
		PageSize:   req.PageSize,
		TotalItems: int32(total),
	}
	for _, a := range alerts {
		item := &gpstrackingpb.MonitoringAlertResponse{
			Id:         a.Id.String(),
			AlertType:  a.AlertType,
			Status:     a.Status,
			VehicleId:  a.VehicleId,
			Message:    a.Message,
			OccurredAt: timestamppb.New(a.OccurredAt),
		}
		if a.ShipmentId != nil {
			item.ShipmentId = a.ShipmentId.String()
		}
		if a.GeofenceId != nil {
			item.GeofenceId = a.GeofenceId.String()
		}
		if a.PositionId != nil {
			item.PositionId = a.PositionId.String()
		}
		if a.ResolvedAt != nil {
			item.ResolvedAt = timestamppb.New(*a.ResolvedAt)
		}
		res.Alerts = append(res.Alerts, item)
	}
	return res, nil
}

func (h *GpsHandler) ResolveMonitoringAlert(ctx context.Context, req *gpstrackingpb.ResolveMonitoringAlertRequest) (*gpstrackingpb.MonitoringAlertResponse, error) {
	tenantId, err := h.extractTenant(ctx)
	if err != nil {
		return nil, err
	}

	alertId, err := uuid.Parse(req.Id)
	if err != nil {
		return nil, status.Error(codes.InvalidArgument, "invalid alert id")
	}

	alert, err := h.repo.ResolveMonitoringAlert(ctx, tenantId, alertId)
	if err != nil {
		return nil, status.Error(codes.NotFound, "alert not found")
	}

	res := &gpstrackingpb.MonitoringAlertResponse{
		Id:         alert.Id.String(),
		AlertType:  alert.AlertType,
		Status:     alert.Status,
		VehicleId:  alert.VehicleId,
		Message:    alert.Message,
		OccurredAt: timestamppb.New(alert.OccurredAt),
	}
	if alert.ResolvedAt != nil {
		res.ResolvedAt = timestamppb.New(*alert.ResolvedAt)
	}
	return res, nil
}

func mapPositionResponse(pos *domain.GpsPosition) *gpstrackingpb.PositionResponse {
	res := &gpstrackingpb.PositionResponse{
		Id:                pos.Id.String(),
		ExternalReadingId: pos.ExternalReadingId,
		DeviceId:          pos.DeviceId,
		VehicleId:         pos.VehicleId,
		Latitude:          pos.Latitude,
		Longitude:         pos.Longitude,
		SpeedKph:          pos.SpeedKph,
		HeadingDegrees:    pos.HeadingDegrees,
		AccuracyMeters:    pos.AccuracyMeters,
		RecordedAt:        timestamppb.New(pos.RecordedAt),
		ReceivedAt:        timestamppb.New(pos.ReceivedAt),
	}
	if pos.ShipmentId != nil {
		res.ShipmentId = pos.ShipmentId.String()
	}
	return res
}

func mapGeofenceResponse(f *domain.Geofence) *gpstrackingpb.GeofenceResponse {
	res := &gpstrackingpb.GeofenceResponse{
		Id:           f.Id.String(),
		Name:         f.Name,
		Latitude:     f.Latitude,
		Longitude:    f.Longitude,
		RadiusMeters: f.RadiusMeters,
		IsActive:     f.IsActive,
	}
	if f.ShipmentId != nil {
		res.ShipmentId = f.ShipmentId.String()
	}
	if f.VehicleId != nil {
		res.VehicleId = *f.VehicleId
	}
	return res
}

