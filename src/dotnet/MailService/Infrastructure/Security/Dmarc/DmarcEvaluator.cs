namespace MailService.Infrastructure.Security.Dmarc;

public class DmarcEvaluator
{
    public (string Result, string Policy) Evaluate(string spfResult, string dkimResult, string? dmarcRecord)
    {
        if (string.IsNullOrWhiteSpace(dmarcRecord))
        {
            return ("None", "none");
        }

        string policy = "none";
        foreach (var tag in dmarcRecord.Split(';'))
        {
            var kv = tag.Trim().Split('=', 2);
            if (kv.Length == 2 && kv[0].Trim().Equals("p", StringComparison.OrdinalIgnoreCase))
            {
                string p = kv[1].Trim().ToLowerInvariant();
                if (p == "reject") policy = "reject";
                else if (p == "quarantine") policy = "quarantine";
                else policy = "none";
                break;
            }
        }

        bool aligned = spfResult == "Pass" || dkimResult == "Pass";
        string result = aligned ? "Pass" : "Fail";

        return (result, policy);
    }
}
