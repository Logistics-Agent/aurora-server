package domain

import "errors"

var (
	ErrRouteNotFound              = errors.New("không tìm thấy tuyến đường (Route)")
	ErrApprovalNotFound           = errors.New("không tìm thấy yêu cầu phê duyệt (ApprovalRequest)")
	ErrRiskPolicyNotFound         = errors.New("không tìm thấy chính sách rủi ro (TenantRiskPolicy)")
	ErrRiskPolicyNotConfigured    = errors.New("chính sách rủi ro chưa được cấu hình cho tenant này (Fail-Closed)")
	ErrOptimisticLock             = errors.New("xung đột phiên bản (Optimistic Concurrency Conflict) — dữ liệu đã bị sửa đổi bởi người khác")
	ErrInvalidStatusTransition    = errors.New("chuyển đổi trạng thái không hợp lệ")
	ErrInsufficientPermission     = errors.New("người dùng không đủ quyền hạn thực hiện hành động này")
	ErrTenantRequired             = errors.New("yêu cầu bắt buộc x-tenant-id hợp lệ")
	ErrRouteHasTooFewStops        = errors.New("tuyến đường phải có tối thiểu 2 điểm dừng để tối ưu hóa")
	ErrPolicyAlreadyPublished     = errors.New("chính sách đã được phát hành, không thể chỉnh sửa bản nháp")
	ErrPolicyCannotBeApproved     = errors.New("chỉ chính sách ở trạng thái chờ duyệt (PendingReview) mới có thể phê duyệt hoặc từ chối")
	ErrApprovalAlreadyReviewed    = errors.New("yêu cầu phê duyệt này đã được xử lý trước đó")
)

type DomainError struct {
	Code    string
	Message string
}

func (e *DomainError) Error() string {
	return e.Message
}

func NewDomainError(code, message string) *DomainError {
	return &DomainError{Code: code, Message: message}
}

