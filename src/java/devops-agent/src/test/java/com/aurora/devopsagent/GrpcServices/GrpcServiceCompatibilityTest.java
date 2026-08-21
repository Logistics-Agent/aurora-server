package com.aurora.devopsagent.GrpcServices;

import com.aurora.devopsagent.grpc.DevOpsConfigServiceGrpc;
import com.aurora.devopsagent.grpc.DevOpsIncidentServiceGrpc;
import com.aurora.devopsagent.grpc.DevOpsIngestionServiceGrpc;
import com.aurora.devopsagent.grpc.DevOpsRuleServiceGrpc;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

class GrpcServiceCompatibilityTest {

    @Test
    @DisplayName("Verify 4 domain gRPC services are properly generated and decoupled")
    void testDomainGrpcServicesExist() {
        assertNotNull(DevOpsIngestionServiceGrpc.DevOpsIngestionServiceImplBase.class);
        assertNotNull(DevOpsIncidentServiceGrpc.DevOpsIncidentServiceImplBase.class);
        assertNotNull(DevOpsRuleServiceGrpc.DevOpsRuleServiceImplBase.class);
        assertNotNull(DevOpsConfigServiceGrpc.DevOpsConfigServiceImplBase.class);

        assertTrue(DevOpsIngestionServiceGrpc.DevOpsIngestionServiceImplBase.class.isAssignableFrom(IngestionGrpcHandler.class));
        assertTrue(DevOpsIncidentServiceGrpc.DevOpsIncidentServiceImplBase.class.isAssignableFrom(IncidentGrpcHandler.class));
        assertTrue(DevOpsRuleServiceGrpc.DevOpsRuleServiceImplBase.class.isAssignableFrom(RuleGrpcHandler.class));
        assertTrue(DevOpsConfigServiceGrpc.DevOpsConfigServiceImplBase.class.isAssignableFrom(SelfConfigGrpcHandler.class));
    }
}
