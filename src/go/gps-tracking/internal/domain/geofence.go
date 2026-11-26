package domain

import (
	"math"
)

const EarthRadiusMeters = 6371000.0

func DistanceMeters(lat1, lon1, lat2, lon2 float64) float64 {
	latDelta := toRadians(lat2 - lat1)
	lonDelta := toRadians(lon2 - lon1)
	firstLat := toRadians(lat1)
	secondLat := toRadians(lat2)

	haversine := math.Pow(math.Sin(latDelta/2.0), 2) +
		math.Cos(firstLat)*math.Cos(secondLat)*math.Pow(math.Sin(lonDelta/2.0), 2)

	distance := EarthRadiusMeters * 2.0 * math.Atan2(math.Sqrt(haversine), math.Sqrt(1.0-haversine))
	return math.Round(distance*100.0) / 100.0
}

func toRadians(deg float64) float64 {
	return deg * math.Pi / 180.0
}

