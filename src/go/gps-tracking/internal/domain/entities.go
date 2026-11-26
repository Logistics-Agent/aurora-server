package domain

import (
	"time"

	"github.com/google/uuid"
)

type GpsPosition struct {
	Id                uuid.UUID  `gorm:"type:uuid;primaryKey;column:Id"`
	TenantId          uuid.UUID  `gorm:"type:uuid;not null;column:TenantId"`
	DeviceId          string     `gorm:"type:varchar(100);not null;column:DeviceId"`
	VehicleId         string     `gorm:"type:varchar(100);not null;column:VehicleId"`
	ShipmentId        *uuid.UUID `gorm:"type:uuid;column:ShipmentId"`
	ExternalReadingId string     `gorm:"type:varchar(150);not null;column:ExternalReadingId"`
	Latitude          float64    `gorm:"type:numeric(9,6);not null;column:Latitude"`
	Longitude         float64    `gorm:"type:numeric(10,6);not null;column:Longitude"`
	SpeedKph          *float64   `gorm:"type:numeric(8,3);column:SpeedKph"`
	HeadingDegrees    *float64   `gorm:"type:numeric(6,2);column:HeadingDegrees"`
	AccuracyMeters    *float64   `gorm:"type:numeric(10,2);column:AccuracyMeters"`
	RecordedAt        time.Time  `gorm:"not null;column:RecordedAt"`
	ReceivedAt        time.Time  `gorm:"not null;column:ReceivedAt"`
	CreatedAt         time.Time  `gorm:"not null;column:CreatedAt"`
}

func (GpsPosition) TableName() string {
	return "gps_positions"
}

type CurrentLocation struct {
	Id              uuid.UUID  `gorm:"type:uuid;primaryKey;column:Id"`
	TenantId        uuid.UUID  `gorm:"type:uuid;not null;column:TenantId"`
	PositionId      uuid.UUID  `gorm:"type:uuid;not null;column:PositionId"`
	VehicleId       string     `gorm:"type:varchar(100);not null;column:VehicleId"`
	DeviceId        string     `gorm:"type:varchar(100);not null;column:DeviceId"`
	ShipmentId      *uuid.UUID `gorm:"type:uuid;column:ShipmentId"`
	Latitude        float64    `gorm:"type:numeric(9,6);not null;column:Latitude"`
	Longitude       float64    `gorm:"type:numeric(10,6);not null;column:Longitude"`
	SpeedKph        *float64   `gorm:"type:numeric(8,3);column:SpeedKph"`
	HeadingDegrees  *float64   `gorm:"type:numeric(6,2);column:HeadingDegrees"`
	AccuracyMeters  *float64   `gorm:"type:numeric(10,2);column:AccuracyMeters"`
	RecordedAt      time.Time  `gorm:"not null;column:RecordedAt"`
	ReceivedAt      time.Time  `gorm:"not null;column:ReceivedAt"`
	IsStationary    bool       `gorm:"not null;column:IsStationary"`
	StationarySince *time.Time `gorm:"column:StationarySince"`
	CreatedAt       time.Time  `gorm:"not null;column:CreatedAt"`
	UpdatedAt       *time.Time `gorm:"column:UpdatedAt"`
}

func (CurrentLocation) TableName() string {
	return "current_locations"
}

type VehicleShipmentAssignment struct {
	Id         uuid.UUID  `gorm:"type:uuid;primaryKey;column:Id"`
	TenantId   uuid.UUID  `gorm:"type:uuid;not null;column:TenantId"`
	ShipmentId uuid.UUID  `gorm:"type:uuid;not null;column:ShipmentId"`
	VehicleId  string     `gorm:"type:varchar(100);not null;column:VehicleId"`
	RouteId    string     `gorm:"type:varchar(100);not null;column:RouteId"`
	AssignedAt time.Time  `gorm:"not null;column:AssignedAt"`
	EndedAt    *time.Time `gorm:"column:EndedAt"`
}

func (VehicleShipmentAssignment) TableName() string {
	return "vehicle_shipment_assignments"
}

type Geofence struct {
	Id           uuid.UUID  `gorm:"type:uuid;primaryKey;column:Id"`
	TenantId     uuid.UUID  `gorm:"type:uuid;not null;column:TenantId"`
	Name         string     `gorm:"type:varchar(150);not null;column:Name"`
	Latitude     float64    `gorm:"type:numeric(9,6);not null;column:Latitude"`
	Longitude    float64    `gorm:"type:numeric(10,6);not null;column:Longitude"`
	RadiusMeters float64    `gorm:"type:numeric(12,2);not null;column:RadiusMeters"`
	ShipmentId   *uuid.UUID `gorm:"type:uuid;column:ShipmentId"`
	VehicleId    *string    `gorm:"type:varchar(100);column:VehicleId"`
	IsActive     bool       `gorm:"not null;column:IsActive"`
}

func (Geofence) TableName() string {
	return "geofences"
}

type GeofencePresence struct {
	Id         uuid.UUID  `gorm:"type:uuid;primaryKey;column:Id"`
	TenantId   uuid.UUID  `gorm:"type:uuid;not null;column:TenantId"`
	GeofenceId uuid.UUID  `gorm:"type:uuid;not null;column:GeofenceId"`
	VehicleId  string     `gorm:"type:varchar(100);not null;column:VehicleId"`
	IsInside   bool       `gorm:"not null;column:IsInside"`
	ObservedAt time.Time  `gorm:"not null;column:ObservedAt"`
	CreatedAt  time.Time  `gorm:"not null;column:CreatedAt"`
	UpdatedAt  *time.Time `gorm:"column:UpdatedAt"`
}

func (GeofencePresence) TableName() string {
	return "geofence_presences"
}

type ShipmentTrackingState struct {
	Id          uuid.UUID  `gorm:"type:uuid;primaryKey;column:Id"`
	TenantId    uuid.UUID  `gorm:"type:uuid;not null;column:TenantId"`
	ShipmentId  uuid.UUID  `gorm:"type:uuid;not null;column:ShipmentId"`
	IsClosed    bool       `gorm:"not null;column:IsClosed"`
	LastEventAt time.Time  `gorm:"not null;column:LastEventAt"`
	CreatedAt   time.Time  `gorm:"not null;column:CreatedAt"`
	UpdatedAt   *time.Time `gorm:"column:UpdatedAt"`
}

func (ShipmentTrackingState) TableName() string {
	return "shipment_tracking_states"
}

type ConsumedIntegrationEvent struct {
	Id              uuid.UUID `gorm:"type:uuid;primaryKey;column:Id"`
	TenantId        uuid.UUID `gorm:"type:uuid;not null;column:TenantId"`
	SourceEventId   uuid.UUID `gorm:"type:uuid;not null;column:SourceEventId"`
	SourceEventType string    `gorm:"type:varchar(256);not null;column:SourceEventType"`
	ContractVersion int       `gorm:"not null;column:ContractVersion"`
	ReceivedAt      time.Time `gorm:"not null;column:ReceivedAt"`
	CreatedAt       time.Time `gorm:"not null;column:CreatedAt"`
}

func (ConsumedIntegrationEvent) TableName() string {
	return "consumed_integration_events"
}

type MonitoringAlert struct {
	Id               uuid.UUID  `gorm:"type:uuid;primaryKey;column:Id"`
	TenantId         uuid.UUID  `gorm:"type:uuid;not null;column:TenantId"`
	AlertType        string     `gorm:"type:varchar(40);not null;column:AlertType"`
	Status           string     `gorm:"type:varchar(20);not null;column:Status"`
	VehicleId        string     `gorm:"type:varchar(100);not null;column:VehicleId"`
	ShipmentId       *uuid.UUID `gorm:"type:uuid;column:ShipmentId"`
	GeofenceId       *uuid.UUID `gorm:"type:uuid;column:GeofenceId"`
	PositionId       *uuid.UUID `gorm:"type:uuid;column:PositionId"`
	DeduplicationKey string     `gorm:"type:varchar(300);not null;column:DeduplicationKey"`
	Message          string     `gorm:"type:varchar(1000);not null;column:Message"`
	OccurredAt       time.Time  `gorm:"not null;column:OccurredAt"`
	ResolvedAt       *time.Time `gorm:"column:ResolvedAt"`
}

func (MonitoringAlert) TableName() string {
	return "monitoring_alerts"
}

type OutboxMessage struct {
	Id          uuid.UUID  `gorm:"type:uuid;primaryKey;column:Id"`
	TenantId    uuid.UUID  `gorm:"type:uuid;not null;column:TenantId"`
	EventId     uuid.UUID  `gorm:"type:uuid;not null;column:EventId"`
	EventType   string     `gorm:"type:varchar(256);not null;column:EventType"`
	Content     string     `gorm:"type:text;not null;column:Content"`
	OccurredAt  time.Time  `gorm:"not null;column:OccurredAt"`
	ProcessedAt *time.Time `gorm:"column:ProcessedAt"`
	RetryCount  int        `gorm:"not null;column:RetryCount"`
	Error       *string    `gorm:"type:varchar(2000);column:Error"`
}

func (OutboxMessage) TableName() string {
	return "outbox_messages"
}

