package pagination

import "math"


func NormalizePagination(page, limit int32) (int32, int32) {
	if page <= 0 {
		page = 1
	}
	if limit <= 0 {
		limit = 10
	} else if limit > 100 {
		limit = 100
	}
	return page, limit
}

// CalculateTotalPages tính tổng số trang
func CalculateTotalPages(totalItems int64, limit int32) int32 {
	if limit <= 0 {
		return 0
	}
	if totalItems <= 0 {
		return 0
	}
	return int32(math.Ceil(float64(totalItems) / float64(limit)))
}

// Offset tính offset cho SQL query
func Offset(page, limit int32) int32 {
	if page <= 0 {
		page = 1
	}
	return (page - 1) * limit
}

