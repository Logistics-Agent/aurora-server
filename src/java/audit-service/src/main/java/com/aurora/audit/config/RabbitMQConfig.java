package com.aurora.audit.config;

import org.springframework.amqp.core.*;
import org.springframework.amqp.support.converter.Jackson2JsonMessageConverter;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
public class RabbitMQConfig {

    @Value("${app.rabbitmq.queue.audit-events:aurora.audit.events.queue}")
    private String auditQueueName;

    @Value("${app.rabbitmq.exchange.audit:aurora.audit.events.exchange}")
    private String auditExchangeName;

    @Value("${app.rabbitmq.routing-key.audit:aurora.event.#}")
    private String auditRoutingKey;

    @Bean
    public Queue auditEventsQueue() {
        return QueueBuilder.durable(auditQueueName).build();
    }

    @Bean
    public TopicExchange auditEventsExchange() {
        return new TopicExchange(auditExchangeName);
    }

    @Bean
    public Binding auditEventsBinding(Queue auditEventsQueue, TopicExchange auditEventsExchange) {
        return BindingBuilder.bind(auditEventsQueue).to(auditEventsExchange).with(auditRoutingKey);
    }

    @Bean
    public Jackson2JsonMessageConverter jackson2JsonMessageConverter() {
        return new Jackson2JsonMessageConverter();
    }
}
