package model

import (
	"math"
)

type Coordinate struct {
	Lat float64
	Lng float64
}

type RouteTrajectory struct {
	Waypoints   []Coordinate
	Points      []Coordinate
	Description string
}

func FromWaypoints(waypoints []Coordinate, description string, stepsBetween int) *RouteTrajectory {
	if len(waypoints) == 0 {
		return &RouteTrajectory{Description: description}
	}
	if len(waypoints) == 1 {
		return &RouteTrajectory{
			Waypoints:   waypoints,
			Points:      []Coordinate{waypoints[0]},
			Description: description,
		}
	}

	points := make([]Coordinate, 0, (len(waypoints)-1)*stepsBetween+1)
	for i := 0; i < len(waypoints)-1; i++ {
		start := waypoints[i]
		end := waypoints[i+1]
		for step := 0; step < stepsBetween; step++ {
			progress := float64(step) / float64(stepsBetween)
			lat := start.Lat + progress*(end.Lat-start.Lat)
			lng := start.Lng + progress*(end.Lng-start.Lng)
			points = append(points, Coordinate{Lat: lat, Lng: lng})
		}
	}
	points = append(points, waypoints[len(waypoints)-1])

	return &RouteTrajectory{
		Waypoints:   waypoints,
		Points:      points,
		Description: description,
	}
}

func CalculateHeading(lat1, lon1, lat2, lon2 float64) float64 {
	dLon := (lon2 - lon1) * math.Pi / 180.0
	y := math.Sin(dLon) * math.Cos(lat2*math.Pi/180.0)
	x := math.Cos(lat1*math.Pi/180.0)*math.Sin(lat2*math.Pi/180.0) -
		math.Sin(lat1*math.Pi/180.0)*math.Cos(lat2*math.Pi/180.0)*math.Cos(dLon)
	radians := math.Atan2(y, x)
	degrees := math.Mod(radians*180.0/math.Pi+360.0, 360.0)
	return math.Round(degrees*10.0) / 10.0
}

func HaversineDistanceKm(lat1, lon1, lat2, lon2 float64) float64 {
	const r = 6371.0
	dLat := (lat2 - lat1) * math.Pi / 180.0
	dLon := (lon2 - lon1) * math.Pi / 180.0
	a := math.Sin(dLat/2)*math.Sin(dLat/2) +
		math.Cos(lat1*math.Pi/180.0)*math.Cos(lat2*math.Pi/180.0)*
			math.Sin(dLon/2)*math.Sin(dLon/2)
	c := 2 * math.Atan2(math.Sqrt(a), math.Sqrt(1-a))
	return r * c
}

func DefaultCorridorTrajectory() *RouteTrajectory {
	waypoints := []Coordinate{
		{Lat: 9.9333, Lng: -84.0833}, // San Jose Hub
		{Lat: 9.8653, Lng: -83.9189}, // Cartago
		{Lat: 9.3789, Lng: -83.7042}, // San Isidro
		{Lat: 8.9667, Lng: -83.5167}, // Palmar Norte
		{Lat: 8.5333, Lng: -82.8333}, // Paso Canoas Border
		{Lat: 8.4333, Lng: -82.4333}, // David Hub
		{Lat: 8.2333, Lng: -81.7500}, // Tole
		{Lat: 8.1000, Lng: -80.9667}, // Santiago
		{Lat: 8.4000, Lng: -80.4167}, // Penonome
		{Lat: 8.9824, Lng: -79.5199}, // Panama City Logistics Center
	}
	return FromWaypoints(waypoints, "Default Road Corridor: San Jose CR -> Panama City PA", 20)
}

