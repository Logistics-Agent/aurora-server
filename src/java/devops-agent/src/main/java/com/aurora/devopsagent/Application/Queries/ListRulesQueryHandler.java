package com.aurora.devopsagent.Application.Queries;

import com.aurora.devopsagent.Domain.Entity.ExistingRule;
import com.aurora.devopsagent.Infrastructure.Persistence.ExistingRuleJpaRepository;
import com.aurora.shared.pagination.GrpcPaginationUtils;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class ListRulesQueryHandler {

    private final ExistingRuleJpaRepository ruleRepository;

    public ListRulesQueryHandler(ExistingRuleJpaRepository ruleRepository) {
        this.ruleRepository = ruleRepository;
    }

    @Transactional(readOnly = true)
    public Page<ExistingRule> handle(int pageNumber, int pageSize) {
        Pageable pageable = GrpcPaginationUtils.toPageable(pageNumber, pageSize);
        return ruleRepository.findAll(pageable);
    }
}
