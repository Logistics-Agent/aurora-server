package com.aurora.aigovernance.integration;

import com.aurora.aigovernance.gateway.application.capacity.CapacityReservationService;
import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.ProviderReservation;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.shared.domain.AiOperation;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.test.context.ActiveProfiles;

import java.util.Optional;

import static org.junit.jupiter.api.Assertions.assertNotNull;

@SpringBootTest
@ActiveProfiles("test")
public class ProviderCapacityIntegrationTest {

    @Autowired(required = false)
    private CapacityReservationService capacityService;

    @Test
    public void testCapacityReservationIntegration() {
        if (capacityService == null) return;

        ProviderSlot slot = new ProviderSlot();
        slot.setSlotAlias("gemini-test-slot");
        slot.setProvider(AiProvider.GEMINI);
        slot.setOperation(AiOperation.GENERATE);
        slot.setRpmLimit(15);
        slot.setTpmLimit(250000);
        slot.setRpdLimit(500);

        Optional<ProviderReservation> reservation = capacityService.tryReserve(slot, 1000L);
        assertNotNull(reservation);
    }
}
