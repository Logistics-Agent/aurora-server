using DocumentOcr.Domain.Enums;

namespace DocumentOcr.Application.Providers;

public static class OcrPromptBuilder
{
    public static string BuildPrompt(OcrExtractionMode mode, OcrDocumentType typeHint, string fileName)
    {
        return mode switch
        {
            OcrExtractionMode.FullText =>
                $"""
                You are a highly accurate Legal and Regulatory OCR Engine for SynchroCustoms logistics platform.
                Task: Perform FULL TEXT EXTRACTION for document: '{fileName}'.
                Instructions:
                1. Extract complete text verbatim, maintaining chronological section order and hierarchy.
                2. Preserve all section headers, article numbers, paragraphs, table contents, and bullet points.
                3. Mark page boundaries explicitly with '--- Page X ---' if multi-page.
                4. Output clean markdown text only without conversational preface.
                """,

            OcrExtractionMode.Both =>
                $"""
                You are an advanced Logistics Multimodal OCR Engine for SynchroCustoms platform.
                Task: Perform COMBINED STRUCTURED & FULL TEXT EXTRACTION for document: '{fileName}' (Hint: {typeHint}).
                Instructions:
                1. Output a valid JSON structure containing both 'detected_type', 'fields' array, 'overall_confidence', and 'full_text'.
                2. 'fields' must contain key-value pairs with field-level confidence scores (0.0 to 1.0).
                3. 'full_text' must contain the verbatim text preserving layout and headers.
                """,

            _ => // Structured (Default)
                $$"""
                You are a specialized Customs and Trade Document OCR Engine for SynchroCustoms logistics platform.
                Task: Perform STRUCTURED EXTRACTION for document: '{{fileName}}' (Hint: {{typeHint}}).
                Instructions:
                1. Extract all structured trade and logistics fields (e.g. invoice_number, bl_number, hs_code, declaration_number, seller, buyer, weight, currency, total_amount, container_numbers, port_of_loading, port_of_discharge).
                2. Output ONLY a valid JSON object with the following schema:
                {
                  "detected_type": "{{typeHint}}",
                  "overall_confidence": 0.95,
                  "needs_review": false,
                  "fields": [
                    { "name": "field_name", "value": "extracted_value", "confidence": 0.95 }
                  ]
                }
                3. Do not include markdown ticks or conversational wrappers.
                """
        };
    }
}
