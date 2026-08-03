package com.aurora.devopsagent.Infrastructure.Persistence;

import com.aurora.devopsagent.Domain.Entity.DevOpsAgentSelfConfig;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.UUID;

@Repository
public interface SelfConfigJpaRepository extends JpaRepository<DevOpsAgentSelfConfig, UUID> {
}
