# Aurora Mail Platform — DNS & Deliverability Prerequisites Guide

This guide details the mandatory external DNS records, Reverse DNS (PTR), TLS certificates, and ISP prerequisites required to achieve 10/10 deliverability on Gmail, Outlook/Office365, and Yahoo.

---

## 1. Summary of Required Public DNS Records

Assuming base domain: `aurora.vn` and mail server hostname: `mail.aurora.vn` on Static Public IP `203.0.113.10`.

| Type | Host / Name | Target / Value | TTL | Purpose |
|---|---|---|---|---|
| **A** | `mail.aurora.vn` | `203.0.113.10` | 300 | Forward resolution for mail server |
| **MX** | `aurora.vn` | `10 mail.aurora.vn` | 3600 | Inbound mail routing |
| **TXT** | `aurora.vn` | `v=spf1 mx ip4:203.0.113.10 -all` | 3600 | Sender Policy Framework (RFC 7208) |
| **TXT** | `aurora._domainkey.aurora.vn` | `v=DKIM1; k=rsa; p=MIIBIjANBgkqhkiG9w0...` | 3600 | DKIM Public Key (RFC 6376) |
| **TXT** | `_dmarc.aurora.vn` | `v=DMARC1; p=quarantine; rua=mailto:dmarc-reports@aurora.vn; pct=100` | 3600 | DMARC Policy (RFC 7489) |
| **TXT** | `_mta-sts.aurora.vn` | `v=STSv1; id=20260822T100000;` | 86400 | MTA-STS Mode (RFC 8461) |
| **CNAME** | `mta-sts.aurora.vn` | `mail.aurora.vn` | 86400 | MTA-STS Policy host |
| **TXT** | `_smtp._tls.aurora.vn` | `v=TLSRPTv1; rua=mailto:tls-reports@aurora.vn` | 86400 | SMTP TLS Reporting (RFC 8460) |
| **PTR** | `10.113.0.203.in-addr.arpa` | `mail.aurora.vn.` | 3600 | **Reverse DNS (Set at ISP / Hosting level)** |

---

## 2. Reverse DNS (PTR) — The #1 Deliverability Prerequisite

### Why PTR is Mandatory
Both Google and Microsoft reject or quarantine all inbound SMTP connections from IP addresses that fail **Forward-Confirmed Reverse DNS (FCrDNS)**:
1. `mail.aurora.vn` resolves to `203.0.113.10`.
2. Reverse DNS lookup for `203.0.113.10` MUST return `mail.aurora.vn`.
3. Stalwart HELO/EHLO greeting banner MUST be `mail.aurora.vn`.

### How to Configure PTR
- PTR records **cannot** be set in Cloudflare or Namecheap (unless they own your IP block).
- You must contact your Internet Service Provider (ISP) or Static IP datacenter provider and request:
  > *"Please set the rDNS / PTR record for static IP `203.0.113.10` to `mail.aurora.vn`."*

---

## 3. ISP Port 25 Inbound / Outbound Check & CGNAT

- Standard residential internet connections often use Carrier-Grade NAT (CGNAT) and block inbound and outbound TCP port 25.
- For business production use:
  1. Ensure your Mini PC has a **Dedicated Static Public IPv4**.
  2. Confirm with your ISP that **Inbound & Outbound Port 25 is UNBLOCKED**.

---

## 4. DKIM Key Generation in Stalwart

1. Log into Stalwart Web Admin or CLI:
   ```bash
   stalwart-cli -u http://127.0.0.1:8080 -c <ADMIN_TOKEN> domain create aurora.vn
   ```
2. Generate an RSA 2048-bit DKIM Key for selector `aurora`:
   ```bash
   stalwart-cli -u http://127.0.0.1:8080 -c <ADMIN_TOKEN> dkim create aurora.vn aurora rsa-sha256 2048
   ```
3. Copy the output public key string into your DNS TXT record for `aurora._domainkey.aurora.vn`.

---

## 5. DMARC Rollout Strategy

1. **Phase 1: Monitoring Mode (Week 1–2)**:
   ```text
   v=DMARC1; p=none; sp=none; rua=mailto:dmarc-reports@aurora.vn; pct=100
   ```
2. **Phase 2: Quarantine Mode (Week 3+)**:
   ```text
   v=DMARC1; p=quarantine; sp=quarantine; rua=mailto:dmarc-reports@aurora.vn; pct=100
   ```
3. **Phase 3: Strict Reject (Final Hardened State)**:
   ```text
   v=DMARC1; p=reject; sp=reject; rua=mailto:dmarc-reports@aurora.vn; pct=100
   ```
