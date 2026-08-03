package com.aurora.devopsagent.GrpcServices;

import com.aurora.devopsagent.Application.Commands.CreateRuleCommand;
import com.aurora.devopsagent.Application.Commands.CreateRuleCommandHandler;
import com.aurora.devopsagent.Application.Queries.ListRulesQueryHandler;
import com.aurora.devopsagent.Domain.Entity.ExistingRule;
import com.aurora.devopsagent.grpc.*;
import io.grpc.stub.StreamObserver;
import net.devh.boot.grpc.server.service.GrpcService;
import org.springframework.data.domain.Page;

@GrpcService
public class RuleGrpcHandler extends DevOpsAgentServiceGrpc.DevOpsAgentServiceImplBase {

    private final CreateRuleCommandHandler createRuleCommandHandler;
    private final ListRulesQueryHandler listRulesQueryHandler;

    public RuleGrpcHandler(
            CreateRuleCommandHandler createRuleCommandHandler,
            ListRulesQueryHandler listRulesQueryHandler) {
        this.createRuleCommandHandler = createRuleCommandHandler;
        this.listRulesQueryHandler = listRulesQueryHandler;
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
                    .setCreatedBy(rule.getCreatedBy() != null ? rule.getCreatedBy() : "")
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

        ExistingRule saved = createRuleCommandHandler.handle(command);

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
}
