package com.aurora.aigovernance.governance.infrastructure.messaging;

import org.springframework.amqp.core.*;
import org.springframework.amqp.rabbit.connection.ConnectionFactory;
import org.springframework.amqp.rabbit.core.RabbitTemplate;
import org.springframework.amqp.support.converter.Jackson2JsonMessageConverter;
import org.springframework.amqp.support.converter.MessageConverter;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
public class RabbitMqConfig {

    public static final String EXCHANGE_AI_GOVERNANCE = "exchange.ai-governance";
    public static final String QUEUE_POLICY_AUDIT = "queue.ai-governance.policy-audit";
    public static final String QUEUE_USAGE_EVENTS = "queue.ai-governance.usage-events";

    public static final String ROUTING_KEY_POLICY_AUDIT = "ai.policy.audit";
    public static final String ROUTING_KEY_USAGE_EVENTS = "ai.usage.recorded";

    @Bean
    public TopicExchange aiGovernanceExchange() {
        return new TopicExchange(EXCHANGE_AI_GOVERNANCE, true, false);
    }

    @Bean
    public Queue policyAuditQueue() {
        return QueueBuilder.durable(QUEUE_POLICY_AUDIT).build();
    }

    @Bean
    public Queue usageEventsQueue() {
        return QueueBuilder.durable(QUEUE_USAGE_EVENTS).build();
    }

    @Bean
    public Binding policyAuditBinding(Queue policyAuditQueue, TopicExchange aiGovernanceExchange) {
        return BindingBuilder.bind(policyAuditQueue).to(aiGovernanceExchange).with(ROUTING_KEY_POLICY_AUDIT);
    }

    @Bean
    public Binding usageEventsBinding(Queue usageEventsQueue, TopicExchange aiGovernanceExchange) {
        return BindingBuilder.bind(usageEventsQueue).to(aiGovernanceExchange).with(ROUTING_KEY_USAGE_EVENTS);
    }

    @Bean
    public MessageConverter messageConverter() {
        return new Jackson2JsonMessageConverter();
    }

    @Bean
    public RabbitTemplate rabbitTemplate(ConnectionFactory connectionFactory, MessageConverter messageConverter) {
        RabbitTemplate template = new RabbitTemplate(connectionFactory);
        template.setMessageConverter(messageConverter);
        return template;
    }
}
