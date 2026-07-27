namespace IamTenant.Domain.Enums;

public enum TenantStatus
{
    Provisioning,
    Active,
    Suspended,
    Archived
}

public enum UserStatus
{
    Invited,
    Active,
    Blocked
}

public enum UserType
{
    SystemAdmin,
    TenantAdmin,
    TenantStaff,
    TenantManager
}

/// <summary>
/// Nhóm nghiệp vụ chính của staff (đã rút gọn từ 9 type theo module).
/// ⚠ Lưu DB dạng int (ordinal) — CHỈ ĐƯỢC APPEND member mới, KHÔNG reorder/xóa.
/// </summary>
public enum StaffType
{
    Normal = 0,          // mặc định
    Operations = 1,      // ← RoutePlanning, GpsTracking
    Documentation = 2,   // ← Ocr, Compliance
    CustomerService = 3, // ← CustomerAssistant, Negotiation
    Finance = 4          // ← FinancialTax, BillingSettlement
}
