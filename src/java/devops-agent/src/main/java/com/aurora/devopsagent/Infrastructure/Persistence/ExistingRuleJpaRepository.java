package com.aurora.devopsagent.Infrastructure.Persistence;

import com.aurora.devopsagent.Domain.Entity.ExistingRule;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.UUID;

@Repository
public interface ExistingRuleJpaRepository extends JpaRepository<ExistingRule, UUID> {
    List<ExistingRule> findByTargetService(String targetService);
}
