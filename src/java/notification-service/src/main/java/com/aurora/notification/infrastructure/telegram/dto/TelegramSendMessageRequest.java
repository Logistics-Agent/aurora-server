package com.aurora.notification.infrastructure.telegram.dto;

import com.fasterxml.jackson.annotation.JsonProperty;

public class TelegramSendMessageRequest {
    @JsonProperty("chat_id")
    private String chatId;

    @JsonProperty("text")
    private String text;

    @JsonProperty("parse_mode")
    private String parseMode; // "HTML" or "MarkdownV2"

    public TelegramSendMessageRequest() {}

    public TelegramSendMessageRequest(String chatId, String text, String parseMode) {
        this.chatId = chatId;
        this.text = text;
        this.parseMode = parseMode;
    }

    public String getChatId() { return chatId; }
    public void setChatId(String chatId) { this.chatId = chatId; }

    public String getText() { return text; }
    public void setText(String text) { this.text = text; }

    public String getParseMode() { return parseMode; }
    public void setParseMode(String parseMode) { this.parseMode = parseMode; }
}
