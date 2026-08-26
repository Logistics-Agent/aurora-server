using System;
using System.Collections.Generic;
using System.Linq;

namespace Shared.Constants;

public static class PermissionConstants
{
    // =========================
    // MAIL
    // =========================
    public static class Mail
    {
        // Normal operation
        public const string Read = "mail:read";
        public const string DraftCreate = "mail:draft:create";
        public const string Send = "mail:send";
        public const string ThreadClaim = "mail:thread:claim";

        // Supervision
        public const string ThreadReadAll = "mail:thread:read_all";
        public const string ThreadReassign = "mail:thread:reassign";
        public const string ThreadUnassign = "mail:thread:unassign";

        // Security
        public const string QuarantineRead = "mail:quarantine:read";
        public const string QuarantineRelease = "mail:quarantine:release";
        public const string QuarantineDelete = "mail:quarantine:delete";
        public const string AuditRead = "mail:audit:read";

        // Tenant administration
        public const string DomainManage = "mail:domain:manage";
        public const string MailboxManage = "mail:mailbox:manage";

        // Platform operation
        public const string SystemManage = "mail:system:manage";
    }

    // =========================
    // SHIPMENT
    // =========================
    public static class Shipment
    {
        public const string Create = "shipments:create";
        public const string Read = "shipments:read";
        public const string Update = "shipments:update";
        public const string Submit = "shipments:submit";

        public const string Cancel = "shipments:cancel";
        public const string Delete = "shipments:delete";
        public const string Import = "shipments:import";
    }

    // =========================
    // ROUTE PLANNING
    // =========================
    public static class RoutePlanning
    {
        public const string Read = "route_planning:read";
        public const string Create = "route_planning:create";
        public const string Update = "route_planning:update";
        public const string Optimize = "route_planning:optimize";
        public const string Execute = "route_planning:execute";
        public const string Delete = "route_planning:delete";

        // Exception governance
        public const string ApprovalRead = "route_planning:approval:read";
        public const string Approve = "route_planning:approve";
        public const string Reject = "route_planning:reject";

        // Tenant policy administration
        public const string PolicyManage = "route_planning:policy:manage";
        public const string PolicyPublish = "route_planning:policy:publish";
    }

    // =========================
    // OCR
    // =========================
    public static class Ocr
    {
        // Extract/read inherit document/shipment access.
        public const string Review = "ocr:review";
    }

    // =========================
    // DOCUMENT / KNOWLEDGE
    // =========================
    public static class Documents
    {
        // Read/query use resource scope.
        public const string Ingest = "documents:ingest";
        public const string Manage = "documents:manage";
    }

    // =========================
    // COMPLIANCE
    // =========================
    public static class Compliance
    {
        // Read/query/evaluate are normal operational capabilities.
        public const string Override = "compliance:override";

        // Platform knowledge management.
        public const string PlatformIngest = "compliance:platform:ingest";
    }

    // =========================
    // FINANCIAL
    // =========================
    public static class Financial
    {
        public const string Read = "financial_tax:read";
        public const string Calculate = "financial_tax:calculate";
    }

    // =========================
    // BILLING / SETTLEMENT
    // =========================
    public static class Billing
    {
        public const string Read = "billing_settlement:read";
        public const string InvoiceCreate = "billing_settlement:invoice:create";
        public const string InvoiceUpdate = "billing_settlement:invoice:update";

        public const string CreditCheck = "billing_settlement:credit:check";
        public const string EscrowRead = "billing_settlement:escrow:read";

        // Sensitive financial authority
        public const string SettlementManage = "billing_settlement:settlement:manage";
    }

    // =========================
    // GPS
    // =========================
    public static class Gps
    {
        // Tracking read inherits shipment access.
        public const string GeofenceManage = "gps_tracking:geofence:manage";
    }

    // =========================
    // IAM
    // =========================
    public static class Iam
    {
        public const string UserRead = "iam:user:read";
        public const string UserInvite = "iam:user:invite";
        public const string UserUpdate = "iam:user:update";

        public const string RoleRead = "iam:role:read";
        public const string RoleManage = "iam:role:manage";
        public const string PermissionManage = "iam:permission:manage";
    }

    /// <summary>
    /// Legacy helper for backwards-compatibility callers.
    /// </summary>
    public static string Build(string module, string action) => $"{module}:{action}";

    /// <summary>
    /// Returns all authoritative capability permissions in the system.
    /// </summary>
    public static IReadOnlyList<string> GetAllPermissions() =>
    [
        // Mail
        Mail.Read, Mail.DraftCreate, Mail.Send, Mail.ThreadClaim,
        Mail.ThreadReadAll, Mail.ThreadReassign, Mail.ThreadUnassign,
        Mail.QuarantineRead, Mail.QuarantineRelease, Mail.QuarantineDelete, Mail.AuditRead,
        Mail.DomainManage, Mail.MailboxManage, Mail.SystemManage,

        // Shipment
        Shipment.Create, Shipment.Read, Shipment.Update, Shipment.Submit,
        Shipment.Cancel, Shipment.Delete, Shipment.Import,

        // Route Planning
        RoutePlanning.Read, RoutePlanning.Create, RoutePlanning.Update, RoutePlanning.Optimize,
        RoutePlanning.Execute, RoutePlanning.Delete, RoutePlanning.ApprovalRead,
        RoutePlanning.Approve, RoutePlanning.Reject, RoutePlanning.PolicyManage, RoutePlanning.PolicyPublish,

        // OCR
        Ocr.Review,

        // Documents
        Documents.Ingest, Documents.Manage,

        // Compliance
        Compliance.Override, Compliance.PlatformIngest,

        // Financial
        Financial.Read, Financial.Calculate,

        // Billing
        Billing.Read, Billing.InvoiceCreate, Billing.InvoiceUpdate,
        Billing.CreditCheck, Billing.EscrowRead, Billing.SettlementManage,

        // GPS
        Gps.GeofenceManage,

        // IAM
        Iam.UserRead, Iam.UserInvite, Iam.UserUpdate,
        Iam.RoleRead, Iam.RoleManage, Iam.PermissionManage
    ];

    /// <summary>
    /// Gets baseline permissions for a standard Staff role.
    /// Standard Staff receives ONLY baseline operational access. Approvals, overrides,
    /// reassignment, deletion, and platform/tenant configs require explicit role or permission grants.
    /// </summary>
    public static IReadOnlyList<string> GetDefaultStaffPermissions() =>
    [
        // Mail baseline
        Mail.Read,
        Mail.DraftCreate,
        Mail.Send,
        Mail.ThreadClaim,

        // Shipment baseline
        Shipment.Read,
        Shipment.Create,
        Shipment.Update,
        Shipment.Submit,

        // Route baseline
        RoutePlanning.Read,
        RoutePlanning.Create,
        RoutePlanning.Update,
        RoutePlanning.Optimize,
        RoutePlanning.Execute,

        // Financial baseline
        Financial.Read,
        Financial.Calculate,

        // Billing baseline
        Billing.Read,
        Billing.CreditCheck,
        Billing.EscrowRead,

        // IAM baseline
        Iam.UserRead
    ];

    /// <summary>
    /// Gets supervisory extension permissions for the MANAGER role.
    /// </summary>
    public static IReadOnlyList<string> GetDefaultManagerPermissions() =>
    [
        .. GetDefaultStaffPermissions(),

        // Mail supervision & quarantine review
        Mail.ThreadReadAll,
        Mail.ThreadReassign,
        Mail.ThreadUnassign,
        Mail.QuarantineRead,
        Mail.QuarantineRelease,
        Mail.AuditRead,

        // Shipment management
        Shipment.Cancel,
        Shipment.Delete,
        Shipment.Import,

        // Route planning governance
        RoutePlanning.Delete,
        RoutePlanning.ApprovalRead,
        RoutePlanning.Approve,
        RoutePlanning.Reject,

        // OCR review
        Ocr.Review,

        // Document knowledge management
        Documents.Ingest,
        Documents.Manage,

        // Compliance override
        Compliance.Override,

        // Billing & settlement management
        Billing.InvoiceCreate,
        Billing.InvoiceUpdate,
        Billing.SettlementManage,

        // GPS geofence configuration
        Gps.GeofenceManage,

        // IAM role viewing
        Iam.RoleRead
    ];

    /// <summary>
    /// Gets administrative permissions for the TENANT_ADMIN role (all tenant-scoped permissions).
    /// Excludes system-only permissions (Mail.SystemManage, Compliance.PlatformIngest).
    /// </summary>
    public static IReadOnlyList<string> GetTenantAdminPermissions() =>
        [.. GetAllPermissions().Where(p => p != Mail.SystemManage && p != Compliance.PlatformIngest)];
}
