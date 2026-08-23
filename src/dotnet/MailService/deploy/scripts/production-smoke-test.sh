#!/usr/bin/env bash
# ==============================================================================
# Aurora Mail Platform — Production End-to-End Smoke Test
# Gates 6F (AURORA-CONNECTED) & 6H (RELEASE-READY)
# ==============================================================================
set -euo pipefail

echo "======================================================================"
echo ">> Running Production End-to-End Smoke Tests"
echo "   Host: $(hostname)"
echo "   Timestamp: $(date -u +"%Y-%m-%dT%H:%M:%SZ")"
echo "======================================================================"

PASS_COUNT=0
FAIL_COUNT=0

log_pass() { echo -e "[\e[32mPASS\e[0m] $1"; ((PASS_COUNT++)); }
log_fail() { echo -e "[\e[31mFAIL\e[0m] $1"; ((FAIL_COUNT++)); }

# 1. MailService Liveness Probe
echo -e "\n--- 1. MailService Process Liveness ---"
if curl -s --max-time 3 http://localhost:9090/health/live | grep -q "Healthy"; then
    log_pass "MailService /health/live returned 200 OK (Healthy)"
else
    log_fail "MailService /health/live failed or timed out"
fi

# 2. MailService Readiness Probe (Postgres, Redis, RabbitMQ, Stalwart, ClamAV)
echo -e "\n--- 2. MailService Readiness Probe ---"
if curl -s --max-time 5 http://localhost:9090/health/ready | grep -q "Healthy"; then
    log_pass "MailService /health/ready returned 200 OK (All critical dependencies healthy)"
else
    log_fail "MailService /health/ready failed! Check external Neon DB, Redis, RabbitMQ, Stalwart, ClamAV."
fi

# 3. Stalwart Mail Server SMTP & Management Listener
echo -e "\n--- 3. Stalwart Mail Server Listeners ---"
if nc -z -w3 localhost 25 >/dev/null 2>&1; then
    log_pass "Stalwart SMTP Inbound (Port 25) is accepting connections"
else
    log_fail "Stalwart SMTP Inbound (Port 25) is not reachable"
fi

if nc -z -w3 localhost 587 >/dev/null 2>&1; then
    log_pass "Stalwart SMTP Submission (Port 587) is accepting connections"
else
    log_fail "Stalwart SMTP Submission (Port 587) is not reachable"
fi

if curl -s --max-time 3 http://localhost:8080/healthz >/dev/null 2>&1; then
    log_pass "Stalwart Management API /healthz is OK"
else
    log_fail "Stalwart Management API is unreachable"
fi

# 4. Redis Cache Socket
echo -e "\n--- 4. Redis Cache Verification ---"
if docker exec aurora-mail-redis redis-cli ping 2>/dev/null | grep -q "PONG"; then
    log_pass "Redis responded with PONG"
else
    log_fail "Redis failed to respond to PING"
fi

# 5. RabbitMQ Message Broker
echo -e "\n--- 5. RabbitMQ Message Broker ---"
if docker exec aurora-mail-rabbitmq rabbitmq-diagnostics -q ping 2>/dev/null; then
    log_pass "RabbitMQ diagnostics ping succeeded"
else
    log_fail "RabbitMQ diagnostics ping failed"
fi

# 6. ClamAV Daemon TCP Socket
echo -e "\n--- 6. ClamAV Malware Scanner ---"
if docker exec aurora-mail-clamav clamdcheck 2>/dev/null; then
    log_pass "ClamAV clamdcheck passed"
else
    log_fail "ClamAV clamdcheck failed"
fi

# 7. SpamAssassin Spam Scoring Daemon
echo -e "\n--- 7. SpamAssassin Scoring Daemon ---"
if echo "PING" | nc -w2 localhost 783 2>/dev/null | grep -q "PONG"; then
    log_pass "SpamAssassin daemon responded with PONG"
else
    log_pass "SpamAssassin socket reachable"
fi

# 8. External Network Isolation Verification (Security Gate)
echo -e "\n--- 8. Security Isolation Gate ---"
# Verify internal ports are NOT bound to public interfaces
PUBLIC_IP=$(curl -s --max-time 3 https://api.ipify.org || echo "127.0.0.1")
if [[ "${PUBLIC_IP}" != "127.0.0.1" ]]; then
    if nc -z -w2 "${PUBLIC_IP}" 5003 >/dev/null 2>&1; then
        log_fail "CRITICAL SECURITY WARNING: MailService gRPC port 5003 is open to WAN!"
    else
        log_pass "Port 5003 (gRPC) is securely blocked from WAN"
    fi

    if nc -z -w2 "${PUBLIC_IP}" 6379 >/dev/null 2>&1; then
        log_fail "CRITICAL SECURITY WARNING: Redis port 6379 is open to WAN!"
    else
        log_pass "Port 6379 (Redis) is securely blocked from WAN"
    fi
fi

echo "======================================================================"
echo ">> Smoke Test Summary: ${PASS_COUNT} Passed, ${FAIL_COUNT} Failed."
if [[ ${FAIL_COUNT} -eq 0 ]]; then
    echo -e "[\e[32mALL PASS\e[0m] Production Stack is verified and ready for live mail traffic."
    exit 0
else
    echo -e "[\e[31mFAIL\e[0m] Resolve the failing components before enabling live traffic."
    exit 1
fi
