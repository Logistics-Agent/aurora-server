package com.aurora.shared.pagination;

import org.springframework.data.domain.PageRequest;
import org.springframework.data.domain.Pageable;

/**
 * Utility cho gRPC Pagination (convert giữa Proto page params và Spring Data Pageable).
 */
public final class GrpcPaginationUtils {

    private GrpcPaginationUtils() {}

    public static final int DEFAULT_PAGE_SIZE = 20;
    public static final int MAX_PAGE_SIZE = 100;

    public static Pageable toPageable(int pageNumber, int pageSize) {
        int page = Math.max(0, pageNumber);
        int size = pageSize <= 0 ? DEFAULT_PAGE_SIZE : Math.min(pageSize, MAX_PAGE_SIZE);
        return PageRequest.of(page, size);
    }
}
