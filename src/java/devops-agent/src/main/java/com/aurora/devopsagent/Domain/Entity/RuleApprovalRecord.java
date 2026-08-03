package com.aurora.devopsagent.Domain.Entity;

import com.aurora.devopsagent.Domain.Enums.ApprovalStatus;
import com.aurora.shared.entity.AuditableEntity;
import jakarta.persistence.*;

import java.time.Instant;

@Entity
@Table(name = "rule_approval_records")
public class RuleApprovalRecord extends AuditableEntity {

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "pending_rule_id", nullable = false)
    private PendingRule pendingRule;

    @Enumerated(EnumType.STRING)
    @Column(name = "status", nullable = false, length = 50)
    private ApprovalStatus status = ApprovalStatus.PENDING;

    @Column(name = "reviewed_by", length = 100)
    private String reviewedBy;

    @Column(name = "review_comment", columnDefinition = "TEXT")
    private String reviewComment;

    @Column(name = "reviewed_at")
    private Instant reviewedAt;

    public PendingRule getPendingRule() {
        return pendingRule;
    }

    public void setPendingRule(PendingRule pendingRule) {
        this.pendingRule = pendingRule;
    }

    public ApprovalStatus getStatus() {
        return status;
    }

    public void setStatus(ApprovalStatus status) {
        this.status = status;
    }

    public String getReviewedBy() {
        return reviewedBy;
    }

    public void setReviewedBy(String reviewedBy) {
        this.reviewedBy = reviewedBy;
    }

    public String getReviewComment() {
        return reviewComment;
    }

    public void setReviewComment(String reviewComment) {
        this.reviewComment = reviewComment;
    }

    public Instant getReviewedAt() {
        return reviewedAt;
    }

    public void setReviewedAt(Instant reviewedAt) {
        this.reviewedAt = reviewedAt;
    }
}
