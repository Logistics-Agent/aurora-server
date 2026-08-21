package com.aurora.aigovernance.gateway.infrastructure.persistence;

import com.aurora.aigovernance.gateway.domain.entity.ServiceProviderPoolPolicy;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.UUID;

@Repository
public interface ServiceProviderPoolPolicyRepository extends JpaRepository<ServiceProviderPoolPolicy, UUID> {

    @Query("SELECT p FROM ServiceProviderPoolPolicy p " +
           "JOIN FETCH p.pool " +
           "WHERE p.serviceId = :serviceId " +
           "ORDER BY p.priority ASC")
    List<ServiceProviderPoolPolicy> findByServiceIdOrderByPriorityAsc(@Param("serviceId") String serviceId);
}
