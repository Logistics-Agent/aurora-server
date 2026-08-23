package com.aurora.devopsagent.GrpcServices;

import com.aurora.devopsagent.Application.Commands.CreateRuleCommand;
import com.aurora.devopsagent.Application.Queries.ListRulesQueryHandler;
import com.aurora.devopsagent.Domain.Entity.ExistingRule;
import com.aurora.devopsagent.Domain.Entity.PendingRule;
import com.aurora.devopsagent.Infrastructure.Persistence.ExistingRuleJpaRepository;
import com.aurora.devopsagent.Infrastructure.Persistence.PendingRuleJpaRepository;
import com.aurora.devopsagent.grpc.*;
import com.aurora.shared.pagination.GrpcPaginationUtils;
import io.grpc.Status;
import io.grpc.stub.StreamObserver;
import net.devh.boot.grpc.server.service.GrpcService;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.transaction.annotation.Transactional;

import java.util.UUID;

@GrpcService
public class RuleGrpcHandler extends DevOpsRuleServiceGrpc.DevOpsRuleServiceImplBase {

    private final CreateRuleCommand.Handler createRuleHandler;
    private final ListRulesQueryHandler listRulesQueryHandler;
    private final ExistingRuleJpaRepository existingRuleRepository;
    private final PendingRuleJpaRepository pendingRuleRepository;
    private final com.aurora.devopsagent.Infrastructure.Audit.AuditEventOutboxService outboxService;

    public RuleGrpcHandler(
            CreateRuleCommand.Handler createRuleHandler,
            ListRulesQueryHandler listRulesQueryHandler,
            ExistingRuleJpaRepository existingRuleRepository,
            PendingRuleJpaRepository pendingRuleRepository,
            com.aurora.devopsagent.Infrastructure.Audit.AuditEventOutboxService outboxService) {
        this.createRuleHandler = createRuleHandler;
        this.listRulesQueryHandler = listRulesQueryHandler;
        this.existingRuleRepository = existingRuleRepository;
        this.pendingRuleRepository = pendingRuleRepository;
        this.outboxService = outboxService;
    }

    @Override
    public void listExistingRules(ListRulesRequest request, StreamObserver<ListExistingRulesResponse> responseObserver) {
        Page<ExistingRule> page = listRulesQueryHandler.handle(request.getPageNumber(), request.getPageSize());

        ListExistingRulesResponse.Builder builder = ListExistingRulesResponse.newBuilder()
                .setTotalElements((int) page.getTotalElements())
                .setTotalPages(page.getTotalPages())
                .setCurrentPage(page.getNumber());

        for (ExistingRule rule : page.getContent()) {
            builder.addRules(ExistingRuleSummary.newBuilder()
                    .setId(rule.getId().toString())
                    .setName(rule.getName() != null ? rule.getName() : "")
                    .setErrorSignaturePattern(rule.getErrorSignaturePattern() != null ? rule.getErrorSignaturePattern() : "")
                    .setTargetService(rule.getTargetService() != null ? rule.getTargetService() : "")
                    .setActionType(rule.getActionType() != null ? rule.getActionType() : "")
                    .setCreatedAt(rule.getCreatedAt() != null ? rule.getCreatedAt().toString() : "")
                    .build());
        }

        responseObserver.onNext(builder.build());
        responseObserver.onCompleted();
    }

    @Override
    public void createRule(CreateRuleRequest request, StreamObserver<RuleResponse> responseObserver) {
        CreateRuleCommand command = new CreateRuleCommand(
                request.getName(),
                request.getErrorSignaturePattern(),
                request.getTargetService(),
                request.getTargetDeployment(),
                request.getActionType(),
                request.getActionParamsJson(),
                request.getScopeConstraintJson()
        );

        ExistingRule saved = createRuleHandler.handle(command);

        RuleResponse response = RuleResponse.newBuilder()
                .setId(saved.getId().toString())
                .setName(saved.getName() != null ? saved.getName() : "")
                .setErrorSignaturePattern(saved.getErrorSignaturePattern() != null ? saved.getErrorSignaturePattern() : "")
                .setTargetService(saved.getTargetService() != null ? saved.getTargetService() : "")
                .setActionType(saved.getActionType() != null ? saved.getActionType() : "")
                .setCreatedAt(saved.getCreatedAt() != null ? saved.getCreatedAt().toString() : "")
                .build();

        responseObserver.onNext(response);
        responseObserver.onCompleted();
    }

    @Override
    @Transactional
    public void updateRule(UpdateRuleRequest request, StreamObserver<RuleResponse> responseObserver) {
        UUID ruleId;
        try {
            ruleId = UUID.fromString(request.getId());
        } catch (IllegalArgumentException e) {
            responseObserver.onError(Status.INVALID_ARGUMENT.withDescription("Invalid rule id").asRuntimeException());
            return;
        }

        ExistingRule rule = existingRuleRepository.findById(ruleId).orElse(null);
        if (rule == null) {
            responseObserver.onError(Status.NOT_FOUND.withDescription("Rule not found").asRuntimeException());
            return;
        }

        rule.setName(request.getName());
        rule.setErrorSignaturePattern(request.getErrorSignaturePattern());
        rule.setActionType(request.getActionType());
        rule.setActionParamsJson(request.getActionParamsJson());

        ExistingRule updated = existingRuleRepository.save(rule);

        RuleResponse response = RuleResponse.newBuilder()
                .setId(updated.getId().toString())
                .setName(updated.getName())
                .setErrorSignaturePattern(updated.getErrorSignaturePattern())
                .setActionType(updated.getActionType())
                .setCreatedAt(updated.getCreatedAt().toString())
                .build();

        responseObserver.onNext(response);
        responseObserver.onCompleted();
    }

    @Override
    @Transactional
    public void deleteRule(DeleteRuleRequest request, StreamObserver<EmptyDevOpsResponse> responseObserver) {
        try {
            UUID ruleId = UUID.fromString(request.getId());
            existingRuleRepository.deleteById(ruleId);
            responseObserver.onNext(EmptyDevOpsResponse.newBuilder().build());
            responseObserver.onCompleted();
        } catch (Exception e) {
            responseObserver.onError(Status.INTERNAL.withDescription(e.getMessage()).asRuntimeException());
        }
    }

    @Override
    public void listPendingRules(ListRulesRequest request, StreamObserver<ListPendingRulesResponse> responseObserver) {
        Pageable pageable = GrpcPaginationUtils.toPageable(request.getPageNumber(), request.getPageSize());
        Page<PendingRule> page = pendingRuleRepository.findByStatus("PENDING", pageable);

        ListPendingRulesResponse.Builder builder = ListPendingRulesResponse.newBuilder()
                .setTotalElements((int) page.getTotalElements())
                .setTotalPages(page.getTotalPages())
                .setCurrentPage(page.getNumber());

        for (PendingRule pr : page.getContent()) {
            builder.addRules(PendingRuleSummary.newBuilder()
                    .setId(pr.getId().toString())
                    .setErrorSignature(pr.getErrorPattern() != null ? pr.getErrorPattern() : "")
                    .setProposedAction(pr.getActionType() != null ? pr.getActionType() : "")
                    .setStatus(pr.getStatus() != null ? pr.getStatus() : "")
                    .setCreatedAt(pr.getCreatedAt() != null ? pr.getCreatedAt().toString() : "")
                    .build());
        }

        responseObserver.onNext(builder.build());
        responseObserver.onCompleted();
    }

    @Override
    @Transactional
    public void approvePendingRule(ApproveRuleRequest request, StreamObserver<ApprovalOperationResponse> responseObserver) {
        UUID ruleId;
        try {
            ruleId = UUID.fromString(request.getPendingRuleId());
        } catch (IllegalArgumentException e) {
            responseObserver.onError(Status.INVALID_ARGUMENT.withDescription("Invalid pending rule id").asRuntimeException());
            return;
        }

        PendingRule pr = pendingRuleRepository.findById(ruleId).orElse(null);
        if (pr == null) {
            responseObserver.onError(Status.NOT_FOUND.withDescription("Pending rule not found").asRuntimeException());
            return;
        }

        pr.setStatus("APPROVED");
        pendingRuleRepository.save(pr);

        // Promote to ExistingRule
        ExistingRule er = new ExistingRule();
        er.setName(pr.getProposedRuleName());
        er.setErrorSignaturePattern(pr.getErrorPattern());
        er.setActionType(pr.getActionType());
        er.setActionParamsJson(pr.getActionParamsJson());
        ExistingRule savedEr = existingRuleRepository.save(er);

        outboxService.enqueue(
                savedEr.getId().toString(),
                null,
                com.aurora.devopsagent.Domain.Enums.AuditActionType.RULE_PROMOTED,
                String.format("{\"pendingRuleId\":\"%s\",\"promotedRuleId\":\"%s\"}", pr.getId(), savedEr.getId())
        );

        ApprovalOperationResponse response = ApprovalOperationResponse.newBuilder()
                .setSuccess(true)
                .setNewStatus("APPROVED")
                .setMessage("Pending rule approved and promoted to active rule.")
                .build();

        responseObserver.onNext(response);
        responseObserver.onCompleted();
    }

    @Override
    @Transactional
    public void rejectPendingRule(RejectRuleRequest request, StreamObserver<ApprovalOperationResponse> responseObserver) {
        UUID ruleId;
        try {
            ruleId = UUID.fromString(request.getPendingRuleId());
        } catch (IllegalArgumentException e) {
            responseObserver.onError(Status.INVALID_ARGUMENT.withDescription("Invalid pending rule id").asRuntimeException());
            return;
        }

        PendingRule pr = pendingRuleRepository.findById(ruleId).orElse(null);
        if (pr == null) {
            responseObserver.onError(Status.NOT_FOUND.withDescription("Pending rule not found").asRuntimeException());
            return;
        }

        pr.setStatus("REJECTED");
        pendingRuleRepository.save(pr);

        outboxService.enqueue(
                pr.getId().toString(),
                null,
                com.aurora.devopsagent.Domain.Enums.AuditActionType.RULE_REJECTED,
                String.format("{\"pendingRuleId\":\"%s\",\"reason\":\"%s\"}", pr.getId(), request.getRejectionReason())
        );

        ApprovalOperationResponse response = ApprovalOperationResponse.newBuilder()
                .setSuccess(true)
                .setNewStatus("REJECTED")
                .setMessage("Pending rule rejected: " + request.getRejectionReason())
                .build();

        responseObserver.onNext(response);
        responseObserver.onCompleted();
    }
}
