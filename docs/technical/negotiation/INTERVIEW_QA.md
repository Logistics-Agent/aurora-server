# Freight Rate Negotiation Agent — Interview Q&A Guide

> **Target Audience**: Junior, Mid-level, Senior & AI Safety Interviewers  
> **Source-of-Truth**: Grounded 100% in NestJS `negotiation-agent-service` implementation.

---

### Q1 (Junior): Why is the Negotiation Agent implemented with a deterministic formula instead of letting an LLM negotiate freely?
**Answer**:  
Allowing an LLM to freely negotiate financial terms leads to severe hallucination risks (e.g. quoting below-cost freight rates, promising unapproved discounts, or accepting arbitrary currency changes). In Aurora, the **Deterministic Financial Engine** calculates all counter-offers, bottom price thresholds, and decisions using strict TypeScript code. The LLM is used strictly for natural language formulation of the counter-proposal letter.

---

### Q2 (Mid): How does the concession formula work when an offer is below the bottom price?
**Answer**:  
When an offer is below `BottomPrice` and negotiation rounds remain ($R < 3$), the engine calculates:
$$\text{CounterPrice} = \max\left(\text{BottomPrice}, \; \text{OfferPrice} + (\text{ListPrice} - \text{OfferPrice}) \times 0.4\right)$$
This formula concedes 40% of the remaining spread between offer and list price, but ensures the resulting counter-offer never drops below the company's approved floor margin.

---

### Q3 (Mid): Why do VIP and Enterprise customer tiers trigger an automatic `HUMAN_HANDOFF`?
**Answer**:  
High-value accounts require customized commercial terms, volume rebates, or executive relationships. When `customerTier` is `VIP` or `ENTERPRISE`, the engine bypasses automated concession curves and immediately transitions to `HUMAN_HANDOFF` so a designated account manager can handle the relationship personally.

---

### Q4 (Senior): Explain how Human-in-the-Loop draft creation works and why AI does not send emails directly.
**Answer**:  
1. Inbound negotiation events produce a validated `SuggestedReplyDto` stored in the negotiation database.
2. Inbound mail **never** auto-sends outbound replies.
3. Operational staff review the negotiation metrics in the UI and explicitly click `[Create Mail Draft]`.
4. `Staff.Bff` queries `GetDraftSuggestion` via internal gRPC (with zero AI re-computation) and calls `MailService.CreateDraftMessageCommand`.
5. The staff member edits and approves the draft, and only their explicit click on `[Send]` initiates the outbound security pipeline.

---

### Q5 (System Design): What happens if the LLM generation in `AiGovernance` times out during offer submission?
**Answer**:  
The negotiation strategy calculation is completely decoupled from natural language generation. If `AiGovernance` times out or fails, the engine falls back to pre-compiled, template-based response strings (e.g. `"We thank you for your offer of $X. Our counter-offer is $Y."`) and returns the deterministic decision with `fallback_used = true`.
