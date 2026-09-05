package com.aurora.devopsagent.Infrastructure.Persistence;

import java.util.UUID;

import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import com.aurora.devopsagent.Domain.Entity.ExistingRule;

@Repository
public interface ExistingRuleJpaRepository extends JpaRepository<ExistingRule, UUID> {
}
