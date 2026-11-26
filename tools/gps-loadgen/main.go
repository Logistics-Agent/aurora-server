package main

import (
	"context"
	"fmt"
	"os"
	"os/signal"
	"strings"
	"syscall"

	"aurora/gps-loadgen/config"
	"aurora/gps-loadgen/discovery"
	"aurora/gps-loadgen/generator"
	"aurora/gps-loadgen/metrics"
	"aurora/gps-loadgen/proto/gpstrackingpb"
	"aurora/gps-loadgen/proto/shipmentpb"
	"aurora/gps-loadgen/runner"

	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
)

func cleanEndpoint(url string) string {
	url = strings.TrimPrefix(url, "http://")
	url = strings.TrimPrefix(url, "https://")
	return url
}

func main() {
	cfg, err := config.ParseFlags()
	if err != nil {
		fmt.Fprintf(os.Stderr, "Error: %v\n", err)
		os.Exit(1)
	}

	ctx, cancel := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer cancel()

	fmt.Println("=========================================================")
	fmt.Println("              Aurora GPS Go Load-Generator               ")
	fmt.Println("=========================================================")
	fmt.Printf("Mode          : %s\n", cfg.Mode)
	if cfg.Profile != "" {
		fmt.Printf("Profile       : %s\n", cfg.Profile)
	}
	if cfg.Mode == config.ModeSingle {
		fmt.Printf("Shipment      : %s\n", cfg.SingleShipmentId)
	} else {
		fmt.Printf("Vehicles Limit: %d\n", cfg.VehiclesLimit)
	}
	fmt.Printf("Behavior      : %s\n", cfg.Behavior)
	fmt.Printf("Interval      : %s | Base Speed: %.1f km/h\n", cfg.Interval, cfg.BaseSpeedKph)
	if cfg.Duration > 0 {
		fmt.Printf("Duration      : %s\n", cfg.Duration)
	}
	if cfg.HasSeed {
		fmt.Printf("Random Seed   : %d\n", cfg.RandomSeed)
	}
	fmt.Printf("GPS URL       : %s\n", cfg.GpsGrpcUrl)
	fmt.Printf("Shipment URL  : %s\n", cfg.ShipmentGrpcUrl)
	fmt.Printf("Tenant ID     : %s\n", cfg.TenantId)
	fmt.Printf("Max Concurrency: %d\n", cfg.MaxConcurrency)
	fmt.Println("=========================================================")
	fmt.Println()

	// 1. Connect to GPS Tracking Service
	gpsConn, err := grpc.NewClient(cleanEndpoint(cfg.GpsGrpcUrl), grpc.WithTransportCredentials(insecure.NewCredentials()))
	if err != nil {
		fmt.Fprintf(os.Stderr, "Failed to connect to GPS service at %s: %v\n", cfg.GpsGrpcUrl, err)
		os.Exit(2)
	}
	defer gpsConn.Close()
	gpsClient := gpstrackingpb.NewGpsTrackingServiceClient(gpsConn)

	// 2. Connect to Shipment Workflow Service (optional for discovery)
	var shipmentClient shipmentpb.ShipmentWorkflowServiceClient
	shipmentConn, err := grpc.NewClient(cleanEndpoint(cfg.ShipmentGrpcUrl), grpc.WithTransportCredentials(insecure.NewCredentials()))
	if err == nil {
		defer shipmentConn.Close()
		shipmentClient = shipmentpb.NewShipmentWorkflowServiceClient(shipmentConn)
	}

	// 3. Discover/Generate vehicles
	fmt.Println("Discovering vehicle simulation candidates...")
	vehicles, err := discovery.DiscoverVehicles(ctx, cfg, shipmentClient)
	if err != nil {
		fmt.Fprintf(os.Stderr, "Discovery failed: %v\n", err)
		os.Exit(3)
	}

	fmt.Printf("Initialized %d active vehicle simulation(s).\n\n", len(vehicles))

	// 4. Run load generation
	telemetryGen := generator.NewTelemetryGenerator(cfg.BaseSpeedKph, cfg.RandomSeed, cfg.HasSeed)
	simMetrics := metrics.NewSimulationMetrics()
	simRunner := runner.NewRunner(gpsClient, telemetryGen, simMetrics, cfg)

	if err := simRunner.Run(ctx, vehicles); err != nil {
		fmt.Fprintf(os.Stderr, "Simulation error: %v\n", err)
		os.Exit(4)
	}
}

