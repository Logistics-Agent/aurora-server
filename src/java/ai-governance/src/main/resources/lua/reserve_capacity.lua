-- reserve_capacity.lua
-- Atomic capacity reservation across RPM, TPM, and RPD with effective limits.
-- KEYS[1] = rpmKey
-- KEYS[2] = tpmKey
-- KEYS[3] = rpdKey
-- ARGV[1] = effectiveRpmLimit
-- ARGV[2] = effectiveTpmLimit
-- ARGV[3] = effectiveRpdLimit
-- ARGV[4] = requestedTokens
-- ARGV[5] = rpmTtlSeconds
-- ARGV[6] = tpmTtlSeconds
-- ARGV[7] = rpdTtlSeconds

local rpmKey = KEYS[1]
local tpmKey = KEYS[2]
local rpdKey = KEYS[3]

local rpmLimit = tonumber(ARGV[1])
local tpmLimit = tonumber(ARGV[2])
local rpdLimit = tonumber(ARGV[3])
local requestedTokens = tonumber(ARGV[4])

local rpmTtl = tonumber(ARGV[5])
local tpmTtl = tonumber(ARGV[6])
local rpdTtl = tonumber(ARGV[7])

-- 1. Read current values
local currentRpm = tonumber(redis.call('GET', rpmKey) or '0')
local currentTpm = tonumber(redis.call('GET', tpmKey) or '0')
local currentRpd = tonumber(redis.call('GET', rpdKey) or '0')

-- 2. Check limits against effective limits
if (currentRpm + 1) > rpmLimit then
    return 0
end

if (currentTpm + requestedTokens) > tpmLimit then
    return 0
end

if (currentRpd + 1) > rpdLimit then
    return 0
end

-- 3. Atomic increment
local newRpm = redis.call('INCRBY', rpmKey, 1)
if newRpm == 1 then
    redis.call('EXPIRE', rpmKey, rpmTtl)
end

local newTpm = redis.call('INCRBY', tpmKey, requestedTokens)
if newTpm == requestedTokens then
    redis.call('EXPIRE', tpmKey, tpmTtl)
end

local newRpd = redis.call('INCRBY', rpdKey, 1)
if newRpd == 1 then
    redis.call('EXPIRE', rpdKey, rpdTtl)
end

return 1
