package com.aurora.aigovernance.gateway.application.execution;

import com.aurora.aigovernance.gateway.application.capacity.CapacityReservationService;
import com.aurora.aigovernance.gateway.application.port.AiProviderClient;
import com.aurora.aigovernance.gateway.application.routing.ProviderRoutingService;
import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.*;
import com.aurora.aigovernance.gateway.infrastructure.persistence.ProviderSlotRepository;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.governance.domain.valueobject.GovernanceDecision;
import com.aurora.aigovernance.shared.domain.AiOperation;
import com.aurora.aigovernance.shared.domain.TokenBudget;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

import java.time.OffsetDateTime;
import java.util.List;
import java.util.Map;
import java.util.Optional;

@Service
public class AiExecutionService {

    private static final Logger log = LoggerFactory.getLogger(AiExecutionService.class);

    private final ProviderRoutingService routingService;
    private final CapacityReservationService capacityService;
    private final Map<String, AiProviderClient> providerClients;
    private final ProviderSlotRepository slotRepository;

    public AiExecutionService(
            ProviderRoutingService routingService,
            CapacityReservationService capacityService,
            Map<String, AiProviderClient> providerClients,
            ProviderSlotRepository slotRepository) {
        this.routingService = routingService;
        this.capacityService = capacityService;
        this.providerClients = providerClients;
        this.slotRepository = slotRepository;
    }

    /**
     * Governed Generation execution through candidate loop.
     */
    public AiGenerateResult generate(
            GovernanceDecision decision,
            AiGenerateRequest request,
            String callerServiceId,
            TokenBudget tokenBudget) {

        List<ProviderSlot> candidates = routingService.getCandidates(
                decision.allowedProviders(),
                decision.allowedProviderPools(),
                AiOperation.GENERATE,
                callerServiceId,
                tokenBudget
        );

        if (candidates.isEmpty()) {
            log.error("PROVIDER_CAPACITY_EXHAUSTED: No available candidates for serviceId={}", callerServiceId);
            throw new IllegalStateException("PROVIDER_CAPACITY_EXHAUSTED: No available provider candidates found.");
        }

        Exception lastException = null;

        for (ProviderSlot candidate : candidates) {
            // 1. Atomic Redis capacity reservation
            Optional<ProviderReservation> reservationOpt = capacityService.tryReserve(
                    candidate,
                    tokenBudget.reservationTokens()
            );

            if (reservationOpt.isEmpty()) {
                log.info("AI_CAPACITY_RESERVATION_FAILED: Contention on slot {}, advancing to next candidate",
                        candidate.getSlotAlias());
                continue;
            }

            ProviderReservation reservation = reservationOpt.get();
            AiProviderClient client = resolveProviderClient(candidate.getProvider());

            // 2. Provider execution
            try {
                AiGenerateResult result = client.generate(candidate, request);

                // 3. Reconcile usage
                long actualTokens = result.inputTokens() + result.outputTokens();
                capacityService.reconcile(reservation, actualTokens);

                log.info("AI_EXECUTION_COMPLETED: slot={}, actualTokens={}",
                        candidate.getSlotAlias(), actualTokens);
                return result;

            } catch (Exception e) {
                lastException = e;
                log.warn("AI_PROVIDER_FAILED: slot={}, error={}", candidate.getSlotAlias(), e.getMessage());

                // Failure Semantics:
                // 1. Definite non-consumption (e.g. 429 rate limit or pre-send validation) -> release reservation
                // 2. Ambiguous timeout / 5xx where request may have been processed -> UNCERTAIN, do NOT release (let TTL expire)
                if (isDefiniteNonConsumingRejection(e)) {
                    log.info("AI_CAPACITY_RELEASED: slot={}, definite non-consumption error", candidate.getSlotAlias());
                    capacityService.release(reservation);
                } else {
                    log.warn("AI_CAPACITY_UNCERTAIN: slot={}, ambiguous error ({}), keeping reservation until TTL expiry",
                            candidate.getSlotAlias(), e.getClass().getSimpleName());
                }

                // Cooldown slot on 429 / rate limit error
                if (e.getMessage() != null && e.getMessage().contains("429")) {
                    applyCooldown(candidate, 60);
                }

                log.info("AI_PROVIDER_FAILOVER: Attempting next candidate slot...");
            }
        }

        log.error("PROVIDER_EXECUTION_FAILED: All candidates exhausted for serviceId={}", callerServiceId);
        throw new IllegalStateException("PROVIDER_EXECUTION_FAILED: All candidates failed to execute.", lastException);
    }

    /**
     * Governed Embedding execution through candidate loop.
     */
    public AiEmbeddingResult embed(
            GovernanceDecision decision,
            AiEmbeddingRequest request,
            String callerServiceId,
            TokenBudget tokenBudget) {

        List<ProviderSlot> candidates = routingService.getCandidates(
                decision.allowedProviders(),
                decision.allowedProviderPools(),
                AiOperation.EMBED,
                callerServiceId,
                tokenBudget
        );

        if (candidates.isEmpty()) {
            log.error("PROVIDER_CAPACITY_EXHAUSTED: No embedding candidates for serviceId={}", callerServiceId);
            throw new IllegalStateException("PROVIDER_CAPACITY_EXHAUSTED: No available embedding provider candidates.");
        }

        Exception lastException = null;

        for (ProviderSlot candidate : candidates) {
            Optional<ProviderReservation> reservationOpt = capacityService.tryReserve(
                    candidate,
                    tokenBudget.reservationTokens() // estimatedInputTokens
            );

            if (reservationOpt.isEmpty()) {
                log.info("AI_CAPACITY_RESERVATION_FAILED: Contention on embedding slot {}, advancing...",
                        candidate.getSlotAlias());
                continue;
            }

            ProviderReservation reservation = reservationOpt.get();
            AiProviderClient client = resolveProviderClient(candidate.getProvider());

            try {
                AiEmbeddingResult result = client.embed(candidate, request);

                // Reconcile embedding tokens (input only)
                capacityService.reconcile(reservation, result.inputTokens());

                log.info("AI_EMBEDDING_COMPLETED: slot={}, inputTokens={}",
                        candidate.getSlotAlias(), result.inputTokens());
                return result;

            } catch (Exception e) {
                lastException = e;
                log.warn("AI_PROVIDER_FAILED: embedding slot={}, error={}", candidate.getSlotAlias(), e.getMessage());
                if (isDefiniteNonConsumingRejection(e)) {
                    capacityService.release(reservation);
                } else {
                    log.warn("AI_CAPACITY_UNCERTAIN: embedding slot={}, keeping reservation until TTL expiry", candidate.getSlotAlias());
                }
                if (e.getMessage() != null && e.getMessage().contains("429")) {
                    applyCooldown(candidate, 60);
                }
            }
        }

        throw new IllegalStateException("PROVIDER_EXECUTION_FAILED: All embedding candidates failed.", lastException);
    }

    private boolean isDefiniteNonConsumingRejection(Exception e) {
        if (e == null || e.getMessage() == null) return false;
        String msg = e.getMessage().toLowerCase();
        // 429 Too Many Requests or 400 Bad Request or pre-call credential lookup failure
        return msg.contains("429") || msg.contains("too many requests") || msg.contains("rate limit") ||
               msg.contains("400") || msg.contains("bad request") || msg.contains("secretref cannot be null");
    }

    private AiProviderClient resolveProviderClient(AiProvider provider) {
        return switch (provider) {
            case GEMINI -> providerClients.get("geminiProviderClient");
            case AZURE_OPENAI -> providerClients.get("azureOpenAiProviderClient");
        };
    }

    private void applyCooldown(ProviderSlot slot, int seconds) {
        try {
            slot.setCooldownUntil(OffsetDateTime.now().plusSeconds(seconds));
            slotRepository.save(slot);
            log.info("Applied {}s cooldown to slot: {}", seconds, slot.getSlotAlias());
        } catch (Exception ex) {
            log.warn("Failed to persist cooldown for slot {}: {}", slot.getSlotAlias(), ex.getMessage());
        }
    }
}
