package com.aurora.devopsagent.Infrastructure.Persistence;

import com.aurora.devopsagent.Domain.Entity.LlmApiKeyPool;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.UUID;

@Repository
public interface LlmApiKeyPoolJpaRepository extends JpaRepository<LlmApiKeyPool, UUID> {
    @Query("SELECT k FROM LlmApiKeyPool k WHERE k.provider = :provider AND k.isActive = true AND (k.cooldownUntil IS NULL OR k.cooldownUntil <= CURRENT_TIMESTAMP) ORDER BY k.priority ASC")
    List<LlmApiKeyPool> findAvailableKeysForProvider(@Param("provider") String provider);
}
