package com.aurora.aigovernance.gateway.infrastructure.persistence;

import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.shared.domain.AiOperation;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.Set;
import java.util.UUID;

@Repository
public interface ProviderSlotRepository extends JpaRepository<ProviderSlot, UUID> {

    @Query("SELECT s FROM ProviderSlot s " +
           "JOIN FETCH s.pool p " +
           "WHERE p.code IN :poolCodes " +
           "AND s.provider IN :providers " +
           "AND s.operation = :operation " +
           "AND s.enabled = true " +
           "ORDER BY s.priority ASC")
    List<ProviderSlot> findActiveCandidateSlots(
            @Param("poolCodes") Set<String> poolCodes,
            @Param("providers") Set<AiProvider> providers,
            @Param("operation") AiOperation operation
    );
}
