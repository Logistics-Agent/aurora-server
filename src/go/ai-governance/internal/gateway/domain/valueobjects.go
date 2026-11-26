package domain

type PartType string

const (
	PartTypeText PartType = "TEXT"
	PartTypeFile PartType = "FILE"
)

type FileReference struct {
	StorageReference string
	MimeType         string
	FileName         string
	SizeBytes        int64
}

type MultimodalPart struct {
	Type PartType
	Text string
	File *FileReference
}

func TextPart(text string) MultimodalPart {
	return MultimodalPart{Type: PartTypeText, Text: text}
}

func FilePart(storageRef, mimeType, fileName string, sizeBytes int64) MultimodalPart {
	return MultimodalPart{
		Type: PartTypeFile,
		File: &FileReference{
			StorageReference: storageRef,
			MimeType:         mimeType,
			FileName:         fileName,
			SizeBytes:        sizeBytes,
		},
	}
}

type AiGenerateRequest struct {
	CapabilityCode       string
	Prompt               string
	MaxOutputTokens      int64
	EstimatedInputTokens int64
	Parameters           map[string]string
	InputParts           []MultimodalPart
}

type AiGenerateResult struct {
	Content      string
	InputTokens  int64
	OutputTokens int64
	Model        string
	Provider     string
}

type AiEmbeddingRequest struct {
	CapabilityCode       string
	Content              string
	Dimensions           *int32
	EstimatedInputTokens int64
}

type AiEmbeddingResult struct {
	Vector      []float32
	InputTokens int64
	Model       string
	Provider    string
}

type ProviderCapacityLimits struct {
	RpmLimit int64
	TpmLimit int64
	RpdLimit int64
}

type ProviderReservation struct {
	ReservationID  string
	Slot           *ProviderSlot
	ReservedTokens int64
	RpmKey         string
	TpmKey         string
	RpdKey         string
}

type RateWindow struct {
	BucketKey  string
	TTLSeconds int64
}

type SlotCapacity struct {
	CurrentRpm int64
	CurrentTpm int64
	CurrentRpd int64
}
