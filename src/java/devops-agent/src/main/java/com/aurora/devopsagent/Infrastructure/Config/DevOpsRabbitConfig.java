package com.aurora.devopsagent.Infrastructure.Config;

import org.springframework.amqp.core.TopicExchange;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
public class DevOpsRabbitConfig {

    @Bean
    public TopicExchange devOpsAuditEventsExchange() {
        return new TopicExchange("devops.audit.events", true, false);
    }

    @Bean
    public TopicExchange devOpsNotificationEventsExchange() {
        return new TopicExchange("devops.events.exchange", true, false);
    }
}
