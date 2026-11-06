ALTER TABLE incidents
    ADD COLUMN evidence_json TEXT NOT NULL DEFAULT '{}';
