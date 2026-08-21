package com.aurora.aigovernance.gateway.application.capacity;

import com.aurora.aigovernance.gateway.application.port.ProviderCapacityPort;
import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.ProviderCapacityLimits;
import com.aurora.aigovernance.gateway.domain.valueobject.ProviderReservation;
import com.aurora.aigovernance.gateway.domain.valueobject.SlotCapacity;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.shared.domain.AiOperation;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.util.Optional;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
public class CapacityReservationServiceTest {

    @Mock
    private ProviderCapacityPort providerCapacityPort;

    private ProviderCapacityLimitPolicy capacityLimitPolicy;
    private CapacityReservationService capacityReservationService;

    @BeforeEach
    public void setup() {
        // 20% headroom
        capacityLimitPolicy = new ProviderCapacityLimitPolicy(0.20);
        capacityReservationService = new CapacityReservationService(providerCapacityPort, capacityLimitPolicy);
    }

    @Test
    public void testEffectiveLimitHeadroomDeduction() {
        ProviderSlot slot = new ProviderSlot();
        slot.setRpmLimit(15);
        slot.setTpmLimit(250000);
        slot.setRpdLimit(500);

        ProviderCapacityLimits effective = capacityLimitPolicy.effectiveLimits(slot);

        // 15 * 0.8 = 12
        assertEquals(12, effective.rpmLimit());
        // 250000 * 0.8 = 200000
        assertEquals(200000, effective.tpmLimit());
        // 500 * 0.8 = 400
        assertEquals(400, effective.rpdLimit());
    }

    @Test
    public void testTryReserve_CallsPortWithEffectiveLimits() {
        ProviderSlot slot = new ProviderSlot();
        slot.setSlotAlias("slot-1");
        slot.setProvider(AiProvider.GEMINI);
        slot.setOperation(AiOperation.GENERATE);
        slot.setRpmLimit(15);
        slot.setTpmLimit(250000);
        slot.setRpdLimit(500);

        ProviderReservation reservation = new ProviderReservation("res-1", slot, 1000, "rpm", "tpm", "rpd");
        when(providerCapacityPort.tryReserve(eq(slot), any(ProviderCapacityLimits.class), eq(1000L)))
                .thenReturn(Optional.of(reservation));

        Optional<ProviderReservation> result = capacityReservationService.tryReserve(slot, 1000L);

        assertTrue(result.isPresent());
        assertEquals("res-1", result.get().reservationId());
    }

    @Test
    public void testReconcileAndRelease_DelegatesToPort() {
        ProviderSlot slot = new ProviderSlot();
        ProviderReservation reservation = new ProviderReservation("res-1", slot, 1000, "rpm", "tpm", "rpd");

        capacityReservationService.reconcile(reservation, 800L);
        verify(providerCapacityPort).reconcile(reservation, 800L);

        capacityReservationService.release(reservation);
        verify(providerCapacityPort).release(reservation);
    }
}
