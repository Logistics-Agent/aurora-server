package vroom

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"log/slog"
	"math"
	"net/http"
	"sort"
	"strings"
	"time"

	"aurora/route-planning-agent/internal/domain"
	"aurora/route-planning-agent/internal/domain/entities"
)

type OptimizationService interface {
	Optimize(ctx context.Context, route *entities.Route) (*OptimizationResult, error)
}

type Client struct {
	baseURL    string
	httpClient *http.Client
	logger     *slog.Logger
}

func NewClient(baseURL string, timeout time.Duration, logger *slog.Logger) *Client {
	if logger == nil {
		logger = slog.Default()
	}
	if timeout <= 0 {
		timeout = 30 * time.Second
	}
	baseURL = strings.TrimRight(baseURL, "/")

	return &Client{
		baseURL: baseURL,
		httpClient: &http.Client{
			Timeout: timeout,
			Transport: &http.Transport{
				MaxIdleConns:        100,
				MaxIdleConnsPerHost: 20,
				IdleConnTimeout:     90 * time.Second,
			},
		},
		logger: logger,
	}
}

// NewClientWithHTTPClient cho phép cấu hình custom httpClient (hữu ích cho unit test mock RoundTripper)
func NewClientWithHTTPClient(baseURL string, httpClient *http.Client, logger *slog.Logger) *Client {
	if logger == nil {
		logger = slog.Default()
	}
	if httpClient == nil {
		httpClient = &http.Client{Timeout: 30 * time.Second}
	}
	baseURL = strings.TrimRight(baseURL, "/")

	return &Client{
		baseURL:    baseURL,
		httpClient: httpClient,
		logger:     logger,
	}
}

func (c *Client) Optimize(ctx context.Context, route *entities.Route) (*OptimizationResult, error) {
	if route == nil || len(route.Stops) < 2 {
		return nil, domain.ErrRouteHasTooFewStops
	}

	// Sắp xếp các điểm dừng theo Sequence tăng dần
	orderedStops := make([]entities.RouteStop, len(route.Stops))
	copy(orderedStops, route.Stops)
	sort.Slice(orderedStops, func(i, j int) bool {
		return orderedStops[i].Sequence < orderedStops[j].Sequence
	})

	startStop := orderedStops[0]
	jobStops := orderedStops[1:]

	jobIDToStop := make(map[int]entities.RouteStop, len(jobStops))
	jobs := make([]vroomJob, 0, len(jobStops))

	for i, stop := range jobStops {
		jobID := i + 1
		jobIDToStop[jobID] = stop
		jobs = append(jobs, vroomJob{
			ID:       jobID,
			Location: []float64{stop.Longitude, stop.Latitude}, // VROOM: [lng, lat]
			Service:  stop.ServiceDurationMinutes * 60,         // đổi sang giây
		})
	}

	reqBody := vroomRequest{
		Vehicles: []vroomVehicle{
			{
				ID:      1,
				Profile: "car",
				Start:   []float64{startStop.Longitude, startStop.Latitude},
			},
		},
		Jobs:    jobs,
		Options: vroomOptions{G: true},
	}

	jsonBytes, err := json.Marshal(reqBody)
	if err != nil {
		return nil, fmt.Errorf("lỗi encode request VROOM: %w", err)
	}

	url := fmt.Sprintf("%s/", c.baseURL)
	httpReq, err := http.NewRequestWithContext(ctx, http.MethodPost, url, bytes.NewBuffer(jsonBytes))
	if err != nil {
		return nil, fmt.Errorf("lỗi tạo HTTP request: %w", err)
	}
	httpReq.Header.Set("Content-Type", "application/json")

	resp, err := c.httpClient.Do(httpReq)
	if err != nil {
		c.logger.Error("Gọi VROOM solver thất bại",
			slog.String("route_id", route.ID.String()),
			slog.Any("error", err),
		)
		return nil, fmt.Errorf("Optimization service (VROOM/OSRM) hiện không khả dụng: %w", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode < 200 || resp.StatusCode >= 300 {
		return nil, fmt.Errorf("VROOM solver trả về mã HTTP lỗi: %d", resp.StatusCode)
	}

	var vroomResp vroomResponse
	if err := json.NewDecoder(resp.Body).Decode(&vroomResp); err != nil {
		return nil, fmt.Errorf("lỗi decode response từ VROOM: %w", err)
	}

	if vroomResp.Code != 0 {
		errMsg := "không rõ nguyên nhân"
		if vroomResp.Error != nil {
			errMsg = *vroomResp.Error
		}
		return nil, fmt.Errorf("VROOM solver trả về lỗi (code=%d): %s", vroomResp.Code, errMsg)
	}

	if len(vroomResp.Unassigned) > 0 {
		unassignedNames := make([]string, 0, len(vroomResp.Unassigned))
		for _, u := range vroomResp.Unassigned {
			if s, ok := jobIDToStop[u.ID]; ok {
				unassignedNames = append(unassignedNames, s.LocationName)
			} else {
				unassignedNames = append(unassignedNames, fmt.Sprintf("ID:%d", u.ID))
			}
		}
		return nil, fmt.Errorf("không thể xếp lịch cho các điểm dừng: %s (ngoài vùng bản đồ?)", strings.Join(unassignedNames, ", "))
	}

	if len(vroomResp.Routes) == 0 {
		return nil, fmt.Errorf("VROOM không trả về tuyến đường nào")
	}

	vroomRoute := vroomResp.Routes[0]

	// Điểm xuất phát giữ sequence = 1, arrival = 0
	optimized := []OptimizedStop{
		{
			StopID:                  startStop.ID,
			Sequence:                1,
			EstimatedArrivalMinutes: 0,
		},
	}

	seq := int32(2)
	var totalDurationSeconds int64

	for _, step := range vroomRoute.Steps {
		if step.Type == "job" && step.ID != nil {
			if stop, ok := jobIDToStop[*step.ID]; ok {
				arrivalMins := int32(math.Round(float64(step.Arrival) / 60.0))
				optimized = append(optimized, OptimizedStop{
					StopID:                  stop.ID,
					Sequence:                seq,
					EstimatedArrivalMinutes: arrivalMins,
				})
				seq++
			}
		}

		stepDuration := step.Arrival + step.Service
		if stepDuration > totalDurationSeconds {
			totalDurationSeconds = stepDuration
		}
	}

	distanceKm := math.Round((float64(vroomRoute.Distance)/1000.0)*1000) / 1000
	durationMins := int32(math.Round(float64(totalDurationSeconds) / 60.0))

	return &OptimizationResult{
		Stops:                optimized,
		TotalDistanceKm:      distanceKm,
		TotalDurationMinutes: durationMins,
	}, nil
}

// Interface check
var _ OptimizationService = (*Client)(nil)
