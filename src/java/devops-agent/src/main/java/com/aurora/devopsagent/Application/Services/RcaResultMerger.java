package com.aurora.devopsagent.Application.Services;

import com.aurora.devopsagent.Domain.Entity.RcaAnalysis;
import com.aurora.devopsagent.Domain.ValueObject.Recommendation;

import java.util.List;

public interface RcaResultMerger {
    /**
     * Merge N parallel RCA results into a single final Recommendation.
     */
    Recommendation merge(List<RcaAnalysis> analyses);
}
