package config

import (
	"flag"
	"fmt"
	"os"
	"strings"
	"time"
)

type SimulationMode string

const (
	ModeSingle SimulationMode = "single"
	ModeAuto   SimulationMode = "auto"
	ModeFleet  SimulationMode = "fleet"
)

type SimulationBehavior string

const (
	BehaviorNormal     SimulationBehavior = "normal"
	BehaviorMixed      SimulationBehavior = "mixed"
	BehaviorDeviation  SimulationBehavior = "deviation"
	BehaviorSignalLoss SimulationBehavior = "signal-loss"
)

type SimulationConfig struct {
	Mode             SimulationMode
	Profile          string
	SingleShipmentId string
	ShipmentsFile    string
	VehiclesLimit    int
	Interval         time.Duration
	BaseSpeedKph     float64
	Behavior         SimulationBehavior
	Duration         time.Duration
	RandomSeed       int64
	HasSeed          bool
	GpsGrpcUrl       string
	ShipmentGrpcUrl  string
	RouteGrpcUrl     string
	TenantId         string
	UserId           string
	AutoAssign       bool
	MaxConcurrency   int
}

func getEnvOrDefault(key, fallback string) string {
	if val := os.Getenv(key); val != "" {
		return val
	}
	return fallback
}

func ParseFlags() (*SimulationConfig, error) {
	cfg := &SimulationConfig{
		GpsGrpcUrl:      getEnvOrDefault("GPS_GRPC_URL", "http://localhost:6002"),
		ShipmentGrpcUrl: getEnvOrDefault("SHIPMENT_GRPC_URL", "http://localhost:6000"),
		RouteGrpcUrl:    getEnvOrDefault("ROUTE_GRPC_URL", "http://localhost:5002"),
		TenantId:        getEnvOrDefault("TENANT_ID", "01920000-0000-7000-8000-000000000001"),
		UserId:          getEnvOrDefault("USER_ID", "01910000-0000-7000-8000-000000000001"),
	}

	modeStr := flag.String("mode", "auto", "Simulation mode: single, auto, fleet")
	profileStr := flag.String("profile", "", "Load profile shortcut: A, B, C, D, E")
	shipmentId := flag.String("shipment", "", "Single shipment UUID")
	shipmentsFile := flag.String("shipments", "", "Path to JSON file containing shipment IDs")
	vehicles := flag.Int("vehicles", 10, "Number of virtual vehicles to simulate")
	intervalSec := flag.Float64("interval", 3.0, "Interval in seconds between telemetry readings")
	speed := flag.Float64("speed", 50.0, "Base speed in km/h")
	behaviorStr := flag.String("behavior", "normal", "Behavior profile: normal, mixed, deviation, signal-loss")
	durationSec := flag.Int("duration", 0, "Simulation duration in seconds (0 = until finished)")
	seedVal := flag.Int64("seed", -1, "Deterministic random seed")
	gpsUrl := flag.String("gps", "", "GPS Tracking gRPC URL")
	grpcUrl := flag.String("grpc", "", "Alias for -gps")
	shipmentUrl := flag.String("shipment-grpc", "", "Shipment Workflow gRPC URL")
	routeUrl := flag.String("route-grpc", "", "Route Planning gRPC URL")
	tenant := flag.String("tenant", "", "Tenant ID")
	userId := flag.String("user-id", "", "User ID")
	autoAssign := flag.Bool("auto-assign", false, "Auto-assign vehicle ID to shipment")
	concurrency := flag.Int("concurrency", 1000, "Max concurrent active vehicle tasks")

	flag.Parse()

	if *gpsUrl != "" {
		cfg.GpsGrpcUrl = *gpsUrl
	} else if *grpcUrl != "" {
		cfg.GpsGrpcUrl = *grpcUrl
	}
	if *shipmentUrl != "" {
		cfg.ShipmentGrpcUrl = *shipmentUrl
	}
	if *routeUrl != "" {
		cfg.RouteGrpcUrl = *routeUrl
	}
	if *tenant != "" {
		cfg.TenantId = *tenant
	}
	if *userId != "" {
		cfg.UserId = *userId
	}

	cfg.SingleShipmentId = *shipmentId
	cfg.ShipmentsFile = *shipmentsFile
	cfg.VehiclesLimit = *vehicles
	cfg.Interval = time.Duration(*intervalSec * float64(time.Second))
	cfg.BaseSpeedKph = *speed
	cfg.AutoAssign = *autoAssign
	cfg.MaxConcurrency = *concurrency

	if *durationSec > 0 {
		cfg.Duration = time.Duration(*durationSec) * time.Second
	}
	if *seedVal >= 0 {
		cfg.RandomSeed = *seedVal
		cfg.HasSeed = true
	}

	// Profile override
	if *profileStr != "" {
		cfg.Profile = strings.ToUpper(*profileStr)
		cfg.Mode = ModeFleet
		switch cfg.Profile {
		case "A":
			cfg.VehiclesLimit = 10
			cfg.Interval = 5 * time.Second
		case "B":
			cfg.VehiclesLimit = 50
			cfg.Interval = 3 * time.Second
		case "C":
			cfg.VehiclesLimit = 100
			cfg.Interval = 1 * time.Second
		case "D":
			cfg.VehiclesLimit = 500
			cfg.Interval = 1 * time.Second
		case "E":
			cfg.VehiclesLimit = 1000
			cfg.Interval = 5 * time.Second
		default:
			return nil, fmt.Errorf("unknown load profile: %s (allowed: A, B, C, D, E)", *profileStr)
		}
	} else {
		if cfg.SingleShipmentId != "" {
			cfg.Mode = ModeSingle
		} else {
			cfg.Mode = SimulationMode(strings.ToLower(*modeStr))
		}
	}

	switch strings.ToLower(*behaviorStr) {
	case "normal":
		cfg.Behavior = BehaviorNormal
	case "mixed":
		cfg.Behavior = BehaviorMixed
	case "deviation":
		cfg.Behavior = BehaviorDeviation
	case "signal-loss", "signalloss":
		cfg.Behavior = BehaviorSignalLoss
	default:
		return nil, fmt.Errorf("unknown behavior: %s (allowed: normal, mixed, deviation, signal-loss)", *behaviorStr)
	}

	if cfg.Mode == ModeSingle && cfg.SingleShipmentId == "" {
		return nil, fmt.Errorf("-shipment is required when running in single mode")
	}

	return cfg, nil
}

