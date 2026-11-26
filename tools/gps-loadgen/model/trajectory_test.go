package model

import (
	"math"
	"testing"
)

func TestInterpolateRoute(t *testing.T) {
	waypoints := []Coordinate{
		{Lat: 10.0, Lng: 20.0},
		{Lat: 11.0, Lng: 21.0},
		{Lat: 12.0, Lng: 22.0},
	}

	traj := FromWaypoints(waypoints, "Test", 10)
	if len(traj.Points) != 21 {
		t.Fatalf("expected 21 points, got %d", len(traj.Points))
	}
	if math.Abs(traj.Points[0].Lat-10.0) > 0.0001 {
		t.Errorf("start point lat mismatch: %f", traj.Points[0].Lat)
	}
	if math.Abs(traj.Points[len(traj.Points)-1].Lat-12.0) > 0.0001 {
		t.Errorf("end point lat mismatch: %f", traj.Points[len(traj.Points)-1].Lat)
	}
}

func TestCalculateHeading(t *testing.T) {
	north := CalculateHeading(0.0, 0.0, 1.0, 0.0)
	if math.Abs(north-0.0) > 0.5 {
		t.Errorf("expected 0.0 for North, got %f", north)
	}

	east := CalculateHeading(0.0, 0.0, 0.0, 1.0)
	if math.Abs(east-90.0) > 0.5 {
		t.Errorf("expected 90.0 for East, got %f", east)
	}

	south := CalculateHeading(1.0, 0.0, 0.0, 0.0)
	if math.Abs(south-180.0) > 0.5 {
		t.Errorf("expected 180.0 for South, got %f", south)
	}

	west := CalculateHeading(0.0, 1.0, 0.0, 0.0)
	if math.Abs(west-270.0) > 0.5 {
		t.Errorf("expected 270.0 for West, got %f", west)
	}
}

func TestHaversineDistance(t *testing.T) {
	// Distance between San Jose CR and Panama City
	dist := HaversineDistanceKm(9.9333, -84.0833, 8.9824, -79.5199)
	if dist < 500.0 || dist > 550.0 {
		t.Errorf("expected distance in [500, 550] km, got %f", dist)
	}
}

