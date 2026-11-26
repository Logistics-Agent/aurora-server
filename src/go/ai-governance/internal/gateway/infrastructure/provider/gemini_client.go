package provider

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"log/slog"
	"math"
	"net/http"
	"strings"
	"time"

	"aurora/ai-governance/internal/gateway/domain"
	"aurora/ai-governance/internal/gateway/infrastructure/credential"
)

const defaultGeminiBaseURL = "https://generativelanguage.googleapis.com"

type GeminiProviderClient struct {
	credentialPort credential.CredentialPort
	httpClient     *http.Client
	baseURL        string
	logger         *slog.Logger
}

func NewGeminiProviderClient(credentialPort credential.CredentialPort, baseURL string, logger *slog.Logger) *GeminiProviderClient {
	if baseURL == "" {
		baseURL = defaultGeminiBaseURL
	}
	if logger == nil {
		logger = slog.Default()
	}
	return &GeminiProviderClient{
		credentialPort: credentialPort,
		httpClient:     &http.Client{Timeout: 30 * time.Second},
		baseURL:        strings.TrimRight(baseURL, "/"),
		logger:         logger,
	}
}

func (c *GeminiProviderClient) Generate(ctx context.Context, slot *domain.ProviderSlot, request domain.AiGenerateRequest) (domain.AiGenerateResult, error) {
	apiKey, err := c.credentialPort.ResolveSecret(slot.SecretRef)
	if err != nil {
		return domain.AiGenerateResult{}, err
	}
	c.logger.Debug("Executing Gemini generate", slog.String("slot", slot.SlotAlias), slog.String("model", slot.ModelName))

	if IsTestKey(apiKey) {
		return c.simulateGenerate(slot, request), nil
	}

	model := slot.ModelName
	if strings.TrimSpace(model) == "" {
		model = "gemini-1.5-flash"
	}

	parts := make([]map[string]any, 0)
	if strings.TrimSpace(request.Prompt) != "" {
		parts = append(parts, map[string]any{"text": request.Prompt})
	}
	for _, part := range request.InputParts {
		if part.Type == domain.PartTypeText && part.Text != "" {
			parts = append(parts, map[string]any{"text": part.Text})
		} else if part.Type == domain.PartTypeFile && part.File != nil {
			parts = append(parts, map[string]any{
				"text": fmt.Sprintf("[Document Reference: %s, mime: %s, name: %s]",
					part.File.StorageReference, part.File.MimeType, part.File.FileName),
			})
		}
	}

	body := map[string]any{
		"contents": []map[string]any{
			{"parts": parts},
		},
	}
	if request.MaxOutputTokens > 0 {
		genCfg := map[string]any{
			"maxOutputTokens": request.MaxOutputTokens,
			"temperature":     0.1,
		}
		if RequiresJSONObject(request) {
			genCfg["responseMimeType"] = "application/json"
		}
		body["generationConfig"] = genCfg
	}

	rawBody, err := json.Marshal(body)
	if err != nil {
		return domain.AiGenerateResult{}, err
	}

	endpoint := fmt.Sprintf("%s/v1beta/models/%s:generateContent", c.baseURL, model)
	req, err := http.NewRequestWithContext(ctx, http.MethodPost, endpoint, bytes.NewReader(rawBody))
	if err != nil {
		return domain.AiGenerateResult{}, err
	}
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("x-goog-api-key", apiKey)

	resp, err := c.httpClient.Do(req)
	if err != nil {
		return domain.AiGenerateResult{}, fmt.Errorf("gemini provider error: %w", err)
	}
	defer resp.Body.Close()

	respBytes, err := io.ReadAll(resp.Body)
	if err != nil {
		return domain.AiGenerateResult{}, fmt.Errorf("gemini provider read error: %w", err)
	}
	if resp.StatusCode != http.StatusOK {
		return domain.AiGenerateResult{}, fmt.Errorf("gemini generate HTTP %d: %s", resp.StatusCode, string(respBytes))
	}

	var parsed struct {
		Candidates []struct {
			Content struct {
				Parts []struct {
					Text string `json:"text"`
				} `json:"parts"`
			} `json:"content"`
		} `json:"candidates"`
		UsageMetadata struct {
			PromptTokenCount     int64 `json:"promptTokenCount"`
			CandidatesTokenCount int64 `json:"candidatesTokenCount"`
		} `json:"usageMetadata"`
	}
	if err := json.Unmarshal(respBytes, &parsed); err != nil {
		return domain.AiGenerateResult{}, fmt.Errorf("gemini provider parse error: %w", err)
	}

	var text string
	if len(parsed.Candidates) > 0 && len(parsed.Candidates[0].Content.Parts) > 0 {
		text = parsed.Candidates[0].Content.Parts[0].Text
	}
	inputTokens := parsed.UsageMetadata.PromptTokenCount
	if inputTokens == 0 {
		inputTokens = request.EstimatedInputTokens
	}
	outputTokens := parsed.UsageMetadata.CandidatesTokenCount
	if outputTokens == 0 {
		outputTokens = 50
	}

	return domain.AiGenerateResult{
		Content:      text,
		InputTokens:  inputTokens,
		OutputTokens: outputTokens,
		Model:        model,
		Provider:     "GEMINI",
	}, nil
}

func (c *GeminiProviderClient) Embed(ctx context.Context, slot *domain.ProviderSlot, request domain.AiEmbeddingRequest) (domain.AiEmbeddingResult, error) {
	apiKey, err := c.credentialPort.ResolveSecret(slot.SecretRef)
	if err != nil {
		return domain.AiEmbeddingResult{}, err
	}
	c.logger.Debug("Executing Gemini embed", slog.String("slot", slot.SlotAlias), slog.String("model", slot.ModelName))

	targetDim := 768
	if request.Dimensions != nil && *request.Dimensions > 0 {
		targetDim = int(*request.Dimensions)
	}

	if IsTestKey(apiKey) {
		return c.simulateEmbed(slot, request, targetDim), nil
	}

	model := slot.ModelName
	if strings.TrimSpace(model) == "" {
		model = "text-embedding-004"
	}

	body := map[string]any{
		"model": "models/" + model,
		"content": map[string]any{
			"parts": []map[string]any{{"text": request.Content}},
		},
		"outputDimensionality": targetDim,
	}
	rawBody, err := json.Marshal(body)
	if err != nil {
		return domain.AiEmbeddingResult{}, err
	}

	endpoint := fmt.Sprintf("%s/v1beta/models/%s:embedContent", c.baseURL, model)
	req, err := http.NewRequestWithContext(ctx, http.MethodPost, endpoint, bytes.NewReader(rawBody))
	if err != nil {
		return domain.AiEmbeddingResult{}, err
	}
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("x-goog-api-key", apiKey)

	resp, err := c.httpClient.Do(req)
	if err != nil {
		return domain.AiEmbeddingResult{}, fmt.Errorf("gemini embed provider error: %w", err)
	}
	defer resp.Body.Close()

	respBytes, err := io.ReadAll(resp.Body)
	if err != nil {
		return domain.AiEmbeddingResult{}, fmt.Errorf("gemini embed read error: %w", err)
	}
	if resp.StatusCode != http.StatusOK {
		return domain.AiEmbeddingResult{}, fmt.Errorf("gemini embed HTTP %d: %s", resp.StatusCode, string(respBytes))
	}

	var parsed struct {
		Embedding struct {
			Values []float32 `json:"values"`
		} `json:"embedding"`
	}
	if err := json.Unmarshal(respBytes, &parsed); err != nil {
		return domain.AiEmbeddingResult{}, fmt.Errorf("gemini embed parse error: %w", err)
	}

	vec := parsed.Embedding.Values
	NormalizeVector(vec)

	inputTokens := request.EstimatedInputTokens
	if inputTokens <= 0 {
		inputTokens = 50
	}
	return domain.AiEmbeddingResult{
		Vector:      vec,
		InputTokens: inputTokens,
		Model:       model,
		Provider:    "GEMINI",
	}, nil
}

func (c *GeminiProviderClient) simulateGenerate(slot *domain.ProviderSlot, request domain.AiGenerateRequest) domain.AiGenerateResult {
	estInput := request.EstimatedInputTokens
	if estInput <= 0 {
		estInput = 100
	}
	if RequiresJSONObject(request) {
		return domain.AiGenerateResult{
			Content:      DeterministicJSONResponse(),
			InputTokens:  estInput,
			OutputTokens: 50,
			Model:        slot.ModelName,
			Provider:     "GEMINI",
		}
	}
	return domain.AiGenerateResult{
		Content: fmt.Sprintf("[Gemini Response via slot '%s' (model: %s)]: Processed prompt for capability '%s'.",
			slot.SlotAlias, slot.ModelName, request.CapabilityCode),
		InputTokens:  estInput,
		OutputTokens: 50,
		Model:        slot.ModelName,
		Provider:     "GEMINI",
	}
}

func (c *GeminiProviderClient) simulateEmbed(slot *domain.ProviderSlot, request domain.AiEmbeddingRequest, dim int) domain.AiEmbeddingResult {
	vec := make([]float32, dim)
	seed := DeterministicSeed(request.Content)
	for i := 0; i < dim; i++ {
		vec[i] = float32(math.Sin(float64(seed+i+1) * 0.05))
	}
	NormalizeVector(vec)

	estInput := request.EstimatedInputTokens
	if estInput <= 0 {
		estInput = 50
	}
	return domain.AiEmbeddingResult{
		Vector:      vec,
		InputTokens: estInput,
		Model:       slot.ModelName,
		Provider:    "GEMINI",
	}
}
