package provider

import (
	"hash/fnv"
	"math"
	"strings"

	"aurora/ai-governance/internal/gateway/domain"
)

const JsonObjectResponseFormat = "json_object"

func RequiresJSONObject(request domain.AiGenerateRequest) bool {
	if request.CapabilityCode == "compliance.answer" {
		return true
	}
	if request.Parameters == nil {
		return false
	}
	return strings.EqualFold(request.Parameters["response_format"], JsonObjectResponseFormat)
}

func DeterministicJSONResponse() string {
	return `{"answer":"The configured test AI provider returned no generated answer.","citations":[],"knowledgeReferences":[],"conflicts":[],"insufficientEvidence":true,"missingInformation":["A live AI provider is required to generate a grounded answer."]}`
}

func IsTestKey(apiKey string) bool {
	return apiKey == "" ||
		strings.HasPrefix(apiKey, "demo") ||
		strings.HasPrefix(apiKey, "test") ||
		strings.HasPrefix(apiKey, "mock") ||
		apiKey == "dev-key"
}

func NormalizeVector(vec []float32) {
	var normSq float64
	for _, v := range vec {
		normSq += float64(v) * float64(v)
	}
	if normSq <= 0.0 {
		return
	}
	norm := math.Sqrt(normSq)
	for i := range vec {
		vec[i] = float32(float64(vec[i]) / norm)
	}
}

func DeterministicSeed(content string) int {
	if content == "" {
		return 42
	}
	h := fnv.New32a()
	_, _ = h.Write([]byte(content))
	return int(int32(h.Sum32()))
}
