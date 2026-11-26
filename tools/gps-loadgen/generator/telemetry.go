package generator

import (
	"fmt"
	"math"
	"math/rand"
	"sync"
	"time"

	"aurora/gps-loadgen/config"
	"aurora/gps-loadgen/model"
	"aurora/gps-loadgen/proto/gpstrackingpb"

	"google.golang.org/protobuf/types/known/timestamppb"
)

type TelemetryGenerator struct {
	baseSpeed float64
	rnd       *rand.Rand
	mu        sync.Mutex
}

func NewTelemetryGenerator(baseSpeed float64, seed int64, hasSeed bool) *TelemetryGenerator {
	var r *rand.Rand
	if hasSeed {
		r = rand.New(rand.NewSource(seed))
	} else {
		r = rand.New(rand.NewSource(time.Now().UnixNano()))
	}
	return &TelemetryGenerator{
		baseSpeed: baseSpeed,
		rnd:       r,
	}
}

func (g *TelemetryGenerator) GenerateNext(v *model.VehicleSimulation) *gpstrackingpb.IngestPositionRequest {
	points := v.Trajectory.Points
	if len(points) == 0 {
		return nil
	}

	g.mu.Lock()
	defer g.mu.Unlock()

	// Handle SignalLoss behavior
	if v.Behavior == config.BehaviorSignalLoss {
		if v.SignalLossSteps > 0 {
			v.SignalLossSteps--
			return nil
		}
		if g.rnd.Float64() < 0.15 {
			v.SignalLossSteps = g.rnd.Intn(4) + 2 // 2-5 intervals
			return nil
		}
	}

	currIdx := v.CurrentWaypointIndex
	currPoint := points[currIdx]

	nextIdx := (currIdx + 1) % len(points)
	v.CurrentWaypointIndex = nextIdx

	nextPoint := points[nextIdx]
	heading := model.CalculateHeading(currPoint.Lat, currPoint.Lng, nextPoint.Lat, nextPoint.Lng)

	// Speed variation (+/- 4 km/h)
	speed := math.Round((g.baseSpeed+(g.rnd.Float64()*8.0-4.0))*10.0) / 10.0
	if speed < 0 {
		speed = 0
	}

	lat := currPoint.Lat
	lng := currPoint.Lng
	accuracy := 3.5

	// Handle Deviation behavior
	if v.Behavior == config.BehaviorDeviation {
		v.DevLatOffset += (g.rnd.Float64() - 0.5) * 0.002
		v.DevLngOffset += (g.rnd.Float64() - 0.5) * 0.002
		lat += v.DevLatOffset
		lng += v.DevLngOffset
		accuracy = 8.0
	}

	v.CurrentPosition = model.Coordinate{Lat: lat, Lng: lng}
	v.CurrentSpeed = speed
	v.Heading = heading
	v.ExternalReadingSequence++

	extReadingId := fmt.Sprintf("sim-%s-%08d", v.VehicleId, v.ExternalReadingSequence)

	return &gpstrackingpb.IngestPositionRequest{
		ExternalReadingId: extReadingId,
		DeviceId:          v.DeviceId,
		VehicleId:         v.VehicleId,
		Latitude:          lat,
		Longitude:         lng,
		SpeedKph:          &speed,
		HeadingDegrees:    &heading,
		AccuracyMeters:    &accuracy,
		RecordedAt:        timestamppb.Now(),
	}
}

