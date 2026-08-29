using System;
using System.Collections.Generic;

namespace RoutePlanningAgent.Application.DTOs.Configs;

public record TenantRiskRuleDto(
    Guid Id,
    Guid PolicyId,
    string RuleCode,
    string RuleName,
    string ThresholdsJson,
    string RiskEffect,
    bool IsEnabled,
    string? SourceReference,
    DateTimeOffset CreatedAt
);

public record TenantRiskRuleInputDto(
    string RuleCode,
    string? RuleName,
    string? ThresholdsJson,
    string? RiskEffect,
    bool? IsEnabled,
    string? SourceReference
);

public record TenantRiskPolicyDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Description,
    string Scope,
    int Version,
    string Status,
    string Source,
    string? SourceDocumentId,
    Guid? SubmittedByUserId,
    DateTimeOffset? SubmittedAt,
    Guid? ReviewedByUserId,
    DateTimeOffset? ReviewedAt,
    string? ReviewerComment,
    Guid? PublishedByUserId,
    DateTimeOffset? PublishedAt,
    string? RejectionReason,
    DateTimeOffset? SupersededAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<TenantRiskRuleDto> Rules
);
