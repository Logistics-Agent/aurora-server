package rules_test

import (
	"context"
	"testing"

	"aurora/route-planning-agent/internal/domain/entities"
	"aurora/route-planning-agent/internal/domain/enums"
	domainrules "aurora/route-planning-agent/internal/domain/rules"
	"aurora/route-planning-agent/internal/infrastructure/rules"

	"github.com/google/uuid"
)

type mockRuleConfigService struct {
	configs map[string]*domainrules.TenantRuleThresholds
}

func (m *mockRuleConfigService) GetThresholds(ctx context.Context, tenantID uuid.UUID, ruleName string) (*domainrules.TenantRuleThresholds, error) {
	if cfg, ok := m.configs[ruleName]; ok {
		return cfg, nil
	}
	return &domainrules.TenantRuleThresholds{IsEnabled: true, Values: map[string]float64{}}, nil
}

func TestHeavyWeightRule(t *testing.T) {
	rule := rules.NewHeavyWeightRule()
	tenantID := uuid.New()
	mockSvc := &mockRuleConfigService{configs: map[string]*domainrules.TenantRuleThresholds{}}

	// Case 1: Normal weight (2000kg <= 5000kg)
	routeNormal := &entities.Route{MaxWeightKg: 2000}
	res, err := rule.Evaluate(context.Background(), &domainrules.RouteRuleContext{
		Route:             routeNormal,
		TenantID:          tenantID,
		RuleConfigService: mockSvc,
	})
	if err != nil || !res.Passed || res.RiskLevel != enums.RouteRiskLevelLow {
		t.Errorf("expected passed Low risk, got: %+v", res)
	}

	// Case 2: Over high risk (6000kg > 5000kg, > 3000kg)
	routeHeavy := &entities.Route{MaxWeightKg: 6000}
	res, err = rule.Evaluate(context.Background(), &domainrules.RouteRuleContext{
		Route:             routeHeavy,
		TenantID:          tenantID,
		RuleConfigService: mockSvc,
	})
	if err != nil || res.Passed || res.RiskLevel != enums.RouteRiskLevelHigh || !res.RequiresApproval {
		t.Errorf("expected failed High risk with approval required, got: %+v", res)
	}

	// Case 3: Rule disabled
	mockSvc.configs[rules.HeavyWeightRuleName] = &domainrules.TenantRuleThresholds{IsEnabled: false}
	res, err = rule.Evaluate(context.Background(), &domainrules.RouteRuleContext{
		Route:             routeHeavy,
		TenantID:          tenantID,
		RuleConfigService: mockSvc,
	})
	if err != nil || !res.Passed {
		t.Errorf("expected passed when disabled, got: %+v", res)
	}
}

func TestLargeVolumeRule(t *testing.T) {
	rule := rules.NewLargeVolumeRule()
	tenantID := uuid.New()
	mockSvc := &mockRuleConfigService{configs: map[string]*domainrules.TenantRuleThresholds{}}

	routeNormal := &entities.Route{MaxVolumeM3: 30}
	res, _ := rule.Evaluate(context.Background(), &domainrules.RouteRuleContext{
		Route:             routeNormal,
		TenantID:          tenantID,
		RuleConfigService: mockSvc,
	})
	if !res.Passed || res.RiskLevel != enums.RouteRiskLevelLow {
		t.Errorf("expected passed Low risk, got: %+v", res)
	}

	routeLarge := &entities.Route{MaxVolumeM3: 60}
	res, _ = rule.Evaluate(context.Background(), &domainrules.RouteRuleContext{
		Route:             routeLarge,
		TenantID:          tenantID,
		RuleConfigService: mockSvc,
	})
	if res.Passed || res.RiskLevel != enums.RouteRiskLevelHigh || !res.RequiresApproval {
		t.Errorf("expected failed High risk with approval, got: %+v", res)
	}
}

func TestRouteStopCountRule(t *testing.T) {
	rule := rules.NewRouteStopCountRule()
	tenantID := uuid.New()
	mockSvc := &mockRuleConfigService{configs: map[string]*domainrules.TenantRuleThresholds{}}

	// 10 stops -> Low (<= 15)
	stops10 := make([]entities.RouteStop, 10)
	route10 := &entities.Route{Stops: stops10}
	res, _ := rule.Evaluate(context.Background(), &domainrules.RouteRuleContext{
		Route:             route10,
		TenantID:          tenantID,
		RuleConfigService: mockSvc,
	})
	if !res.Passed || res.RiskLevel != enums.RouteRiskLevelLow {
		t.Errorf("expected Low risk for 10 stops, got: %+v", res)
	}

	// 18 stops -> Medium (> 15, <= 20)
	stops18 := make([]entities.RouteStop, 18)
	route18 := &entities.Route{Stops: stops18}
	res, _ = rule.Evaluate(context.Background(), &domainrules.RouteRuleContext{
		Route:             route18,
		TenantID:          tenantID,
		RuleConfigService: mockSvc,
	})
	if res.Passed || res.RiskLevel != enums.RouteRiskLevelMedium {
		t.Errorf("expected Medium risk for 18 stops, got: %+v", res)
	}

	// 25 stops -> High (> 20)
	stops25 := make([]entities.RouteStop, 25)
	route25 := &entities.Route{Stops: stops25}
	res, _ = rule.Evaluate(context.Background(), &domainrules.RouteRuleContext{
		Route:             route25,
		TenantID:          tenantID,
		RuleConfigService: mockSvc,
	})
	if res.Passed || res.RiskLevel != enums.RouteRiskLevelHigh {
		t.Errorf("expected High risk for 25 stops, got: %+v", res)
	}
}

func TestOnDemandTypeRule(t *testing.T) {
	rule := rules.NewOnDemandTypeRule()
	tenantID := uuid.New()
	mockSvc := &mockRuleConfigService{configs: map[string]*domainrules.TenantRuleThresholds{}}

	routeFixed := &entities.Route{Type: enums.RouteTypeFixed}
	res, _ := rule.Evaluate(context.Background(), &domainrules.RouteRuleContext{
		Route:             routeFixed,
		TenantID:          tenantID,
		RuleConfigService: mockSvc,
	})
	if !res.Passed {
		t.Errorf("expected passed for Fixed route, got: %+v", res)
	}

	routeOnDemand := &entities.Route{Type: enums.RouteTypeOnDemand}
	res, _ = rule.Evaluate(context.Background(), &domainrules.RouteRuleContext{
		Route:             routeOnDemand,
		TenantID:          tenantID,
		RuleConfigService: mockSvc,
	})
	if res.Passed || res.RiskLevel != enums.RouteRiskLevelMedium {
		t.Errorf("expected Medium risk for OnDemand route, got: %+v", res)
	}
}

func TestMinimumStopsRule(t *testing.T) {
	rule := rules.NewMinimumStopsRule()
	tenantID := uuid.New()
	mockSvc := &mockRuleConfigService{configs: map[string]*domainrules.TenantRuleThresholds{}}

	route1Stop := &entities.Route{Stops: make([]entities.RouteStop, 1)}
	res, _ := rule.Evaluate(context.Background(), &domainrules.RouteRuleContext{
		Route:             route1Stop,
		TenantID:          tenantID,
		RuleConfigService: mockSvc,
	})
	if res.Passed {
		t.Errorf("expected failed for 1 stop, got: %+v", res)
	}

	route2Stops := &entities.Route{Stops: make([]entities.RouteStop, 2)}
	res, _ = rule.Evaluate(context.Background(), &domainrules.RouteRuleContext{
		Route:             route2Stops,
		TenantID:          tenantID,
		RuleConfigService: mockSvc,
	})
	if !res.Passed {
		t.Errorf("expected passed for 2 stops, got: %+v", res)
	}
}

func TestMultiHubRule(t *testing.T) {
	rule := rules.NewMultiHubRule()
	tenantID := uuid.New()
	mockSvc := &mockRuleConfigService{configs: map[string]*domainrules.TenantRuleThresholds{}}

	// 2 Hubs -> <= 2 (OK)
	route2Hubs := &entities.Route{Stops: []entities.RouteStop{
		{StopType: enums.StopTypeHub},
		{StopType: enums.StopTypePort},
		{StopType: enums.StopTypeDelivery},
	}}
	res, _ := rule.Evaluate(context.Background(), &domainrules.RouteRuleContext{
		Route:             route2Hubs,
		TenantID:          tenantID,
		RuleConfigService: mockSvc,
	})
	if !res.Passed {
		t.Errorf("expected passed for 2 hubs, got: %+v", res)
	}

	// 3 Hubs -> > 2 (High Risk)
	route3Hubs := &entities.Route{Stops: []entities.RouteStop{
		{StopType: enums.StopTypeHub},
		{StopType: enums.StopTypePort},
		{StopType: enums.StopTypeCustoms},
	}}
	res, _ = rule.Evaluate(context.Background(), &domainrules.RouteRuleContext{
		Route:             route3Hubs,
		TenantID:          tenantID,
		RuleConfigService: mockSvc,
	})
	if res.Passed || res.RiskLevel != enums.RouteRiskLevelHigh || !res.RequiresApproval {
		t.Errorf("expected High risk with approval for 3 hubs, got: %+v", res)
	}
}

func TestRouteRuleEngineEvaluateAll(t *testing.T) {
	engine := rules.NewDefaultRouteRuleEngine()
	tenantID := uuid.New()
	mockSvc := &mockRuleConfigService{configs: map[string]*domainrules.TenantRuleThresholds{}}

	route := &entities.Route{
		MaxWeightKg:              6000, // Trigger HeavyWeightRule
		MaxVolumeM3:              10,
		EstimatedDurationMinutes: 120,
		Type:                     enums.RouteTypeFixed,
		Stops: []entities.RouteStop{
			{StopType: enums.StopTypePickup},
			{StopType: enums.StopTypeDelivery},
		},
	}

	results, err := engine.EvaluateAll(context.Background(), &domainrules.RouteRuleContext{
		Route:             route,
		TenantID:          tenantID,
		RuleConfigService: mockSvc,
	})

	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}

	if len(results) != 7 {
		t.Fatalf("expected 7 rule results, got %d", len(results))
	}

	// Kiểm tra xem HeavyWeightRule có bị kích hoạt không
	foundHeavyWeight := false
	for _, r := range results {
		if r.RuleName == rules.HeavyWeightRuleName {
			foundHeavyWeight = true
			if r.Passed {
				t.Errorf("expected HeavyWeightRule to fail")
			}
		}
	}
	if !foundHeavyWeight {
		t.Errorf("HeavyWeightRule result not found")
	}
}

