package shared

type AiOperation string

const (
	AiOperationGenerate AiOperation = "GENERATE"
	AiOperationEmbed    AiOperation = "EMBED"
)

type TokenBudget struct {
	EstimatedInputTokens int64
	MaxOutputTokens      int64
}

func NewTokenBudget(estimatedInputTokens, maxOutputTokens int64) TokenBudget {
	return TokenBudget{
		EstimatedInputTokens: estimatedInputTokens,
		MaxOutputTokens:      maxOutputTokens,
	}
}

func ForEmbedding(estimatedInputTokens int64) TokenBudget {
	return TokenBudget{
		EstimatedInputTokens: estimatedInputTokens,
		MaxOutputTokens:      0,
	}
}

func (tb TokenBudget) ReservationTokens() int64 {
	return tb.EstimatedInputTokens + tb.MaxOutputTokens
}
