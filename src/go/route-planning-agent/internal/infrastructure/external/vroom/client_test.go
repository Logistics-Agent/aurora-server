package vroom_test

import (
	"bytes"
	"context"
	"encoding/json"
	"io"
	"net/http"
	"testing"

	"aurora/route-planning-agent/internal/domain"
	"aurora/route-planning-agent/internal/domain/entities"
	"aurora/route-planning-agent/internal/infrastructure/external/vroom"

	"github.com/google/uuid"
)

type mockRoundTripper func(req *http.Request) (*http.Response, error)

func (m mockRoundTripper) RoundTrip(req *http.Request) (*http.Response, error) {
	return m(req)
}

func TestVroomClientOptimizeSuccess(t *testing.T) {
	stop1ID := uuid.New()
	stop2ID := uuid.New()
	stop3ID := uuid.New()

	// Giả lập VROOM server response qua in-memory mock RoundTripper (không cần mở cổng TCP socket)
	mockRT := mockRoundTripper(func(r *http.Request) (*http.Response, error) {
		if r.Method != http.MethodPost {
			t.Errorf("expected POST, got %s", r.Method)
		}

		job1ID := 1
		job2ID := 2

		resp := map[string]any{
			"code": 0,
			"routes": []map[string]any{
				{
					"distance": 15420, // 15.42 km
					"duration": 1800,  // 30 mins
					"steps": []map[string]any{
						{"type": "start", "arrival": 0, "service": 0},
						{"type": "job", "id": job2ID, "arrival": 600, "service": 300},  // stop 3 đến trước
						{"type": "job", "id": job1ID, "arrival": 1500, "service": 300}, // stop 2 đến sau
						{"type": "end", "arrival": 2000, "service": 0},
					},
				},
			},
		}

		bodyBytes, _ := json.Marshal(resp)
		return &http.Response{
			StatusCode: http.StatusOK,
			Header:     http.Header{"Content-Type": []string{"application/json"}},
			Body:       io.NopCloser(bytes.NewReader(bodyBytes)),
		}, nil
	})

	httpClient := &http.Client{Transport: mockRT}
	client := vroom.NewClientWithHTTPClient("http://vroom-mock", httpClient, nil)

	route := &entities.Route{
		Stops: []entities.RouteStop{
			{ID: stop1ID, Sequence: 1, Longitude: 106.6, Latitude: 10.7, LocationName: "Start"},
			{ID: stop2ID, Sequence: 2, Longitude: 106.7, Latitude: 10.8, LocationName: "Delivery A", ServiceDurationMinutes: 5},
			{ID: stop3ID, Sequence: 3, Longitude: 106.8, Latitude: 10.9, LocationName: "Delivery B", ServiceDurationMinutes: 5},
		},
	}
	route.ID = uuid.New()

	result, err := client.Optimize(context.Background(), route)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	if result.TotalDistanceKm != 15.42 {
		t.Errorf("expected 15.42 km, got %f", result.TotalDistanceKm)
	}

	if len(result.Stops) != 3 {
		t.Fatalf("expected 3 stops, got %d", len(result.Stops))
	}

	// Kiểm tra stop 1 là xuất phát (Sequence = 1)
	if result.Stops[0].StopID != stop1ID || result.Stops[0].Sequence != 1 {
		t.Errorf("first stop should be startStop with sequence 1")
	}

	// Kiểm tra stop 3 được tối ưu lên trước (Sequence = 2) vì job 2 được ghé trước
	if result.Stops[1].StopID != stop3ID || result.Stops[1].Sequence != 2 {
		t.Errorf("second stop should be stop3 (job2)")
	}

	// Kiểm tra stop 2 (Sequence = 3)
	if result.Stops[2].StopID != stop2ID || result.Stops[2].Sequence != 3 {
		t.Errorf("third stop should be stop2 (job1)")
	}
}

func TestVroomClientTooFewStops(t *testing.T) {
	client := vroom.NewClient("http://localhost:3000", 0, nil)

	route1Stop := &entities.Route{
		Stops: []entities.RouteStop{
			{ID: uuid.New(), Sequence: 1},
		},
	}

	_, err := client.Optimize(context.Background(), route1Stop)
	if err != domain.ErrRouteHasTooFewStops {
		t.Errorf("expected ErrRouteHasTooFewStops, got: %v", err)
	}
}
