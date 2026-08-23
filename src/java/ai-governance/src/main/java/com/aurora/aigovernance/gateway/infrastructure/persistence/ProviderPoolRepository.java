package com.aurora.aigovernance.gateway.infrastructure.persistence;

import com.aurora.aigovernance.gateway.domain.entity.ProviderPool;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.Optional;
import java.util.UUID;

@Repository
public interface ProviderPoolRepository extends JpaRepository<ProviderPool, UUID> {
    Optional<ProviderPool> findByCode(String code);
}
