package com.aurora.devopsagent.Infrastructure.Persistence;

import com.aurora.devopsagent.Domain.Entity.ServiceCriticalityRegistry;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.Optional;
import java.util.UUID;

@Repository
public interface ServiceCriticalityRegistryJpaRepository extends JpaRepository<ServiceCriticalityRegistry, UUID> {
    Optional<ServiceCriticalityRegistry> findByServiceName(String serviceName);
}
