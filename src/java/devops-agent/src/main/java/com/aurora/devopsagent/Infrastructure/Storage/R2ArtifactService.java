package com.aurora.devopsagent.Infrastructure.Storage;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;
import software.amazon.awssdk.auth.credentials.AwsBasicCredentials;
import software.amazon.awssdk.auth.credentials.StaticCredentialsProvider;
import software.amazon.awssdk.core.sync.RequestBody;
import software.amazon.awssdk.regions.Region;
import software.amazon.awssdk.services.s3.S3Client;
import software.amazon.awssdk.services.s3.model.PutObjectRequest;

import java.net.URI;
import java.nio.charset.StandardCharsets;

@Service
public class R2ArtifactService {

    private static final Logger log = LoggerFactory.getLogger(R2ArtifactService.class);

    private final String bucketName;
    private final S3Client s3Client;

    public R2ArtifactService(
            @Value("${cloudflare.r2.endpoint:}") String endpoint,
            @Value("${cloudflare.r2.access-key-id:}") String accessKey,
            @Value("${cloudflare.r2.secret-access-key:}") String secretKey,
            @Value("${cloudflare.r2.bucket-name:devops-agent-artifacts}") String bucketName) {
        this.bucketName = bucketName;

        if (endpoint != null && !endpoint.isBlank() && accessKey != null && !accessKey.isBlank()) {
            this.s3Client = S3Client.builder()
                    .endpointOverride(URI.create(endpoint))
                    .credentialsProvider(StaticCredentialsProvider.create(
                            AwsBasicCredentials.create(accessKey, secretKey != null ? secretKey : "")
                    ))
                    .region(Region.of("auto"))
                    .build();
            log.info("Initialized Cloudflare R2 S3 Client for bucket '{}'", bucketName);
        } else {
            this.s3Client = null;
            log.info("Cloudflare R2 is not configured; artifact upload will run in local simulation mode.");
        }
    }

    public String uploadDiagnosticLog(String correlationId, String artifactName, String content) {
        String key = String.format("incidents/%s/%s", correlationId, artifactName);
        if (s3Client != null) {
            try {
                PutObjectRequest request = PutObjectRequest.builder()
                        .bucket(bucketName)
                        .key(key)
                        .contentType("text/plain")
                        .build();
                s3Client.putObject(request, RequestBody.fromString(content, StandardCharsets.UTF_8));
                log.info("Uploaded artifact to R2: s3://{}/{}", bucketName, key);
                return key;
            } catch (Exception e) {
                log.error("Failed to upload artifact to R2: {}", e.getMessage(), e);
                return "local://fallback/" + key;
            }
        }
        log.debug("Simulated artifact upload: {}", key);
        return "local://" + key;
    }
}
