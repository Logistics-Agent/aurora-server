using System;
using System.Collections.Generic;
using System.Text.Json;
using Shared.Exceptions;

namespace RoutePlanningAgent.Domain.Services;

public static class TenantRuleValidator
{
    public static readonly HashSet<string> KnownRuleCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ROUTE_WEIGHT_CAPACITY",
        "ROUTE_VOLUME_CAPACITY",
        "ROUTE_DURATION_LIMIT",
        "ROUTE_MIN_STOPS",
        "ROUTE_MULTI_HUB",
        "ROUTE_ON_DEMAND",
        "ROUTE_STOP_COUNT"
    };

    public static readonly Dictionary<string, string> CodeToNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "ROUTE_WEIGHT_CAPACITY", "HeavyWeightRule" },
        { "ROUTE_VOLUME_CAPACITY", "LargeVolumeRule" },
        { "ROUTE_DURATION_LIMIT", "LongDurationRule" },
        { "ROUTE_MIN_STOPS", "MinimumStopsRule" },
        { "ROUTE_MULTI_HUB", "MultiHubRule" },
        { "ROUTE_ON_DEMAND", "OnDemandTypeRule" },
        { "ROUTE_STOP_COUNT", "RouteStopCountRule" }
    };

    /// <summary>
    /// Validates structured rules and their typed thresholds before a policy can be published.
    /// Throws DomainValidationException if any rule has invalid code or unparseable/invalid threshold JSON.
    /// </summary>
    public static void ValidateRulesForPublish(IEnumerable<TenantRiskRule> rules)
    {
        var ruleList = rules as IList<TenantRiskRule> ?? new List<TenantRiskRule>(rules);
        if (ruleList.Count == 0)
        {
            throw new DomainValidationException("Chính sách rủi ro phải chứa ít nhất 1 quy tắc (Rule) trước khi phê duyệt / phát hành.");
        }

        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in ruleList)
        {
            if (string.IsNullOrWhiteSpace(rule.RuleCode))
            {
                throw new DomainValidationException("RuleCode không được để trống.");
            }

            if (!KnownRuleCodes.Contains(rule.RuleCode))
            {
                throw new DomainValidationException($"Mã quy tắc '{rule.RuleCode}' không hợp lệ hoặc không được hệ thống hỗ trợ.");
            }

            if (!seenCodes.Add(rule.RuleCode))
            {
                throw new DomainValidationException($"Quy tắc với mã '{rule.RuleCode}' bị trùng lặp trong chính sách.");
            }

            ValidateThresholdsJson(rule.RuleCode, rule.ThresholdsJson);
        }
    }

    public static Dictionary<string, decimal> ValidateThresholdsJson(string ruleCode, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, decimal>();
        }

        Dictionary<string, decimal>? dict;
        try
        {
            dict = JsonSerializer.Deserialize<Dictionary<string, decimal>>(json);
        }
        catch (Exception ex)
        {
            throw new DomainValidationException($"Cấu hình ngưỡng ThresholdsJson của quy tắc '{ruleCode}' không phải JSON hợp lệ: {ex.Message}");
        }

        if (dict == null)
        {
            throw new DomainValidationException($"ThresholdsJson của quy tắc '{ruleCode}' không được là null.");
        }

        // Validate numeric boundaries
        foreach (var (key, val) in dict)
        {
            if (val < 0)
            {
                throw new DomainValidationException($"Giá trị ngưỡng '{key}' ({val}) của quy tắc '{ruleCode}' không được là số âm.");
            }
        }

        return dict;
    }
}
