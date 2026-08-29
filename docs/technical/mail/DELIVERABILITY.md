# Aurora Mail Platform — Email Deliverability & Domain Reputation

> **Status**: AUTHORITATIVE / PRODUCTION GUIDE (CODE-FIRST)  
> **Source-of-Truth**: Audited against `deploy/scripts/verify-dns-deliverability.sh`, `deploy/DELIVERABILITY_GUIDE.md`, `StalwartManagementClient.cs`, and `Domain.cs`.

---

## 1. Executive Summary & Deliverability Triangle

High inbox placement for business-critical logistics emails (booking confirmations, customs notices, POD invoices) requires flawless DNS authentication across the **Deliverability Triangle**:

```
                  ┌──────────────────────┐
                  │      DMARC Policy    │
                  │ (p=quarantine/reject)│
                  └──────────┬───────────┘
                             │
              Aligned With   │   Aligned With
         ┌───────────────────┴───────────────────┐
         ▼                                       ▼
┌──────────────────┐                   ┌──────────────────┐
│    SPF Record    │                   │   DKIM Signature │
│ (v=spf1 ... ~all)│                   │ (RSA-2048 Keys)  │
└──────────────────┘                   └──────────────────┘
```

---

## 2. Mandatory DNS Record Specifications

For each tenant domain (e.g. `acmelogistics.com`) and the primary platform host (`mail.aurora-logistics.com`), configure the following DNS records:

### 2.1 MX (Mail Exchange) Record
```dns
@               IN  MX  10  mail.aurora-logistics.com.
```
- Directs inbound internet email traffic to the Aurora Stalwart mail server.

### 2.2 SPF (Sender Policy Framework) TXT Record
```dns
@               IN  TXT "v=spf1 ip4:203.0.113.50 include:relay.aurora-logistics.com ~all"
```
- Authorizes the dedicated Aurora Mail Server public IP (`203.0.113.50`) and relay host.
- Uses `~all` (SoftFail) during initial rollout; transition to `-all` (Fail) after verification.

### 2.3 DKIM (DomainKeys Identified Mail) TXT Record
```dns
aurora-2025._domainkey.acmelogistics.com. IN TXT "v=DKIM1; k=rsa; p=MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA0GZ6P..."
```
- Selector: `aurora-2025` (Generated automatically by Stalwart during domain provisioning).
- Key Length: **RSA 2048-bit**.

### 2.4 DMARC (Domain-based Message Authentication) TXT Record
```dns
_dmarc.acmelogistics.com. IN TXT "v=DMARC1; p=quarantine; pct=100; rua=mailto:dmarc-reports@acmelogistics.com; ruf=mailto:dmarc-forensics@acmelogistics.com; fo=1"
```
- **Policy progression**: `p=none` (Monitoring, 14 days) $\rightarrow$ `p=quarantine` (Production default) $\rightarrow$ `p=reject` (Strict security).

### 2.5 Reverse DNS (PTR) Record
```dns
50.113.0.203.in-addr.arpa. IN PTR mail.aurora-logistics.com.
```
- Must resolve the server's public IP to the exact hostname advertised in SMTP `HELO/EHLO` banners.

---

## 3. Automated Deliverability Verification (`verify-dns-deliverability.sh`)

Aurora provides an automated pre-flight deliverability verifier script:
```bash
sudo bash /opt/aurora/mail/scripts/verify-dns-deliverability.sh acmelogistics.com
```

### Checks Executed:
1. **MX Record Resolution**: Verifies MX host points to Aurora server IP.
2. **SPF Syntax & IP Match**: Validates SPF string format and tests IP inclusion.
3. **DKIM Public Key Retrieval**: Queries `<selector>._domainkey.<domain>` and validates RSA public key format.
4. **DMARC Policy Check**: Ensures DMARC record exists with valid `p=` and `rua=` tags.
5. **Reverse DNS (PTR) Validation**: Verifies PTR matches forward A record.
6. **DNSBL (Blacklist) Inspection**: Queries top real-time IP blacklists (Spamhaus ZEN, Barracuda, SpamCop, SORBS).

---

## 4. IP Warm-up Schedule for New Deployments

When launching on a new dedicated IP address, follow this phased sending volume ramp-up to establish high reputation with major ISPs (Google, Microsoft 365, Yahoo):

| Days | Max Volume / Day | Recommended Traffic |
|---|---|---|
| **Days 1–3** | 200 emails / day | Direct high-engagement operational emails (Booking confirmations to known partners). |
| **Days 4–7** | 500 emails / day | Operational freight updates and customs clearance receipts. |
| **Days 8–14**| 1,500 emails / day| Inbound quotation responses and tracking notifications. |
| **Day 15+**  | Full Production Limit (5,000+ / day) | Standard automated operations and freight updates. |

---

## 5. Bounce Handling & Reputation Safeguards

1. **Hard Bounces (5xx User Unknown)**: Automatically logged to `AuditRecords`. The address is suppressed from future automated notifications.
2. **Spam Complaints**: Evaluated via FBL (Feedback Loops) where supported.
3. **Outbound Rate Limiting**: `Domain.OutboundRateLimitPerHour` (Default 200/hr per tenant) prevents runaway compromised accounts from degrading the IP's sender reputation.
