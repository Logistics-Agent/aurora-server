package com.aurora.aigovernance.gateway.domain.entity;

import com.aurora.shared.entity.AuditableEntity;
import jakarta.persistence.*;

import java.util.ArrayList;
import java.util.List;

@Entity
@Table(name = "provider_pools")
public class ProviderPool extends AuditableEntity {

    @Column(name = "code", nullable = false, unique = true, length = 50)
    private String code;

    @Column(name = "name", nullable = false, length = 100)
    private String name;

    @OneToMany(mappedBy = "pool", cascade = CascadeType.ALL)
    private List<ProviderSlot> slots = new ArrayList<>();

    public String getCode() { return code; }
    public void setCode(String code) { this.code = code; }

    public String getName() { return name; }
    public void setName(String name) { this.name = name; }

    public List<ProviderSlot> getSlots() { return slots; }
}
