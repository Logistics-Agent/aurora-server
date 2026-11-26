package provider_test

import (
	"bytes"
	"context"
	"encoding/json"
	"io"
	"log/slog"
	"net/http"
	"strings"
	"testing"

	"aurora/ai-governance/internal/gateway/domain"
	"aurora/ai-governance/internal/gateway/infrastructure/provider"
)

type roundTripFunc func(*http.Request) (*http.Response, error)

func (f roundTripFunc) RoundTrip(req *http.Request) (*http.Response, error) {
	return f(req)
}

type staticCredPort struct{ secret string }

func (s staticCredPort) ResolveSecret(_ string) (string, error) { return s.secret, nil }
func (s staticCredPort) Evict(_ string)                         {}

func TestGeminiAndAzure_StructuredOutputAndErrorHandling(t *testing.T) {
	logger := slog.New(slog.NewTextHandler(io.Discard, nil))

	// 1. Test simulation JSON output for compliance.answer
	simGemini := provider.NewGeminiProviderClient(staticCredPort{secret: "test-key"}, "", logger)
	simRes, err := simGemini.Generate(context.Background(), &domain.ProviderSlot{SlotAlias: "g1", ModelName: "gemini-1.5-flash", SecretRef: "k"}, domain.AiGenerateRequest{
		CapabilityCode: "compliance.answer",
		Prompt:         "Answer as JSON",
	})
	if err != nil {
		t.Fatalf("unexpected sim error: %v", err)
	}
	var parsed map[string]any
	if err := json.Unmarshal([]byte(simRes.Content), &parsed); err != nil {
		t.Fatalf("expected valid JSON structured output, got err=%v content=%s", err, simRes.Content)
	}

	// 2. Test live HTTP Gemini sends responseMimeType=application/json even when MaxOutputTokens==0
	var capturedBody map[string]any
	var returnEmpty bool
	rt := roundTripFunc(func(r *http.Request) (*http.Response, error) {
		bodyStr := `{"candidates":[{"content":{"parts":[{"text":"{\"ok\":true}"}]}}],"usageMetadata":{"promptTokenCount":12,"candidatesTokenCount":4}}`
		if strings.Contains(r.URL.Path, "generateContent") {
			_ = json.NewDecoder(r.Body).Decode(&capturedBody)
			if returnEmpty {
				bodyStr = `{"candidates":[]}`
			}
		} else if strings.Contains(r.URL.Path, "embedContent") {
			bodyStr = `{"embedding":{"values":[]}}`
		}
		return &http.Response{
			StatusCode: http.StatusOK,
			Header:     make(http.Header),
			Body:       io.NopCloser(bytes.NewBufferString(bodyStr)),
		}, nil
	})

	liveGemini := provider.NewGeminiProviderClientWithHTTP(staticCredPort{secret: "live-real-key"}, &http.Client{Transport: rt}, "https://generativelanguage.googleapis.com", logger)
	res, err := liveGemini.Generate(context.Background(), &domain.ProviderSlot{SlotAlias: "g-live", ModelName: "gemini-1.5-flash", SecretRef: "k"}, domain.AiGenerateRequest{
		CapabilityCode:  "compliance.answer",
		Prompt:          "hello",
		MaxOutputTokens: 0,
	})
	if err != nil || res.Content != `{"ok":true}` {
		t.Fatalf("unexpected live generate result: err=%v res=%+v", err, res)
	}
	genCfg, ok := capturedBody["generationConfig"].(map[string]any)
	if !ok || genCfg["responseMimeType"] != "application/json" {
		t.Fatalf("expected generationConfig.responseMimeType=application/json when MaxOutputTokens=0, got %+v", capturedBody)
	}

	// 3. Empty candidates on HTTP 200 must fail
	returnEmpty = true
	if _, err := liveGemini.Generate(context.Background(), &domain.ProviderSlot{SlotAlias: "g-live", ModelName: "gemini-1.5-flash", SecretRef: "k"}, domain.AiGenerateRequest{
		CapabilityCode: "devops.rca",
		Prompt:         "hello",
	}); err == nil {
		t.Fatalf("expected empty candidates response to return error")
	}

	// 4. Empty embedding vector on HTTP 200 must fail
	if _, err := liveGemini.Embed(context.Background(), &domain.ProviderSlot{SlotAlias: "g-live", ModelName: "text-embedding-004", SecretRef: "k"}, domain.AiEmbeddingRequest{
		CapabilityCode: "compliance.embed",
		Content:        "hello",
	}); err == nil {
		t.Fatalf("expected empty embedding vector response to return error")
	}
}
