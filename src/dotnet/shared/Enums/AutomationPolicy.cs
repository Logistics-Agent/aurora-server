namespace Shared.Enums;

public enum AutomationPolicy
{
    Manual,            // Không tự động — human quyết định hoàn toàn
    RulesOnly,         // Rules evaluate, không gọi LLM
    RulesAndLlm,       // Rules + LLM reasoning/recommendation
    RulesLlmApproval   // Rules + LLM + Human phải approve trước khi thực thi
}
