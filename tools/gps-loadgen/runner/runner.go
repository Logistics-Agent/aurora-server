package runner

import (
	"context"
	"fmt"
	"strings"
	"sync"
	"sync/atomic"
	"time"

	"aurora/gps-loadgen/config"
	"aurora/gps-loadgen/generator"
	"aurora/gps-loadgen/metrics"
	"aurora/gps-loadgen/model"
	"aurora/gps-loadgen/proto/gpstrackingpb"

	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/metadata"
	"google.golang.org/grpc/status"
)

type Runner struct {
	client       gpstrackingpb.GpsTrackingServiceClient
	telemetryGen *generator.TelemetryGenerator
	metrics      *metrics.SimulationMetrics
	cfg          *config.SimulationConfig
}

func NewRunner(
	client gpstrackingpb.GpsTrackingServiceClient,
	telemetryGen *generator.TelemetryGenerator,
	metrics *metrics.SimulationMetrics,
	cfg *config.SimulationConfig,
) *Runner {
	return &Runner{
		client:       client,
		telemetryGen: telemetryGen,
		metrics:      metrics,
		cfg:          cfg,
	}
}

func (r *Runner) Run(ctx context.Context, vehicles []*model.VehicleSimulation) error {
	r.metrics.Start(len(vehicles))

	var cancel context.CancelFunc
	if r.cfg.Duration > 0 {
		ctx, cancel = context.WithTimeout(ctx, r.cfg.Duration)
		defer cancel()
	}

	maxConc := r.cfg.MaxConcurrency
	if maxConc <= 0 || maxConc > len(vehicles) {
		maxConc = len(vehicles)
	}
	sem := make(chan struct{}, maxConc)

	var activeCount int32 = int32(len(vehicles))
	var wg sync.WaitGroup

	// Progress reporter ticker
	progressTicker := time.NewTicker(5 * time.Second)
	defer progressTicker.Stop()
	go func() {
		for {
			select {
			case <-ctx.Done():
				return
			case <-progressTicker.C:
				r.metrics.PrintProgress(int(atomic.LoadInt32(&activeCount)))
			}
		}
	}()

	isSingleMode := r.cfg.Mode == config.ModeSingle && len(vehicles) == 1

	for _, v := range vehicles {
		wg.Add(1)
		go func(veh *model.VehicleSimulation) {
			defer wg.Done()
			select {
			case sem <- struct{}{}:
				defer func() { <-sem }()
			case <-ctx.Done():
				atomic.AddInt32(&activeCount, -1)
				return
			}

			defer atomic.AddInt32(&activeCount, -1)
			r.runVehicle(ctx, veh, isSingleMode)
		}(v)
	}

	wg.Wait()
	r.metrics.Stop()
	r.metrics.PrintSummary()

	if ctx.Err() == context.DeadlineExceeded {
		fmt.Printf("Simulation reached target duration of %s.\n", r.cfg.Duration)
	}
	return nil
}

func (r *Runner) runVehicle(ctx context.Context, v *model.VehicleSimulation, isSingleMode bool) {
	ticker := time.NewTicker(r.cfg.Interval)
	defer ticker.Stop()

	md := metadata.Pairs(
		"x-tenant-id", r.cfg.TenantId,
		"x-user-id", r.cfg.UserId,
	)
	outCtx := metadata.NewOutgoingContext(ctx, md)

	totalPoints := len(v.Trajectory.Points)
	stepNumber := 0
	loopForever := r.cfg.Duration > 0 || r.cfg.Mode != config.ModeSingle

	for {
		stepNumber++
		req := r.telemetryGen.GenerateNext(v)
		if req != nil {
			reqCtx, reqCancel := context.WithTimeout(outCtx, 5*time.Second)
			start := time.Now()
			_, err := r.client.IngestPosition(reqCtx, req)
			reqCancel()
			latencyMs := float64(time.Since(start).Microseconds()) / 1000.0

			if err == nil {
				r.metrics.RecordSuccess(latencyMs)
				if isSingleMode {
					speed := 0.0
					if req.SpeedKph != nil {
						speed = *req.SpeedKph
					}
					fmt.Printf("[%d/%d] %.4f,%.4f | %.0f km/h (lat: %.1fms)\n",
						stepNumber, totalPoints, req.Latitude, req.Longitude, speed, latencyMs)
				}
			} else {
				st, ok := status.FromError(err)
				isTimeout := ok && (st.Code() == codes.DeadlineExceeded || strings.Contains(err.Error(), "context deadline exceeded"))
				r.metrics.RecordFailure(isTimeout)
				if isSingleMode {
					fmt.Printf("[WARN] IngestPosition failed: %v\n", err)
				}
			}
		} else {
			if isSingleMode {
				fmt.Printf("[%d/%d] [SIGNAL LOSS] Telemetry dropped.\n", stepNumber, totalPoints)
			}
		}

		if !loopForever && stepNumber >= totalPoints {
			break
		}

		select {
		case <-ctx.Done():
			return
		case <-ticker.C:
		}
	}
}

