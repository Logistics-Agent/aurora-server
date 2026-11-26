package domain

type RedactedIncidentContext struct {
	CorrelationID        string
	ErrorSignature       string
	AffectedService      string
	SanitizedContextJson string
	ExtraSummary         string
}

func NewRedactedIncidentContext(correlationID, errorSignature, affectedService, sanitizedContextJson, extraSummary string) RedactedIncidentContext {
	return RedactedIncidentContext{
		CorrelationID:        correlationID,
		ErrorSignature:       errorSignature,
		AffectedService:      affectedService,
		SanitizedContextJson: sanitizedContextJson,
		ExtraSummary:         extraSummary,
	}
}
