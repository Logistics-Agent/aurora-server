package com.aurora.shared.security;

import com.aurora.shared.constants.GrpcMetadataKeys;
import io.grpc.*;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;

public class ClientMetadataInterceptorServiceIdTest {

    @BeforeEach
    public void setup() {
        CurrentServiceContext.clear();
        CurrentUserContext.clear();
    }

    @AfterEach
    public void tearDown() {
        CurrentServiceContext.clear();
        CurrentUserContext.clear();
    }

    @Test
    public void testClientMetadataInterceptor_PropagatesServiceIdHeader() {
        CurrentServiceContext serviceContext = new CurrentServiceContext();
        serviceContext.populate("devops-agent");
        CurrentServiceContext.setCurrent(serviceContext);

        ClientMetadataInterceptor interceptor = new ClientMetadataInterceptor();

        Metadata capturedHeaders = new Metadata();

        Channel mockChannel = new Channel() {
            @Override
            public <RequestT, ResponseT> ClientCall<RequestT, ResponseT> newCall(
                    MethodDescriptor<RequestT, ResponseT> methodDescriptor, CallOptions callOptions) {
                return new ClientCall<RequestT, ResponseT>() {
                    @Override
                    public void start(Listener<ResponseT> responseListener, Metadata headers) {
                        capturedHeaders.merge(headers);
                    }
                    @Override public void request(int numMessages) {}
                    @Override public void cancel(String message, Throwable cause) {}
                    @Override public void halfClose() {}
                    @Override public void sendMessage(RequestT message) {}
                };
            }
            @Override public String authority() { return "test-authority"; }
        };

        ClientCall<String, String> call = interceptor.interceptCall(null, CallOptions.DEFAULT, mockChannel);
        call.start(new ClientCall.Listener<String>() {}, new Metadata());

        String forwardedServiceId = capturedHeaders.get(GrpcMetadataKeys.SERVICE_ID);
        assertNotNull(forwardedServiceId);
        assertEquals("devops-agent", forwardedServiceId);
    }
}
