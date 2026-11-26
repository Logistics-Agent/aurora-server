package model

import "aurora/gps-loadgen/config"

type VehicleSimulation struct {
	ShipmentId              string
	VehicleId               string
	DeviceId                string
	RouteId                 string
	Trajectory              *RouteTrajectory
	CurrentPosition         Coordinate
	CurrentSpeed            float64
	Heading                 float64
	CurrentWaypointIndex    int
	ExternalReadingSequence int64
	Behavior                config.SimulationBehavior

	SignalLossSteps int
	DevLatOffset    float64
	DevLngOffset    float64
}

