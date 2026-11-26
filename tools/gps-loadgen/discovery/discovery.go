package discovery

import (
	"context"
	"encoding/json"
	"fmt"
	"os"
	"strings"
	"time"

	"aurora/gps-loadgen/config"
	"aurora/gps-loadgen/model"
	"aurora/gps-loadgen/proto/shipmentpb"

	"google.golang.org/grpc/metadata"
)

type ShipmentCandidate struct {
	ShipmentId   string
	VehicleId    string
	RouteId      string
	CustomerName string
	Status       string
	Locations    []model.Coordinate
}

func DiscoverVehicles(
	ctx context.Context,
	cfg *config.SimulationConfig,
	shipmentClient shipmentpb.ShipmentWorkflowServiceClient,
) ([]*model.VehicleSimulation, error) {
	candidates := make([]ShipmentCandidate, 0)
	defaultTrajectory := model.DefaultCorridorTrajectory()

	md := metadata.Pairs(
		"x-tenant-id", cfg.TenantId,
		"x-user-id", cfg.UserId,
	)
	ctx = metadata.NewOutgoingContext(ctx, md)

	if cfg.Mode == config.ModeSingle {
		c := ShipmentCandidate{
			ShipmentId:   cfg.SingleShipmentId,
			VehicleId:    cfg.SingleShipmentId,
			CustomerName: "Single Shipment",
		}
		if shipmentClient != nil {
			reqCtx, cancel := context.WithTimeout(ctx, 5*time.Second)
			defer cancel()
			resp, err := shipmentClient.GetShipment(reqCtx, &shipmentpb.GetShipmentRequest{Id: cfg.SingleShipmentId})
			if err == nil && resp != nil {
				c.CustomerName = resp.CustomerName
				if resp.VehicleId != "" {
					c.VehicleId = resp.VehicleId
				}
				c.RouteId = resp.RouteId
			}
		}
		candidates = append(candidates, c)
	} else if cfg.ShipmentsFile != "" {
		loaded, err := loadFromFile(cfg.ShipmentsFile)
		if err != nil {
			return nil, err
		}
		candidates = append(candidates, loaded...)
	} else if shipmentClient != nil {
		reqCtx, cancel := context.WithTimeout(ctx, 5*time.Second)
		defer cancel()
		resp, err := shipmentClient.ListShipments(reqCtx, &shipmentpb.ListShipmentsRequest{Page: 1, Limit: 50})
		if err == nil && resp != nil {
			for _, s := range resp.Shipments {
				vId := s.VehicleId
				if vId == "" {
					if cfg.AutoAssign {
						vId = fmt.Sprintf("veh-auto-%s", s.Id[:min(8, len(s.Id))])
					} else {
						continue
					}
				}
				candidates = append(candidates, ShipmentCandidate{
					ShipmentId:   s.Id,
					VehicleId:    vId,
					RouteId:      s.RouteId,
					CustomerName: s.CustomerName,
					Status:       s.Status,
				})
				if cfg.VehiclesLimit > 0 && len(candidates) >= cfg.VehiclesLimit {
					break
				}
			}
		}
	}

	// Scale up to VehiclesLimit if needed
	needed := cfg.VehiclesLimit
	if needed <= 0 {
		needed = 10
	}

	if len(candidates) == 0 {
		fmt.Printf("No shipments found via service. Generating %d synthetic vehicles along default corridor...\n", needed)
		for i := 1; i <= needed; i++ {
			candidates = append(candidates, ShipmentCandidate{
				ShipmentId:   fmt.Sprintf("00000000-0000-0000-0000-%012d", i),
				VehicleId:    fmt.Sprintf("sim-veh-%04d", i),
				CustomerName: fmt.Sprintf("Virtual Vehicle #%d", i),
				Status:       "IN_TRANSIT",
			})
		}
	} else if len(candidates) < needed && cfg.Mode == config.ModeFleet {
		fmt.Printf("Discovered %d shipment(s). Scaling up to %d virtual vehicles...\n", len(candidates), needed)
		baseList := make([]ShipmentCandidate, len(candidates))
		copy(baseList, candidates)
		idx := 0
		for len(candidates) < needed {
			template := baseList[idx%len(baseList)]
			idx++
			cloneNum := len(candidates) + 1
			candidates = append(candidates, ShipmentCandidate{
				ShipmentId:   template.ShipmentId,
				VehicleId:    fmt.Sprintf("sim-veh-%04d", cloneNum),
				RouteId:      template.RouteId,
				CustomerName: fmt.Sprintf("%s (Virtual #%d)", template.CustomerName, cloneNum),
				Status:       template.Status,
			})
		}
	}

	vehicles := make([]*model.VehicleSimulation, 0, len(candidates))
	for _, c := range candidates {
		v := &model.VehicleSimulation{
			ShipmentId:              c.ShipmentId,
			VehicleId:               c.VehicleId,
			DeviceId:                fmt.Sprintf("sim-dev-%s", c.VehicleId),
			RouteId:                 c.RouteId,
			Trajectory:              defaultTrajectory,
			CurrentPosition:         defaultTrajectory.Points[0],
			CurrentSpeed:            cfg.BaseSpeedKph,
			Heading:                 0,
			CurrentWaypointIndex:    0,
			ExternalReadingSequence: 0,
			Behavior:                cfg.Behavior,
		}
		vehicles = append(vehicles, v)
	}

	return vehicles, nil
}

func loadFromFile(path string) ([]ShipmentCandidate, error) {
	data, err := os.ReadFile(path)
	if err != nil {
		return nil, err
	}

	var strList []string
	if err := json.Unmarshal(data, &strList); err == nil && len(strList) > 0 {
		res := make([]ShipmentCandidate, 0, len(strList))
		for _, id := range strList {
			cleanId := strings.TrimSpace(id)
			res = append(res, ShipmentCandidate{
				ShipmentId:   cleanId,
				VehicleId:    fmt.Sprintf("sim-veh-%s", cleanId[:min(8, len(cleanId))]),
				CustomerName: "File Loaded",
			})
		}
		return res, nil
	}

	type fileItem struct {
		ShipmentId string `json:"shipmentId"`
		VehicleId  string `json:"vehicleId"`
	}
	var objList []fileItem
	if err := json.Unmarshal(data, &objList); err == nil && len(objList) > 0 {
		res := make([]ShipmentCandidate, 0, len(objList))
		for _, item := range objList {
			vId := item.VehicleId
			if vId == "" {
				vId = item.ShipmentId
			}
			res = append(res, ShipmentCandidate{
				ShipmentId:   item.ShipmentId,
				VehicleId:    vId,
				CustomerName: "File Loaded",
			})
		}
		return res, nil
	}

	return nil, fmt.Errorf("could not parse shipments file: %s", path)
}

func min(a, b int) int {
	if a < b {
		return a
	}
	return b
}

