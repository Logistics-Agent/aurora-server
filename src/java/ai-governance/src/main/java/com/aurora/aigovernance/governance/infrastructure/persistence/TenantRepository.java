package com.aurora.aigovernance.governance.infrastructure.persistence;

import com.aurora.aigovernance.governance.domain.entity.Tenant;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;

import java.util.Optional;
import java.util.UUID;

@Repository
public interface TenantRepository extends JpaRepository<Tenant, UUID> {

    @Query("SELECT t FROM Tenant t " +
           "JOIN FETCH t.plan p " +
           "LEFT JOIN FETCH p.capabilities " +
           "LEFT JOIN FETCH p.quotas " +
           "WHERE t.externalTenantId = :externalTenantId")
    Optional<Tenant> findByExternalTenantIdWithPlan(@Param("externalTenantId") UUID externalTenantId);
}
