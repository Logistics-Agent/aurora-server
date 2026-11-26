package credential

import (
	"errors"
	"log/slog"
	"os"
	"strings"
	"sync"
	"time"
)

type CredentialPort interface {
	ResolveSecret(secretRef string) (string, error)
	Evict(secretRef string)
}

type cachedSecret struct {
	value    string
	cachedAt time.Time
}

func (c cachedSecret) isExpired(ttl time.Duration) bool {
	return time.Now().UTC().After(c.cachedAt.Add(ttl))
}

type KeyVaultCredentialResolver struct {
	keyVaultURL string
	cacheTTL    time.Duration
	mu          sync.RWMutex
	cache       map[string]cachedSecret
	envLookup   func(string) string
	logger      *slog.Logger
}

func NewKeyVaultCredentialResolver(keyVaultURL string, cacheTTL time.Duration, logger *slog.Logger) *KeyVaultCredentialResolver {
	if logger == nil {
		logger = slog.Default()
	}
	if cacheTTL <= 0 {
		cacheTTL = 300 * time.Second
	}
	return &KeyVaultCredentialResolver{
		keyVaultURL: keyVaultURL,
		cacheTTL:    cacheTTL,
		cache:       make(map[string]cachedSecret),
		envLookup:   os.Getenv,
		logger:      logger,
	}
}

func (r *KeyVaultCredentialResolver) ResolveSecret(secretRef string) (string, error) {
	if strings.TrimSpace(secretRef) == "" {
		return "", errors.New("secretRef cannot be null or empty")
	}

	r.mu.RLock()
	cached, ok := r.cache[secretRef]
	r.mu.RUnlock()

	if ok && !cached.isExpired(r.cacheTTL) {
		return cached.value, nil
	}

	// Check environment variable (e.g. gemini-api-key-01 -> GEMINI_API_KEY_01)
	envKey := strings.ToUpper(strings.ReplaceAll(secretRef, "-", "_"))
	if val := strings.TrimSpace(r.envLookup(envKey)); val != "" {
		r.mu.Lock()
		r.cache[secretRef] = cachedSecret{value: val, cachedAt: time.Now().UTC()}
		r.mu.Unlock()
		return val, nil
	}

	// Serve expired cache fallback if present
	if ok {
		r.logger.Warn("Credential lookup unavailable, serving expired cache fallback", slog.String("secretRef", secretRef))
		return cached.value, nil
	}

	// Demo / local fallback matching Java KeyVaultCredentialResolver
	return "demo-secret-" + secretRef, nil
}

func (r *KeyVaultCredentialResolver) Evict(secretRef string) {
	r.mu.Lock()
	delete(r.cache, secretRef)
	r.mu.Unlock()
	r.logger.Info("Evicted secret from cache", slog.String("secretRef", secretRef))
}
