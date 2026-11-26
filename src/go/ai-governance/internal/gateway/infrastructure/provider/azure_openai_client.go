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

const defaultAzureOpenAIEndpoint = "https://aurora-openai.openai.azure.com"

type AzureOpenAiProviderClient struct {
	credentialPort credential.CredentialPort
	httpClient     *http.Client
	endpoint       string
	logger         *slog.Logger
}

func NewAzureOpenAiProviderClient(credentialPort credential.CredentialPort, endpoint string, logger *slog.Logger) *AzureOpenAiProviderClient {
	if endpoint == "" {
		endpoint = defaultAzureOpenAIEndpoint
	}
	if logger == nil {
		logger = slog.Default()
	}
	return &AzureOpenAiProviderClient{
		credentialPort: credentialPort,
		httpClient:     &http.Client{Timeout: 30 * time.Second},
		endpoint:       strings.TrimRight(endpoint, "/"),
		logger:         logger,
	}
}

func (c *AzureOpenAiProviderClient) Generate(ctx context.Context, slot *domain.ProviderSlot, request domain.AiGenerateRequest) (domain.AiGenerateResult, error) {
	apiKey, err := c.credentialPort.ResolveSecret(slot.SecretRef)
	if err != nil {
		return domain.AiGenerateResult{}, err
	}
	c.logger.Debug("Executing Azure OpenAI generate", slog.String("slot", slot.SlotAlias), slog.String("model", slot.ModelName))

	if IsTestKey(apiKey) {
		return c.simulateGenerate(slot, request), nil
	}

	deployment := slot.ModelName
	if strings.TrimSpace(deployment) == "" {
		deployment = "gpt-4o-mini"
	}

	body := map[string]any{
		"messages": []map[string]string{
			{"role": "user", "content": request.Prompt},
		},
	}
	if request.MaxOutputTokens > 0 {
		body["max_tokens"] = request.MaxOutputTokens
	}
	if RequiresJSONObject(request) {
		body["response_format"] = map[string]string{"type": "json_object"}
	}

	rawBody, err := json.Marshal(body)
	if err != nil {
		return domain.AiGenerateResult{}, err
	}

	url := fmt.Sprintf("%s/openai/deployments/%s/chat/completions?api-version=2024-02-01", c.endpoint, deployment)
	req, err := http.NewRequestWithContext(ctx, http.MethodPost, url, bytes.NewReader(rawBody))
	if err != nil {
		return domain.AiGenerateResult{}, err
	}
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("api-key", apiKey)

	resp, err := c.httpClient.Do(req)
	if err != nil {
		return domain.AiGenerateResult{}, fmt.Errorf("azure openai provider error: %w", err)
	}
	defer resp.Body.Close()

	respBytes, err := io.ReadAll(resp.Body)
	if err != nil {
		return domain.AiGenerateResult{}, fmt.Errorf("azure openai read error: %w", err)
	}
	if resp.StatusCode != http.StatusOK {
		return domain.AiGenerateResult{}, fmt.Errorf("azure openai generate HTTP %d: %s", resp.StatusCode, string(respBytes))
	}

	var parsed struct {
		Choices []struct {
			Message struct {
				Content string `json:"content"`
			} `json:"message"`
		} `json:"choices"`
		Usage struct {
			PromptTokens     int64 `json:"prompt_tokens"`
			CompletionTokens int64 `json:"completion_tokens"`
		} `json:"usage"`
	}
	if err := json.Unmarshal(respBytes, &parsed); err != nil {
		return domain.AiGenerateResult{}, fmt.Errorf("azure openai parse error: %w", err)
	}

	var text string
	if len(parsed.Choices) > 0 {
		text = parsed.Choices[0].Message.Content
	}
	inputTokens := parsed.Usage.PromptTokens
	if inputTokens == 0 {
		inputTokens = request.EstimatedInputTokens
	}
	outputTokens := parsed.Usage.CompletionTokens
	if outputTokens == 0 {
		outputTokens = 50
	}

	return domain.AiGenerateResult{
		Content:      text,
		InputTokens:  inputTokens,
		OutputTokens: outputTokens,
		Model:        deployment,
		Provider:     "AZURE_OPENAI",
	}, nil
}

func (c *AzureOpenAiProviderClient) Embed(ctx context.Context, slot *domain.ProviderSlot, request domain.AiEmbeddingRequest) (domain.AiEmbeddingResult, error) {
	apiKey, err := c.credentialPort.ResolveSecret(slot.SecretRef)
	if err != nil {
		return domain.AiEmbeddingResult{}, err
	}
	c.logger.Debug("Executing Azure OpenAI embed", slog.String("slot", slot.SlotAlias), slog.String("model", slot.ModelName))

	targetDim := 768
	if request.Dimensions != nil && *request.Dimensions > 0 {
		targetDim = int(*request.Dimensions)
	}

	if IsTestKey(apiKey) {
		return c.simulateEmbed(slot, request, targetDim), nil
	}

	deployment := slot.ModelName
	if strings.TrimSpace(deployment) == "" {
		deployment = "text-embedding-3-small"
	}

	body := map[string]any{
		"input":      request.Content,
		"dimensions": targetDim,
	}
	rawBody, err := json.Marshal(body)
	if err != nil {
		return domain.AiEmbeddingResult{}, err
	}

	url := fmt.Sprintf("%s/openai/deployments/%s/embeddings?api-version=2024-02-01", c.endpoint, deployment)
	req, err := http.NewRequestWithContext(ctx, http.MethodPost, url, bytes.NewReader(rawBody))
	if err != nil {
		return domain.AiEmbeddingResult{}, err
	}
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("api-key", apiKey)

	resp, err := c.httpClient.Do(req)
	if err != nil {
		return domain.AiEmbeddingResult{}, fmt.Errorf("azure openai embed error: %w", err)
	}
	defer resp.Body.Close()

	respBytes, err := io.ReadAll(resp.Body)
	if err != nil {
		return domain.AiEmbeddingResult{}, fmt.Errorf("azure openai embed read error: %w", err)
	}
	if resp.StatusCode != http.StatusOK {
		return domain.AiEmbeddingResult{}, fmt.Errorf("azure openai embed HTTP %d: %s", resp.StatusCode, string(respBytes))
	}

	var parsed struct {
		Data []struct {
			Embedding []float32 `json:"embedding"`
		} `json:"data"`
		Usage struct {
			PromptTokens int64 `json:"prompt_tokens"`
		} `json:"usage"`
	}
	if err := json.Unmarshal(respBytes, &parsed); err != nil {
		return domain.AiEmbeddingResult{}, fmt.Errorf("azure openai embed parse error: %w", err)
	}

	var vec []float32
	if len(parsed.Data) > 0 {
		vec = parsed.Data[0].Embedding
	}
	NormalizeVector(vec)

	inputTokens := parsed.Usage.PromptTokens
	if inputTokens == 0 {
		if request.EstimatedInputTokens > 0 {
			inputTokens = request.EstimatedInputTokens
		} else {
			inputTokens = 50
		}
	}

	return domain.AiEmbeddingResult{
		Vector:      vec,
		InputTokens: inputTokens,
		Model:       deployment,
		Provider:    "AZURE_OPENAI",
	}, nil
}

func (c *AzureOpenAiProviderClient) simulateGenerate(slot *domain.ProviderSlot, request domain.AiGenerateRequest) domain.AiGenerateResult {
	estInput := request.EstimatedInputTokens
	if estInput <= 0 {
		estInput = 120
	}
	if RequiresJSONObject(request) {
		return domain.AiGenerateResult{
			Content:      DeterministicJSONResponse(),
			InputTokens:  estInput,
			OutputTokens: 60,
			Model:        slot.ModelName,
			Provider:     "AZURE_OPENAI",
		}
	}
	return domain.AiGenerateResult{
		Content: fmt.Sprintf("[Azure OpenAI Response via slot '%s' (deployment: %s)]: Processed prompt for capability '%s'.",
			slot.SlotAlias, slot.ModelName, request.CapabilityCode),
		InputTokens:  estInput,
		OutputTokens: 60,
		Model:        slot.ModelName,
		Provider:     "AZURE_OPENAI",
	}
}

func (c *AzureOpenAiProviderClient) simulateEmbed(slot *domain.ProviderSlot, request domain.AiEmbeddingRequest, dim int) domain.AiEmbeddingResult {
	vec := make([]float32, dim)
	seed := DeterministicSeed(request.Content)
	for i := 0; i < dim; i++ {
		vec[i] = float32(math.Cos(float64(seed+i+1) * 0.05))
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
		Provider:    "AZURE_OPENAI",
	}
}
