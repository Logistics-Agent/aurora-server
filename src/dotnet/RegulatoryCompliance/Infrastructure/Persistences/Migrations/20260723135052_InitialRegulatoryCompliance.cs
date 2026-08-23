using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RegulatoryCompliance.Infrastructure.Persistences.Migrations
{
    /// <inheritdoc />
    public partial class InitialRegulatoryCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "compliance_evaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ExternalShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RiskLevel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    EvidenceSufficiency = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    AssumptionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    MissingDocumentsJson = table.Column<string>(type: "jsonb", nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessingStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_evaluations", x => x.Id);
                    table.UniqueConstraint("AK_compliance_evaluations_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceEventType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ContractVersion = table.Column<int>(type: "integer", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Content = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "regulatory_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScopeKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Authority = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CanonicalSourceUri = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    JurisdictionCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RegulationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulatory_documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "compliance_findings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComplianceEvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Severity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_findings", x => x.Id);
                    table.UniqueConstraint("AK_compliance_findings_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_compliance_findings_compliance_evaluations_TenantId_Complia~",
                        columns: x => new { x.TenantId, x.ComplianceEvaluationId },
                        principalTable: "compliance_evaluations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "retrieval_traces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComplianceEvaluationId = table.Column<Guid>(type: "uuid", nullable: true),
                    QueryHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    JurisdictionCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    RegulationTypesJson = table.Column<string>(type: "jsonb", nullable: false),
                    EmbeddingModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TopK = table.Column<int>(type: "integer", nullable: false),
                    MinimumRelevanceScore = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    RetrievedChunkIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ScoresJson = table.Column<string>(type: "jsonb", nullable: false),
                    EvidenceSufficiency = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retrieval_traces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_retrieval_traces_compliance_evaluations_TenantId_Compliance~",
                        columns: x => new { x.TenantId, x.ComplianceEvaluationId },
                        principalTable: "compliance_evaluations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "regulatory_document_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScopeKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RegulatoryDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngestionKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    VersionLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContentReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    IngestionStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SupersedesVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupersededAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ChunkCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ProcessingStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulatory_document_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_regulatory_document_versions_regulatory_document_versions_S~",
                        column: x => x.SupersedesVersionId,
                        principalTable: "regulatory_document_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_regulatory_document_versions_regulatory_documents_Regulator~",
                        column: x => x.RegulatoryDocumentId,
                        principalTable: "regulatory_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "regulatory_chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScopeKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RegulatoryDocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    SectionLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PageLabel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NormalizedText = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    TokenCount = table.Column<int>(type: "integer", nullable: false),
                    CharacterCount = table.Column<int>(type: "integer", nullable: false),
                    StartOffset = table.Column<int>(type: "integer", nullable: false),
                    EndOffset = table.Column<int>(type: "integer", nullable: false),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EmbeddingStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Embedding = table.Column<float[]>(type: "real[]", nullable: true),
                    EmbeddingModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmbeddingModelVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EmbeddedContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EmbeddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EmbeddingError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulatory_chunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_regulatory_chunks_regulatory_document_versions_RegulatoryDo~",
                        column: x => x.RegulatoryDocumentVersionId,
                        principalTable: "regulatory_document_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "compliance_citations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ComplianceFindingId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegulatoryDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegulatoryDocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegulatoryChunkId = table.Column<Guid>(type: "uuid", nullable: false),
                    Authority = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CanonicalSourceUri = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    VersionLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SectionLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PageLabel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Excerpt = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RelevanceScore = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_citations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compliance_citations_compliance_findings_TenantId_Complianc~",
                        columns: x => new { x.TenantId, x.ComplianceFindingId },
                        principalTable: "compliance_findings",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_compliance_citations_regulatory_chunks_RegulatoryChunkId",
                        column: x => x.RegulatoryChunkId,
                        principalTable: "regulatory_chunks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_compliance_citations_regulatory_document_versions_Regulator~",
                        column: x => x.RegulatoryDocumentVersionId,
                        principalTable: "regulatory_document_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_compliance_citations_regulatory_documents_RegulatoryDocumen~",
                        column: x => x.RegulatoryDocumentId,
                        principalTable: "regulatory_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_compliance_citations_RegulatoryChunkId",
                table: "compliance_citations",
                column: "RegulatoryChunkId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_citations_RegulatoryDocumentId",
                table: "compliance_citations",
                column: "RegulatoryDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_citations_RegulatoryDocumentVersionId",
                table: "compliance_citations",
                column: "RegulatoryDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_compliance_citations_TenantId_ComplianceFindingId_Regulator~",
                table: "compliance_citations",
                columns: new[] { "TenantId", "ComplianceFindingId", "RegulatoryChunkId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_compliance_evaluations_TenantId_ExternalShipmentId_Requeste~",
                table: "compliance_evaluations",
                columns: new[] { "TenantId", "ExternalShipmentId", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_compliance_evaluations_TenantId_IdempotencyKey",
                table: "compliance_evaluations",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_compliance_evaluations_TenantId_RequestHash",
                table: "compliance_evaluations",
                columns: new[] { "TenantId", "RequestHash" });

            migrationBuilder.CreateIndex(
                name: "IX_compliance_evaluations_TenantId_Status_RequestedAt",
                table: "compliance_evaluations",
                columns: new[] { "TenantId", "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_compliance_findings_TenantId_ComplianceEvaluationId_Type",
                table: "compliance_findings",
                columns: new[] { "TenantId", "ComplianceEvaluationId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_SourceEventType_SourceEventId",
                table: "inbox_messages",
                columns: new[] { "SourceEventType", "SourceEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_TenantId_ReceivedAt",
                table: "inbox_messages",
                columns: new[] { "TenantId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_EventId",
                table: "outbox_messages",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedAt_RetryCount_OccurredAt",
                table: "outbox_messages",
                columns: new[] { "ProcessedAt", "RetryCount", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_TenantId_OccurredAt",
                table: "outbox_messages",
                columns: new[] { "TenantId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_regulatory_chunks_RegulatoryDocumentVersionId_Sequence",
                table: "regulatory_chunks",
                columns: new[] { "RegulatoryDocumentVersionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_regulatory_chunks_ScopeKey_ContentSha256",
                table: "regulatory_chunks",
                columns: new[] { "ScopeKey", "ContentSha256" });

            migrationBuilder.CreateIndex(
                name: "IX_regulatory_chunks_ScopeKey_EmbeddingStatus_CreatedAt",
                table: "regulatory_chunks",
                columns: new[] { "ScopeKey", "EmbeddingStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_regulatory_document_versions_RegulatoryDocumentId_ContentSh~",
                table: "regulatory_document_versions",
                columns: new[] { "RegulatoryDocumentId", "ContentSha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_regulatory_document_versions_RegulatoryDocumentId_VersionLa~",
                table: "regulatory_document_versions",
                columns: new[] { "RegulatoryDocumentId", "VersionLabel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_regulatory_document_versions_ScopeKey_IngestionKey",
                table: "regulatory_document_versions",
                columns: new[] { "ScopeKey", "IngestionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_regulatory_document_versions_ScopeKey_IngestionStatus_Effec~",
                table: "regulatory_document_versions",
                columns: new[] { "ScopeKey", "IngestionStatus", "EffectiveFrom", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_regulatory_document_versions_SupersedesVersionId",
                table: "regulatory_document_versions",
                column: "SupersedesVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_regulatory_documents_ScopeKey_CanonicalSourceUri_Jurisdicti~",
                table: "regulatory_documents",
                columns: new[] { "ScopeKey", "CanonicalSourceUri", "JurisdictionCode", "LanguageCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_regulatory_documents_ScopeKey_JurisdictionCode_RegulationTy~",
                table: "regulatory_documents",
                columns: new[] { "ScopeKey", "JurisdictionCode", "RegulationType", "LanguageCode" });

            migrationBuilder.CreateIndex(
                name: "IX_retrieval_traces_TenantId_ComplianceEvaluationId_CreatedAt",
                table: "retrieval_traces",
                columns: new[] { "TenantId", "ComplianceEvaluationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_retrieval_traces_TenantId_QueryHash_CreatedAt",
                table: "retrieval_traces",
                columns: new[] { "TenantId", "QueryHash", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "compliance_citations");

            migrationBuilder.DropTable(
                name: "inbox_messages");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "retrieval_traces");

            migrationBuilder.DropTable(
                name: "compliance_findings");

            migrationBuilder.DropTable(
                name: "regulatory_chunks");

            migrationBuilder.DropTable(
                name: "compliance_evaluations");

            migrationBuilder.DropTable(
                name: "regulatory_document_versions");

            migrationBuilder.DropTable(
                name: "regulatory_documents");
        }
    }
}
