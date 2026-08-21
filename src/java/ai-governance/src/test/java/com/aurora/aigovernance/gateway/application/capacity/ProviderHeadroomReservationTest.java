package com.aurora.aigovernance.gateway.application.capacity;

import com.aurora.aigovernance.gateway.application.port.ProviderCapacityPort;
import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.ProviderCapacityLimits;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.verify;

@ExtendWith(MockitoExtension.class)
public class ProviderHeadroomReservationTest {

    @Mock
    private ProviderCapacityPort providerCapacityPort;

    @Test
    public void testReservationPassesEffectiveLimitsToPort() {
        ProviderCapacityLimitPolicy policy = new ProviderCapacityLimitPolicy(0.20);
        CapacityReservationService service = new CapacityReservationService(providerCapacityPort, policy);

        ProviderSlot slot = new ProviderSlot();
        slot.setRpmLimit(15);
        slot.setTpmLimit(250000);
        slot.setRpdLimit(500);

        service.tryReserve(slot, 5000L);

        ArgumentCaptor<ProviderCapacityLimits> limitsCaptor = ArgumentCaptor.forClass(ProviderCapacityLimits.class);
        verify(providerCapacityPort).tryReserve(eq(slot), limitsCaptor.capture(), eq(5000L));

        ProviderCapacityLimits passedLimits = limitsCaptor.getValue();
        assertEquals(12, passedLimits.rpmLimit());
        assertEquals(200000, passedLimits.tpmLimit());
        assertEquals(400, passedLimits.rpdLimit());
    }
}
