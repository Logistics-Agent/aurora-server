package vroom

import "github.com/google/uuid"

// OptimizedStop kết quả thứ tự điểm dừng sau khi VROOM giải bài toán VRP
type OptimizedStop struct {
	StopID                  uuid.UUID `json:"stop_id"`
	Sequence                int32     `json:"sequence"`
	EstimatedArrivalMinutes int32     `json:"estimated_arrival_minutes"`
}

// OptimizationResult kết quả tổng thể giải thuật tối ưu hóa
type OptimizationResult struct {
	Stops                []OptimizedStop `json:"stops"`
	TotalDistanceKm      float64         `json:"total_distance_km"`
	TotalDurationMinutes int32           `json:"total_duration_minutes"`
}

// vroomRequest cấu trúc gửi tới VROOM HTTP API
type vroomRequest struct {
	Vehicles []vroomVehicle `json:"vehicles"`
	Jobs     []vroomJob     `json:"jobs"`
	Options  vroomOptions   `json:"options"`
}

type vroomVehicle struct {
	ID      int       `json:"id"`
	Profile string    `json:"profile"`
	Start   []float64 `json:"start"` // [longitude, latitude]
}

type vroomJob struct {
	ID       int       `json:"id"`
	Location []float64 `json:"location"` // [longitude, latitude]
	Service  int32     `json:"service"`  // giây
}

type vroomOptions struct {
	G bool `json:"g"` // yêu cầu trả về geometry và khoảng cách chi tiết từ OSRM
}

// vroomResponse cấu trúc nhận về từ VROOM
type vroomResponse struct {
	Code       int               `json:"code"`
	Error      *string           `json:"error,omitempty"`
	Unassigned []vroomUnassigned `json:"unassigned,omitempty"`
	Routes     []vroomRoute      `json:"routes,omitempty"`
}

type vroomUnassigned struct {
	ID int `json:"id"`
}

type vroomRoute struct {
	Distance int64       `json:"distance"` // mét
	Duration int64       `json:"duration"` // giây di chuyển
	Steps    []vroomStep `json:"steps"`
}

type vroomStep struct {
	Type    string `json:"type"` // "start" | "job" | "end"
	ID      *int   `json:"id,omitempty"`
	Arrival int64  `json:"arrival"` // giây kể từ thời điểm xuất phát
	Service int64  `json:"service"` // giây phục vụ tại trạm
}

