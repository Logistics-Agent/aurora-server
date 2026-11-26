package generator

import (
	"testing"

	"aurora/gps-loadgen/config"
	"aurora/gps-loadgen/model"
)

func TestTelemetryGenerator_Sequential(t *testing.T) {
	traj := model.DefaultCorridorTrajectory()
	v := &model.VehicleSimulation{
		ShipmentId: "ship-1",
		VehicleId:  "veh-1",
		DeviceId:   "sim-dev-veh-1",
		Trajectory: traj,
		Behavior:   config.BehaviorNormal,
	}

	gen := NewTelemetryGenerator(50.0, 42, true)

	req1 := gen.GenerateNext(v)
	if req1 == nil {
		t.Fatal("expected req1 not nil")
	}
	if req1.ExternalReadingId != "sim-veh-1-00000001" {
		t.Errorf("unexpected reading id 1: %s", req1.ExternalReadingId)
	}
	if v.ExternalReadingSequence != 1 {
		t.Errorf("expected sequence 1, got %d", v.ExternalReadingSequence)
	}

	req2 := gen.GenerateNext(v)
	if req2 == nil {
		t.Fatal("expected req2 not nil")
	}
	if req2.ExternalReadingId != "sim-veh-1-00000002" {
		t.Errorf("unexpected reading id 2: %s", req2.ExternalReadingId)
	}
	if v.ExternalReadingSequence != 2 {
		t.Errorf("expected sequence 2, got %d", v.ExternalReadingSequence)
	}
}

func TestTelemetryGenerator_Deviation(t *testing.T) {
	traj := model.DefaultCorridorTrajectory()
	v := &model.VehicleSimulation{
		ShipmentId: "ship-dev",
		VehicleId:  "veh-dev",
		DeviceId:   "sim-dev-veh-dev",
		Trajectory: traj,
		Behavior:   config.BehaviorDeviation,
	}

	gen := NewTelemetryGenerator(50.0, 42, true)
	req := gen.GenerateNext(v)
	if req == nil {
		t.Fatal("expected req not nil")
	}
	if v.DevLatOffset == 0.0 && v.DevLngOffset == 0.0 {
		t.Error("expected deviation offset to be non-zero")
	}
	if req.AccuracyMeters == nil || *req.AccuracyMeters != 8.0 {
		t.Errorf("expected deviation accuracy 8.0, got %v", req.AccuracyMeters)
	}
}

