#!/usr/bin/env bash
# ==============================================================================
# Aurora Mail Platform — DNS & Deliverability Pre-Flight Verification
# Gates 6C (NETWORK-READY) & 6D (DOMAIN-READY)
# ==============================================================================
set -euo pipefail

DOMAIN="${1:-aurora.vn}"
MAIL_HOST="${2:-mail.${DOMAIN}}"
SELECTOR="${3:-aurora}"

echo "======================================================================"
echo ">> Running DNS & Deliverability Pre-Flight Check for: ${DOMAIN}"
echo "   Mail Server Hostname: ${MAIL_HOST}"
echo "   DKIM Selector:        ${SELECTOR}"
echo "======================================================================"

PASS_COUNT=0
FAIL_COUNT=0
WARN_COUNT=0

log_pass() { echo -e "[\e[32mPASS\e[0m] $1"; ((PASS_COUNT++)); }
log_fail() { echo -e "[\e[31mFAIL\e[0m] $1"; ((FAIL_COUNT++)); }
log_warn() { echo -e "[\e[33mWARN\e[0m] $1"; ((WARN_COUNT++)); }

# 1. Check Public IP & Port 25 Outbound ISP Egress
echo -e "\n--- 1. Network & Port 25 Egress (Gate 6C) ---"
PUBLIC_IP=$(curl -s --max-time 5 https://api.ipify.org || curl -s --max-time 5 https://checkip.amazonaws.com || echo "UNKNOWN")

if [[ "${PUBLIC_IP}" != "UNKNOWN" ]]; then
    log_pass "Public IPv4 detected: ${PUBLIC_IP}"
else
    log_fail "Could not determine Public IP address."
fi

# Test Port 25 Outbound TCP connectivity (checking if ISP blocks Port 25)
if command -v nc >/dev/null 2>&1; then
    if nc -z -w5 smtp.gmail.com 25 >/dev/null 2>&1; then
        log_pass "Outbound Port 25 is OPEN on ISP network (not blocked)."
    else
        log_warn "Outbound Port 25 TCP connect failed or filtered by ISP. Verify ISP firewall!"
    fi
else
    log_warn "nc (netcat) not installed. Skipping direct port 25 egress check."
fi

# 2. Check Forward A Record
echo -e "\n--- 2. Forward DNS A Record (Gate 6D) ---"
if command -v dig >/dev/null 2>&1; then
    A_RECORD=$(dig +short A "${MAIL_HOST}" | tail -n1)
    if [[ -n "${A_RECORD}" ]]; then
        if [[ "${A_RECORD}" == "${PUBLIC_IP}" ]]; then
            log_pass "A Record: ${MAIL_HOST} -> ${A_RECORD} (matches public IP)"
        else
            log_warn "A Record: ${MAIL_HOST} -> ${A_RECORD} (does NOT match current public IP ${PUBLIC_IP}. NAT/Proxy in place?)"
        fi
    else
        log_fail "A Record for ${MAIL_HOST} not found!"
    fi

    # 3. Check Reverse DNS (PTR Record)
    echo -e "\n--- 3. Reverse DNS (PTR Record) ---"
    if [[ "${PUBLIC_IP}" != "UNKNOWN" ]]; then
        PTR_RECORD=$(dig +short -x "${PUBLIC_IP}" | sed 's/\.$//' | tail -n1)
        if [[ -n "${PTR_RECORD}" ]]; then
            if [[ "${PTR_RECORD}" == "${MAIL_HOST}" ]]; then
                log_pass "PTR Record: ${PUBLIC_IP} -> ${PTR_RECORD} (Forward-Confirmed Reverse DNS match!)"
            else
                log_warn "PTR Record: ${PUBLIC_IP} -> ${PTR_RECORD} (Mismatch! Expected: ${MAIL_HOST}). Crucial for Gmail/Microsoft inbox deliverability."
            fi
        else
            log_fail "No PTR Record found for ${PUBLIC_IP}. Request PTR update from your ISP / Cloud Provider!"
        fi
    fi

    # 4. Check MX Record
    echo -e "\n--- 4. MX Record ---"
    MX_RECORDS=$(dig +short MX "${DOMAIN}")
    if [[ -n "${MX_RECORDS}" ]]; then
        log_pass "MX Records for ${DOMAIN}:\n${MX_RECORDS}"
        if echo "${MX_RECORDS}" | grep -q "${MAIL_HOST}"; then
            log_pass "MX contains target mail server: ${MAIL_HOST}"
        else
            log_warn "MX does not explicitly list ${MAIL_HOST}. Ensure routing is correct."
        fi
    else
        log_fail "No MX Record found for domain ${DOMAIN}!"
    fi

    # 5. Check SPF TXT Record
    echo -e "\n--- 5. SPF TXT Record (RFC 7208) ---"
    SPF_RECORD=$(dig +short TXT "${DOMAIN}" | grep -i "v=spf1" || true)
    if [[ -n "${SPF_RECORD}" ]]; then
        log_pass "SPF Record found: ${SPF_RECORD}"
        if echo "${SPF_RECORD}" | grep -Eq "\-all|~all"; then
            log_pass "SPF enforces strict fail policy (-all or ~all)."
        else
            log_warn "SPF policy is weak or permissive (?all / +all). Recommend using '-all'."
        fi
    else
        log_fail "No SPF Record (v=spf1) found for ${DOMAIN}!"
    fi

    # 6. Check DKIM TXT Record
    echo -e "\n--- 6. DKIM TXT Record (RFC 6376) ---"
    DKIM_HOST="${SELECTOR}._domainkey.${DOMAIN}"
    DKIM_RECORD=$(dig +short TXT "${DKIM_HOST}" || true)
    if [[ -n "${DKIM_RECORD}" ]]; then
        log_pass "DKIM Record found at ${DKIM_HOST}:\n${DKIM_RECORD}"
    else
        log_fail "No DKIM Record found at ${DKIM_HOST}! Public key must be published for selector '${SELECTOR}'."
    fi

    # 7. Check DMARC TXT Record
    echo -e "\n--- 7. DMARC TXT Record (RFC 7489) ---"
    DMARC_HOST="_dmarc.${DOMAIN}"
    DMARC_RECORD=$(dig +short TXT "${DMARC_HOST}" || true)
    if [[ -n "${DMARC_RECORD}" ]]; then
        log_pass "DMARC Record found at ${DMARC_HOST}:\n${DMARC_RECORD}"
        if echo "${DMARC_RECORD}" | grep -Eq "p=quarantine|p=reject"; then
            log_pass "DMARC policy is protective (quarantine or reject)."
        else
            log_warn "DMARC policy is in monitoring mode (p=none). Upgrade to 'quarantine' or 'reject' once aligned."
        fi
    else
        log_fail "No DMARC Record (v=DMARC1) found at ${DMARC_HOST}!"
    fi
else
    log_warn "dig tool is not installed. Install dnsutils/bind-utils to run comprehensive DNS checks."
fi

echo "======================================================================"
echo ">> Verification Summary: ${PASS_COUNT} Passed, ${WARN_COUNT} Warnings, ${FAIL_COUNT} Failed."
if [[ ${FAIL_COUNT} -eq 0 ]]; then
    echo -e "[\e[32mSUCCESS\e[0m] Domain & Network Deliverability Prerequisites are SATISFIED."
else
    echo -e "[\e[31mACTION REQUIRED\e[0m] Resolve the failed items above to ensure Gmail/Microsoft delivery."
fi
echo "======================================================================"
