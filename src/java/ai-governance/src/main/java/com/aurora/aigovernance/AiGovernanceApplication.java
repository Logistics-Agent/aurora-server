package com.aurora.aigovernance;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.cache.annotation.EnableCaching;

/**
 * AiGovernanceService — Centralized AI Control Plane + AI Gateway.
 * <p>
 * Single deployable with three bounded modules:
 * <ul>
 *   <li><b>Governance</b> — Policy Decision Point</li>
 *   <li><b>Gateway</b> — Provider Execution Plane</li>
 *   <li><b>Orchestration</b> — Cross-module use-case coordination</li>
 * </ul>
 */
@SpringBootApplication
@EnableCaching
public class AiGovernanceApplication {

    public static void main(String[] args) {
        SpringApplication.run(AiGovernanceApplication.class, args);
    }
}
