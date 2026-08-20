package com.aurora.devopsagent.Application.Commands;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.lang.reflect.Modifier;

import static org.junit.jupiter.api.Assertions.*;

class CommandHandlerStructureTest {

    @Test
    @DisplayName("Verify separate handler and result classes no longer exist in class path")
    void testSeparatedHandlerClassesDoNotExist() {
        assertThrows(ClassNotFoundException.class, () ->
                Class.forName("com.aurora.devopsagent.Application.Commands.CreateRuleCommandHandler")
        );
        assertThrows(ClassNotFoundException.class, () ->
                Class.forName("com.aurora.devopsagent.Application.Commands.IngestAlertCommandHandler")
        );
        assertThrows(ClassNotFoundException.class, () ->
                Class.forName("com.aurora.devopsagent.Application.Commands.IngestAlertResult")
        );
        assertThrows(ClassNotFoundException.class, () ->
                Class.forName("com.aurora.devopsagent.Application.Commands.UpdateSelfConfigCommandHandler")
        );
    }

    @Test
    @DisplayName("Verify Command classes expose nested Handler and Result classes")
    void testCommandClassesExposeNestedHandlers() {
        // IngestAlertCommand
        assertNotNull(IngestAlertCommand.Handler.class);
        assertTrue(Modifier.isPublic(IngestAlertCommand.Handler.class.getModifiers()));
        assertNotNull(IngestAlertCommand.Result.class);

        // CreateRuleCommand
        assertNotNull(CreateRuleCommand.Handler.class);
        assertTrue(Modifier.isPublic(CreateRuleCommand.Handler.class.getModifiers()));

        // UpdateSelfConfigCommand
        assertNotNull(UpdateSelfConfigCommand.Handler.class);
        assertTrue(Modifier.isPublic(UpdateSelfConfigCommand.Handler.class.getModifiers()));
    }
}
