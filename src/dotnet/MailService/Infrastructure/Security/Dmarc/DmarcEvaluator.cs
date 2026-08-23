using System;
using System.Collections.Generic;

namespace MailService.Infrastructure.Security.Dmarc;

public enum DmarcStatus
{
    Pass,
    Fail,
    None,
    TempError,
    PermError
}

public enum DmarcPolicy
{
    None,
    Quarantine,
    Reject
}

public enum DmarcEnforcementAction
{
    Accept,
    Quarantine,
    Reject
}

public record DmarcEvaluationResult(
    DmarcStatus Status,
    DmarcPolicy Policy,
    DmarcPolicy SubdomainPolicy,
    int Percentage,
    bool SpfAligned,
    bool DkimAligned,
    string? Rua = null
)
{
    public static DmarcEvaluationResult None(string explanation) =>
        new(DmarcStatus.None, DmarcPolicy.None, DmarcPolicy.None, 100, false, false);
}

public record DmarcEnforcementDecision(
    DmarcEnforcementAction Action,
    string Reason
)
{
    public bool ShouldQuarantine => Action == DmarcEnforcementAction.Quarantine;
    public bool ShouldReject => Action == DmarcEnforcementAction.Reject;
}


public class DmarcEvaluator
{
    /// <summary>
    /// Evaluates DMARC alignment and policy from DNS record tags (RFC 7489 Section 6.6)
    /// </summary>
    public DmarcEvaluationResult Evaluate(
        string fromDomain,
        string spfResult,
        string spfDomain,
        string dkimResult,
        string dkimDomain,
        string? dmarcRecord)
    {
        if (string.IsNullOrWhiteSpace(dmarcRecord))
            return DmarcEvaluationResult.None("No DMARC record published");

        var tags = ParseTags(dmarcRecord);
        if (!tags.TryGetValue("v", out var v) || !v.Equals("DMARC1", StringComparison.OrdinalIgnoreCase))
            return DmarcEvaluationResult.None("Invalid DMARC version tag");

        DmarcPolicy policy = DmarcPolicy.None;
        if (tags.TryGetValue("p", out var pTag))
        {
            policy = pTag.ToLowerInvariant() switch
            {
                "reject" => DmarcPolicy.Reject,
                "quarantine" => DmarcPolicy.Quarantine,
                _ => DmarcPolicy.None
            };
        }

        DmarcPolicy subdomainPolicy = policy;
        if (tags.TryGetValue("sp", out var spTag))
        {
            subdomainPolicy = spTag.ToLowerInvariant() switch
            {
                "reject" => DmarcPolicy.Reject,
                "quarantine" => DmarcPolicy.Quarantine,
                "none" => DmarcPolicy.None,
                _ => policy
            };
        }

        int percentage = 100;
        if (tags.TryGetValue("pct", out var pctStr) && int.TryParse(pctStr, out int pctVal))
        {
            percentage = Math.Clamp(pctVal, 0, 100);
        }

        tags.TryGetValue("rua", out var rua);

        // Alignment check: SPF must Pass AND domain matches (or subdomain in relaxed mode)
        bool spfAligned = string.Equals(spfResult, "Pass", StringComparison.OrdinalIgnoreCase) &&
                          IsDomainAligned(fromDomain, spfDomain);

        // Alignment check: DKIM must Pass AND domain matches
        bool dkimAligned = string.Equals(dkimResult, "Pass", StringComparison.OrdinalIgnoreCase) &&
                           IsDomainAligned(fromDomain, dkimDomain);

        // DMARC passes if EITHER SPF or DKIM is aligned and passed
        DmarcStatus status = (spfAligned || dkimAligned) ? DmarcStatus.Pass : DmarcStatus.Fail;

        return new DmarcEvaluationResult(
            status,
            policy,
            subdomainPolicy,
            percentage,
            spfAligned,
            dkimAligned,
            rua);
    }

    /// <summary>
    /// Determines enforcement action based on evaluation result, policy, and percentage
    /// </summary>
    public DmarcEnforcementDecision DetermineEnforcement(DmarcEvaluationResult evalResult)
    {
        if (evalResult.Status == DmarcStatus.Pass || evalResult.Status == DmarcStatus.None)
        {
            return new DmarcEnforcementDecision(DmarcEnforcementAction.Accept, "DMARC check passed or no policy published");
        }

        // Apply policy
        return evalResult.Policy switch
        {
            DmarcPolicy.Reject => new DmarcEnforcementDecision(DmarcEnforcementAction.Reject, "DMARC policy reject enforced (SPF/DKIM alignment failed)"),
            DmarcPolicy.Quarantine => new DmarcEnforcementDecision(DmarcEnforcementAction.Quarantine, "DMARC policy quarantine enforced (SPF/DKIM alignment failed)"),
            _ => new DmarcEnforcementDecision(DmarcEnforcementAction.Accept, "DMARC policy is none; email accepted with warning")
        };
    }

    private static bool IsDomainAligned(string fromDomain, string authDomain)
    {
        if (string.IsNullOrWhiteSpace(fromDomain) || string.IsNullOrWhiteSpace(authDomain))
            return false;

        fromDomain = fromDomain.ToLowerInvariant().Trim();
        authDomain = authDomain.ToLowerInvariant().Trim();

        return fromDomain == authDomain || fromDomain.EndsWith("." + authDomain);
    }

    private static Dictionary<string, string> ParseTags(string record)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in record.Split(';'))
        {
            var kv = part.Trim().Split('=', 2);
            if (kv.Length == 2)
            {
                dict[kv[0].Trim()] = kv[1].Trim();
            }
        }
        return dict;
    }
}
