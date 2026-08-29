# AI Customer & Operational Assistant — Deep Technical Details

> **Service Layer**: Intent Orchestration, Tool Registry, Sandboxing & Grounding  
> **Source-of-Truth**: `src/nestjs/customer-assistant-service`, `ToolRegistryService`, `ShipmentLookupTool`, `AiGovernanceClient`.

---

## 1. Multi-Turn Orchestration & Intent Flow

```mermaid
sequenceDiagram
    autonumber
    actor User as Customer / Staff
    participant Assist as CustomerAssistant (NestJS)
    participant AiGov as Central AiGovernance (Java)
    participant Shipment as ShipmentWorkflow (.NET 10)

    User->>Assist: "Where is my container CMAU9281729?"
    Assist->>Assist: Loads conversation history from Redis
    Assist->>AiGov: Prompt + Tool Definitions (ShipmentLookup, Billing, Regulatory)
    AiGov-->>Assist: Returns Tool Call: ShipmentLookupTool(containerNo: "CMAU9281729")
    
    Note over Assist,Shipment: Tool Execution Phase (Sandboxed)
    Assist->>Shipment: GetShipmentByTrackingNumber(tenantId, "CMAU9281729") (gRPC)
    Shipment-->>Assist: Status: InTransit, Vessel: Marco Polo, ETA: 2026-08-30
    
    Assist->>AiGov: Synthesize Response (Prompt + Tool Output)
    AiGov-->>Assist: "Your container CMAU9281729 is currently in transit on vessel Marco Polo with an ETA of August 30."
    Assist-->>User: Delivers finalized response via WebSocket / REST
```

---

## 2. Tool Sandboxing & Tenant Security

Every tool implements the `ITool` interface:

```typescript
export interface ITool {
  name: string;
  description: string;
  parametersSchema: JSONSchema;
  execute(args: any, context: ExecutionContext): Promise<ToolResult>;
}
```

### Security Enforcement in `ShipmentLookupTool`:
```typescript
async execute(args: ShipmentLookupArgs, context: ExecutionContext): Promise<ToolResult> {
  // Enforce caller tenantId from authenticated context (never trust LLM args)
  const tenantId = context.tenantId;
  const customerId = context.customerId;

  const shipment = await this.shipmentGrpcClient.getShipmentByTracking({
    tenantId,
    customerId, // Scoped to customer if caller is external customer
    trackingNumber: args.trackingNumber,
  });

  return { success: true, data: shipment };
}
```

---

## 3. Resilience, Timeouts & Error Handling

- **Tool Execution Timeout**: Maximum 4000ms per tool invocation. If a downstream service times out, the assistant receives a typed `TOOL_TIMEOUT` error and gracefully informs the user without hanging the chat session.
- **Max Tool Hop Limit**: Limited to 2 consecutive tool calls per user turn to prevent infinite agentic execution loops.
- **Session Eviction**: Conversation sessions in Redis expire after 2 hours of inactivity.
