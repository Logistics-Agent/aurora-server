package com.aurora.notification.config;

import org.springframework.amqp.core.*;
import org.springframework.amqp.support.converter.Jackson2JsonMessageConverter;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
public class RabbitMQConfig {

    @Value("${app.rabbitmq.queue.devops-alerts:devops.alerts.queue}")
    private String devopsQueueName;

    @Value("${app.rabbitmq.exchange.devops:devops.events.exchange}")
    private String devopsExchangeName;

    @Value("${app.rabbitmq.routing-key.devops:devops.alert.#}")
    private String devopsRoutingKey;

    @Bean
    public Queue devopsAlertsQueue() {
        return QueueBuilder.durable(devopsQueueName).build();
    }

    @Bean
    public TopicExchange devopsEventsExchange() {
        return new TopicExchange(devopsExchangeName);
    }

    @Bean
    public Binding devopsAlertsBinding(Queue devopsAlertsQueue, TopicExchange devopsEventsExchange) {
        return BindingBuilder.bind(devopsAlertsQueue).to(devopsEventsExchange).with(devopsRoutingKey);
    }

    @Bean
    public Jackson2JsonMessageConverter jackson2JsonMessageConverter() {
        return new Jackson2JsonMessageConverter();
    }
}
