package com.aurora.shared.entity;

import jakarta.persistence.Id;
import jakarta.persistence.MappedSuperclass;
import jakarta.persistence.Version;

import java.io.Serializable;
import java.util.UUID;

/**
 * Base JPA Entity matching BaseEntity.cs in .NET (uses UUID PK).
 */
@MappedSuperclass
public abstract class BaseEntity implements Serializable {

    @Id
    private UUID id = UUID.randomUUID();

    @Version
    private Long version;

    public UUID getId() {
        return id;
    }

    public void setId(UUID id) {
        this.id = id;
    }

    public Long getVersion() {
        return version;
    }

    public void setVersion(Long version) {
        this.version = version;
    }
}
