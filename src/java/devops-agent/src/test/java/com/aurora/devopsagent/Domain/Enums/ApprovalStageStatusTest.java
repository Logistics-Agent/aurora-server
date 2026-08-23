package com.aurora.devopsagent.Domain.Enums;

import com.aurora.devopsagent.Domain.Entity.PrApprovalRecord;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.*;

class ApprovalStageStatusTest {

    @Test
    @DisplayName("ApprovalStage contains MERGE and PRODUCTION_DEPLOY")
    void testApprovalStageEnum() {
        assertEquals(2, ApprovalStage.values().length);
        assertNotNull(ApprovalStage.valueOf("MERGE"));
        assertNotNull(ApprovalStage.valueOf("PRODUCTION_DEPLOY"));
    }

    @Test
    @DisplayName("ApprovalStatus contains orthogonal PENDING, APPROVED, REJECTED, EXPIRED without encoded stages")
    void testApprovalStatusEnum() {
        assertEquals(4, ApprovalStatus.values().length);
        assertNotNull(ApprovalStatus.valueOf("PENDING"));
        assertNotNull(ApprovalStatus.valueOf("APPROVED"));
        assertNotNull(ApprovalStatus.valueOf("REJECTED"));
        assertNotNull(ApprovalStatus.valueOf("EXPIRED"));

        // Confirm composite legacy stage-status enums no longer exist
        assertThrows(IllegalArgumentException.class, () -> ApprovalStatus.valueOf("PENDING_APPROVAL_1"));
        assertThrows(IllegalArgumentException.class, () -> ApprovalStatus.valueOf("PENDING_APPROVAL_2"));
    }

    @Test
    @DisplayName("PrApprovalRecord combines independent Stage and Status")
    void testPrApprovalRecordStageAndStatus() {
        PrApprovalRecord record = new PrApprovalRecord();
        record.setStage(ApprovalStage.MERGE);
        record.setStatus(ApprovalStatus.PENDING);

        assertEquals(ApprovalStage.MERGE, record.getStage());
        assertEquals(ApprovalStatus.PENDING, record.getStatus());

        record.setStage(ApprovalStage.PRODUCTION_DEPLOY);
        record.setStatus(ApprovalStatus.APPROVED);

        assertEquals(ApprovalStage.PRODUCTION_DEPLOY, record.getStage());
        assertEquals(ApprovalStatus.APPROVED, record.getStatus());
    }
}
