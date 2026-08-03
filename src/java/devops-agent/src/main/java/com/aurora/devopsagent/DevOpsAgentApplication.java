package com.aurora.devopsagent;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.boot.autoconfigure.domain.EntityScan;
import org.springframework.data.jpa.repository.config.EnableJpaRepositories;

@SpringBootApplication(scanBasePackages = {"com.aurora.devopsagent", "com.aurora.shared"})
@EntityScan(basePackages = {"com.aurora.devopsagent.Domain.Entity", "com.aurora.shared.entity"})
@EnableJpaRepositories(basePackages = {"com.aurora.devopsagent.Infrastructure.Persistence"})
public class DevOpsAgentApplication {

    public static void main(String[] args) {
        SpringApplication.run(DevOpsAgentApplication.class, args);
    }
}
