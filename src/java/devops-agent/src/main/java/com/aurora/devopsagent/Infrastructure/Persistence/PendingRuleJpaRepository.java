package com.aurora.devopsagent.Infrastructure.Persistence;

import com.aurora.devopsagent.Domain.Entity.PendingRule;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.UUID;

@Repository
public interface PendingRuleJpaRepository extends JpaRepository<PendingRule, UUID> {
    Page<PendingRule> findByStatus(String status, Pageable pageable);
}
