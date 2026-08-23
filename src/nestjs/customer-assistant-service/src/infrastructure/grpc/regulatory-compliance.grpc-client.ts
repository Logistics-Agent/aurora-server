import { Injectable, OnModuleInit, Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import * as grpc from '@grpc/grpc-js';
import * as protoLoader from '@grpc/proto-loader';
import * as path from 'path';
import { CurrentUser } from '../security/current-user.interface';
import {
  RegulatoryCitationInfo,
  KnowledgeReferenceInfo,
  GroundedConflictInfo,
} from '../../domain/entities/message.entity';

export interface GroundedAnswerGrpcResult {
  query: string;
  answer: string;
  regulatoryCitations: RegulatoryCitationInfo[];
  knowledgeReferences: KnowledgeReferenceInfo[];
  conflicts: GroundedConflictInfo[];
  insufficientEvidence: boolean;
  missingInformation: string[];
  governance: {
    decisionId: string;
    automationLevel: string;
    requiresApproval: boolean;
    capabilityCode: string;
    totalTokens: number;
  };
  retrievalTraceId: string;
}

@Injectable()
export class RegulatoryComplianceGrpcClient implements OnModuleInit {
  private readonly logger = new Logger(RegulatoryComplianceGrpcClient.name);
  private client: any;

  constructor(private readonly configService: ConfigService) {}

  onModuleInit() {
    const protoPath = path.resolve(__dirname, '../../../../../protos/regulatory_compliance.proto');
    const grpcUrl =
      this.configService.get<string>('REGULATORY_COMPLIANCE_GRPC_URL') || 'localhost:5002';

    try {
      const packageDefinition = protoLoader.loadSync(protoPath, {
        keepCase: true,
        longs: String,
        enums: String,
        defaults: true,
        oneofs: true,
      });

      const protoDescriptor = grpc.loadPackageDefinition(packageDefinition) as any;
      const service = protoDescriptor.regulatory_compliance.RegulatoryComplianceService;
      this.client = new service(grpcUrl, grpc.credentials.createInsecure());
      this.logger.log(`[RegulatoryComplianceGrpcClient] Connected to RegulatoryCompliance at ${grpcUrl}`);
    } catch (err) {
      this.logger.warn(`[RegulatoryComplianceGrpcClient] Failed to load proto from ${protoPath}. Initializing fallback client.`, err);
    }
  }

  async queryRegulations(
    query: string,
    jurisdictionCode: string,
    topK = 5,
    minimumScore = 0.4,
    context?: CurrentUser,
  ): Promise<RegulatoryCitationInfo[]> {
    if (!this.client) return [];

    const metadata = this.buildMetadata(context);
    const request = {
      query,
      jurisdiction_code: jurisdictionCode,
      effective_at: { seconds: Math.floor(Date.now() / 1000), nanos: 0 },
      preferred_language: 'vi',
      top_k: topK,
      minimum_relevance_score: minimumScore,
    };

    return new Promise((resolve) => {
      this.client.QueryRegulations(request, metadata, { deadline: Date.now() + 15000 }, (err: any, response: any) => {
        if (err) {
          this.logger.warn(`[RegulatoryComplianceGrpcClient] QueryRegulations error: ${err.message}`);
          return resolve([]);
        }

        const evidence = (response.evidence || []).map((e: any, idx: number) => ({
          evidenceId: `R${idx + 1}`,
          sourceId: e.citation?.regulatory_document_id || '',
          documentVersionId: e.citation?.document_version_id || '',
          chunkId: e.citation?.chunk_id || '',
          title: e.citation?.title || 'Regulatory Rule',
          authority: e.citation?.authority || '',
          jurisdiction: jurisdictionCode,
          regulationType: 'Regulation',
          section: e.citation?.section_label || '',
          page: e.citation?.page_label || '',
          excerpt: e.citation?.excerpt || '',
          canonicalSourceUri: e.citation?.canonical_source_uri || '',
          score: Number(e.citation?.relevance_score || 0),
        }));

        resolve(evidence);
      });
    });
  }

  async queryKnowledge(
    query: string,
    categories: (string | number)[] = [],
    topK = 5,
    minimumScore = 0.4,
    context?: CurrentUser,
  ): Promise<KnowledgeReferenceInfo[]> {
    if (!this.client) return [];

    const mappedCategories = categories
      .map((c) => {
        if (typeof c === 'number') return c;
        const upper = c.toUpperCase();
        if (upper === 'SOP' || upper === 'PUBLIC_PROCEDURE') return 1;
        if (upper === 'POLICY' || upper === 'PRICING_POLICY') return 2;
        if (upper === 'GUIDELINE' || upper === 'PUBLIC_FAQ' || upper === 'CUSTOMER_GUIDE') return 3;
        if (upper === 'CARRIER_CONTRACT') return 4;
        if (upper === 'INTERNAL_RULE') return 5;
        return 0;
      })
      .filter((c) => c > 0);

    const metadata = this.buildMetadata(context);
    const request = {
      query,
      categories: mappedCategories,
      top_k: topK,
      minimum_relevance_score: minimumScore,
    };

    return new Promise((resolve) => {
      this.client.QueryKnowledge(request, metadata, { deadline: Date.now() + 15000 }, (err: any, response: any) => {
        if (err) {
          this.logger.warn(`[RegulatoryComplianceGrpcClient] QueryKnowledge error: ${err.message}`);
          return resolve([]);
        }

        const evidence = (response.evidence || []).map((k: any, idx: number) => ({
          evidenceId: `K${idx + 1}`,
          sourceId: k.knowledge_document_id || '',
          documentVersionId: k.document_version_id || '',
          chunkId: k.chunk_id || '',
          title: k.title || 'Knowledge SOP',
          category: k.category || 'SOP',
          section: k.section_label || '',
          page: k.page_label || '',
          excerpt: k.excerpt || '',
          score: Number(k.relevance_score || 0),
        }));

        resolve(evidence);
      });
    });
  }

  async generateGroundedAnswer(
    request: {
      query: string;
      mode?: 'REGULATORY' | 'KNOWLEDGE' | 'ALL';
      jurisdictionCode?: string;
      regulationTypes?: number[];
      categories?: number[];
      topK?: number;
      minimumScore?: number;
    },
    context?: CurrentUser,
  ): Promise<GroundedAnswerGrpcResult> {
    if (!this.client) {
      return {
        query: request.query,
        answer: 'Regulatory compliance service is currently unavailable in offline development mode.',
        regulatoryCitations: [],
        knowledgeReferences: [],
        conflicts: [],
        insufficientEvidence: true,
        missingInformation: ['Regulatory gRPC client offline'],
        governance: {
          decisionId: 'offline-dec',
          automationLevel: 'DETERMINISTIC_FALLBACK',
          requiresApproval: false,
          capabilityCode: 'compliance.answer',
          totalTokens: 0,
        },
        retrievalTraceId: 'trace-offline',
      };
    }

    const metadata = this.buildMetadata(context);
    const modeVal =
      request.mode === 'REGULATORY'
        ? 2
        : request.mode === 'KNOWLEDGE'
        ? 3
        : 1;

    const rpcReq = {
      query: request.query,
      mode: modeVal,
      jurisdiction_code: request.jurisdictionCode || '',
      effective_at: { seconds: Math.floor(Date.now() / 1000), nanos: 0 },
      regulation_types: request.regulationTypes || [],
      categories: request.categories || [],
      top_k: request.topK || 5,
      minimum_relevance_score: request.minimumScore || 0.4,
    };

    return new Promise((resolve, reject) => {
      this.client.GenerateGroundedAnswer(
        rpcReq,
        metadata,
        { deadline: Date.now() + 45000 },
        (err: any, response: any) => {
          if (err) {
            this.logger.error(`[RegulatoryComplianceGrpcClient] GenerateGroundedAnswer error: ${err.message}`, err);
            return reject(err);
          }

          const regCitations: RegulatoryCitationInfo[] = (response.regulatory_citations || []).map((r: any) => ({
            evidenceId: r.evidence_id || '',
            sourceId: r.source_id || '',
            documentVersionId: r.document_version_id || '',
            chunkId: r.chunk_id || '',
            title: r.title || '',
            authority: r.authority || '',
            jurisdiction: r.jurisdiction || '',
            regulationType: r.regulation_type || '',
            section: r.section || '',
            page: r.page || '',
            excerpt: r.excerpt || '',
            canonicalSourceUri: r.canonical_source_uri || '',
            score: Number(r.score || 0),
          }));

          const knowReferences: KnowledgeReferenceInfo[] = (response.knowledge_references || []).map((k: any) => ({
            evidenceId: k.evidence_id || '',
            sourceId: k.source_id || '',
            documentVersionId: k.document_version_id || '',
            chunkId: k.chunk_id || '',
            title: k.title || '',
            category: k.category || '',
            section: k.section || '',
            page: k.page || '',
            excerpt: k.excerpt || '',
            score: Number(k.score || 0),
          }));

          const conflicts: GroundedConflictInfo[] = (response.conflicts || []).map((c: any) => ({
            regulatoryEvidenceId: c.regulatory_evidence_id || '',
            knowledgeEvidenceId: c.knowledge_evidence_id || '',
            description: c.description || '',
          }));

          resolve({
            query: response.query || request.query,
            answer: response.answer || '',
            regulatoryCitations: regCitations,
            knowledgeReferences: knowReferences,
            conflicts,
            insufficientEvidence: Boolean(response.insufficient_evidence),
            missingInformation: response.missing_information || [],
            governance: {
              decisionId: response.governance?.decision_id || '',
              automationLevel: response.governance?.automation_level || 'ASSISTED',
              requiresApproval: Boolean(response.governance?.requires_approval),
              capabilityCode: response.governance?.capability_code || 'compliance.answer',
              totalTokens: Number(response.governance?.total_tokens || 0),
            },
            retrievalTraceId: response.retrieval_trace_id || '',
          });
        },
      );
    });
  }

  async validateGroundedEvidence(
    request: {
      answer: string;
      citations: Array<{ evidenceId: string }>;
      knowledgeReferences: Array<{ evidenceId: string }>;
      conflicts: Array<{ regulatoryEvidenceId: string; knowledgeEvidenceId: string; description: string }>;
      insufficientEvidence: boolean;
      missingInformation: string[];
      availableRegulatoryEvidence: RegulatoryCitationInfo[];
      availableKnowledgeEvidence: KnowledgeReferenceInfo[];
    },
    context?: CurrentUser,
  ): Promise<{
    sanitizedAnswer: string;
    validatedRegulatoryCitations: RegulatoryCitationInfo[];
    validatedKnowledgeReferences: KnowledgeReferenceInfo[];
    validatedConflicts: GroundedConflictInfo[];
    insufficientEvidence: boolean;
    missingInformation: string[];
  }> {
    if (!this.client) {
      // Local fallback validator when gRPC client offline
      const validReg = request.availableRegulatoryEvidence.filter((r) =>
        request.citations.some((c) => c.evidenceId.toUpperCase() === r.evidenceId.toUpperCase()),
      );
      const validKnow = request.availableKnowledgeEvidence.filter((k) =>
        request.knowledgeReferences.some((kr) => kr.evidenceId.toUpperCase() === k.evidenceId.toUpperCase()),
      );
      const validConflicts = request.conflicts.filter(
        (c) =>
          request.availableRegulatoryEvidence.some((r) => r.evidenceId === c.regulatoryEvidenceId) &&
          request.availableKnowledgeEvidence.some((k) => k.evidenceId === c.knowledgeEvidenceId),
      );

      return {
        sanitizedAnswer: request.answer,
        validatedRegulatoryCitations: validReg,
        validatedKnowledgeReferences: validKnow,
        validatedConflicts: validConflicts,
        insufficientEvidence: request.insufficientEvidence,
        missingInformation: request.missingInformation,
      };
    }

    const metadata = this.buildMetadata(context);
    const rpcReq = {
      answer: request.answer,
      citations: request.citations.map((c) => ({ evidence_id: c.evidenceId })),
      knowledge_references: request.knowledgeReferences.map((k) => ({ evidence_id: k.evidenceId })),
      conflicts: request.conflicts.map((c) => ({
        regulatory_evidence_id: c.regulatoryEvidenceId,
        knowledge_evidence_id: c.knowledgeEvidenceId,
        description: c.description,
      })),
      insufficient_evidence: request.insufficientEvidence,
      missing_information: request.missingInformation,
      available_regulatory_evidence: request.availableRegulatoryEvidence.map((r) => ({
        evidence_id: r.evidenceId,
        source_id: r.sourceId,
        document_version_id: r.documentVersionId,
        chunk_id: r.chunkId,
        title: r.title,
        authority: r.authority,
        jurisdiction: r.jurisdiction,
        regulation_type: r.regulationType,
        section: r.section,
        page: r.page,
        excerpt: r.excerpt,
        canonical_source_uri: r.canonicalSourceUri,
        score: r.score,
      })),
      available_knowledge_evidence: request.availableKnowledgeEvidence.map((k) => ({
        evidence_id: k.evidenceId,
        source_id: k.sourceId,
        document_version_id: k.documentVersionId,
        chunk_id: k.chunkId,
        title: k.title,
        category: k.category,
        section: k.section,
        page: k.page,
        excerpt: k.excerpt,
        score: k.score,
      })),
    };

    return new Promise((resolve) => {
      this.client.ValidateGroundedEvidence(
        rpcReq,
        metadata,
        { deadline: Date.now() + 10000 },
        (err: any, response: any) => {
          if (err) {
            this.logger.warn(`[RegulatoryComplianceGrpcClient] ValidateGroundedEvidence error: ${err.message}. Using local fallback.`);
            const validReg = request.availableRegulatoryEvidence.filter((r) =>
              request.citations.some((c) => c.evidenceId.toUpperCase() === r.evidenceId.toUpperCase()),
            );
            const validKnow = request.availableKnowledgeEvidence.filter((k) =>
              request.knowledgeReferences.some((kr) => kr.evidenceId.toUpperCase() === k.evidenceId.toUpperCase()),
            );
            return resolve({
              sanitizedAnswer: request.answer,
              validatedRegulatoryCitations: validReg,
              validatedKnowledgeReferences: validKnow,
              validatedConflicts: request.conflicts,
              insufficientEvidence: request.insufficientEvidence,
              missingInformation: request.missingInformation,
            });
          }

          const validatedReg: RegulatoryCitationInfo[] = (response.validated_regulatory_citations || []).map((r: any) => ({
            evidenceId: r.evidence_id || '',
            sourceId: r.source_id || '',
            documentVersionId: r.document_version_id || '',
            chunkId: r.chunk_id || '',
            title: r.title || '',
            authority: r.authority || '',
            jurisdiction: r.jurisdiction || '',
            regulationType: r.regulation_type || '',
            section: r.section || '',
            page: r.page || '',
            excerpt: r.excerpt || '',
            canonicalSourceUri: r.canonical_source_uri || '',
            score: Number(r.score || 0),
          }));

          const validatedKnow: KnowledgeReferenceInfo[] = (response.validated_knowledge_references || []).map((k: any) => ({
            evidenceId: k.evidence_id || '',
            sourceId: k.source_id || '',
            documentVersionId: k.document_version_id || '',
            chunkId: k.chunk_id || '',
            title: k.title || '',
            category: k.category || '',
            section: k.section || '',
            page: k.page || '',
            excerpt: k.excerpt || '',
            score: Number(k.score || 0),
          }));

          const validatedConflicts: GroundedConflictInfo[] = (response.validated_conflicts || []).map((c: any) => ({
            regulatoryEvidenceId: c.regulatory_evidence_id || '',
            knowledgeEvidenceId: c.knowledge_evidence_id || '',
            description: c.description || '',
          }));

          resolve({
            sanitizedAnswer: response.sanitized_answer || request.answer,
            validatedRegulatoryCitations: validatedReg,
            validatedKnowledgeReferences: validatedKnow,
            validatedConflicts,
            insufficientEvidence: Boolean(response.insufficient_evidence),
            missingInformation: response.missing_information || [],
          });
        },
      );
    });
  }

  private buildMetadata(context?: CurrentUser): grpc.Metadata {
    const metadata = new grpc.Metadata();
    metadata.add('x-service-id', 'customer-assistant-orchestrator');
    if (context?.tenantId) metadata.add('x-tenant-id', context.tenantId);
    if (context?.userId) metadata.add('x-user-id', context.userId);
    if (context?.traceId) metadata.add('x-trace-id', context.traceId);
    return metadata;
  }
}
