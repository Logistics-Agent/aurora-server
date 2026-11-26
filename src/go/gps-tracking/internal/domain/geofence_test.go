package domain

import (
	"math"
	"testing"
)

func TestDistanceMeters(t *testing.T) {
	// San Jose CR (9.9333, -84.0833) to point 1km north (approx 9.9423, -84.0833)
	dist := DistanceMeters(9.9333, -84.0833, 9.9423, -84.0833)
	// 0.009 deg lat is approx 1000m
	if dist < 950.0 || dist > 1050.0 {
		t.Errorf("expected approx 1000m, got %f", dist)
	}
}

func TestDistanceMeters_Zero(t *testing.T) {
	dist := DistanceMeters(10.0, 20.0, 10.0, 20.0)
	if math.Abs(dist) > 0.001 {
		t.Errorf("expected 0m, got %f", dist)
	}
}

