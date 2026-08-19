using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IamTenant.Infrastructure.Persistences.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Actor = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Resource = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "text", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TaxCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CompanyDomain = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PlanType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsSystemRole = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Roles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CognitoSub = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StaffCode = table.Column<string>(type: "text", nullable: true),
                    Department = table.Column<string>(type: "text", nullable: true),
                    UserType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StaffType = table.Column<int>(type: "integer", nullable: false),
                    PermissionVersion = table.Column<int>(type: "integer", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPermissions",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermissions", x => new { x.UserId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_UserPermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPermissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Code", "Description", "Module" },
                values: new object[,]
                {
                    { new Guid("068e32ca-33e5-70a5-6604-c194694bc1e3"), "customer_assistant:read", "Allows read operation on customer_assistant", "customer_assistant" },
                    { new Guid("081e77ae-7147-2809-2f39-e9b9cb280902"), "financial_tax:update", "Allows update operation on financial_tax", "financial_tax" },
                    { new Guid("0dbd7403-68e6-a892-6469-df2e70e1e833"), "ocr:read", "Allows read operation on ocr", "ocr" },
                    { new Guid("15625c6c-9b22-c1ae-6983-4251cb40be02"), "customer_assistant:assign", "Allows assign operation on customer_assistant", "customer_assistant" },
                    { new Guid("15b7ea39-f96f-729d-a8f0-9ab100a70f9d"), "iam:read", "Allows read operation on iam", "iam" },
                    { new Guid("187716d8-3c74-0e02-747e-02e1615e2866"), "compliance:delete", "Allows delete operation on compliance", "compliance" },
                    { new Guid("19857379-200c-97d0-5ff9-6f0b9badda77"), "gps_tracking:create", "Allows create operation on gps_tracking", "gps_tracking" },
                    { new Guid("1c3ca6c8-4329-e474-b556-57f0038debf8"), "billing_settlement:export", "Allows export operation on billing_settlement", "billing_settlement" },
                    { new Guid("1c8018e6-7b49-9272-7205-62a82f3e3c40"), "documents:import", "Allows import operation on documents", "documents" },
                    { new Guid("1e712188-2f5a-dd70-6f50-ac0a66320d32"), "negotiation:create", "Allows create operation on negotiation", "negotiation" },
                    { new Guid("1ff37fd5-4256-1381-f7d1-00ecd272c99e"), "billing_settlement:delete", "Allows delete operation on billing_settlement", "billing_settlement" },
                    { new Guid("2cfedbf2-c0ae-3639-54a0-56a8179d0bcc"), "financial_tax:export", "Allows export operation on financial_tax", "financial_tax" },
                    { new Guid("2ed631a4-5ed7-f20b-90d5-4672f613bb3b"), "iam:export", "Allows export operation on iam", "iam" },
                    { new Guid("31d1e9ec-14cc-f2dd-a7c9-5d7a46fb7178"), "documents:update", "Allows update operation on documents", "documents" },
                    { new Guid("3d0ae68a-a252-8a70-af99-411d4e24da5e"), "gps_tracking:delete", "Allows delete operation on gps_tracking", "gps_tracking" },
                    { new Guid("42417a0a-4563-a7c4-c2ef-347d7334faf9"), "negotiation:delete", "Allows delete operation on negotiation", "negotiation" },
                    { new Guid("4fac1c69-8449-ac46-7950-02a1bbbccc28"), "ocr:assign", "Allows assign operation on ocr", "ocr" },
                    { new Guid("5269576b-1b7c-0564-5877-6be5b44805ea"), "iam:assign", "Allows assign operation on iam", "iam" },
                    { new Guid("5852a524-2a93-1166-f085-abac92302476"), "negotiation:assign", "Allows assign operation on negotiation", "negotiation" },
                    { new Guid("590cd177-2903-543a-2ccc-c2fa3e8139f1"), "gps_tracking:update", "Allows update operation on gps_tracking", "gps_tracking" },
                    { new Guid("5a4cbf72-f6ad-9cef-3175-8c428c98d471"), "iam:import", "Allows import operation on iam", "iam" },
                    { new Guid("5a7eb9f6-3de5-db44-3b3a-cafde0e300aa"), "financial_tax:assign", "Allows assign operation on financial_tax", "financial_tax" },
                    { new Guid("5c27a6c3-737a-b19d-8ab9-e0dc98c15200"), "iam:create", "Allows create operation on iam", "iam" },
                    { new Guid("6081e667-ca38-9248-3fbd-bb64e16673ef"), "billing_settlement:update", "Allows update operation on billing_settlement", "billing_settlement" },
                    { new Guid("645c65e3-9165-2bc5-10fa-aa6d110989ec"), "billing_settlement:import", "Allows import operation on billing_settlement", "billing_settlement" },
                    { new Guid("6fc2d4ff-4899-517b-38c7-2d72128b1624"), "financial_tax:create", "Allows create operation on financial_tax", "financial_tax" },
                    { new Guid("71a58738-ef23-26c2-6c4b-041d371e2427"), "iam:delete", "Allows delete operation on iam", "iam" },
                    { new Guid("71d5a7d6-7606-14a6-6ad1-1e8f02f0e104"), "gps_tracking:import", "Allows import operation on gps_tracking", "gps_tracking" },
                    { new Guid("734a0a4e-985c-ac75-21c0-5a337882e175"), "customer_assistant:import", "Allows import operation on customer_assistant", "customer_assistant" },
                    { new Guid("754fb2cc-eba6-b8a2-6c94-373511245170"), "customer_assistant:delete", "Allows delete operation on customer_assistant", "customer_assistant" },
                    { new Guid("75dfde55-3ca2-ebfd-9f0b-d97976653e2e"), "documents:export", "Allows export operation on documents", "documents" },
                    { new Guid("7b8b7102-317b-4f4b-70fd-55709da64424"), "customer_assistant:update", "Allows update operation on customer_assistant", "customer_assistant" },
                    { new Guid("7fe854ea-7721-4bae-b0ef-e89b33e616f7"), "compliance:read", "Allows read operation on compliance", "compliance" },
                    { new Guid("81270dd0-8652-46a4-5298-80fe31302b99"), "route_planning:create", "Allows create operation on route_planning", "route_planning" },
                    { new Guid("83602fc0-e37d-f93a-d8d0-3aac458e5f1a"), "route_planning:assign", "Allows assign operation on route_planning", "route_planning" },
                    { new Guid("857b24df-0d1b-73fe-7f78-3d6a32f5d243"), "documents:read", "Allows read operation on documents", "documents" },
                    { new Guid("8beabe90-8210-25ac-c7b0-f3827de23154"), "ocr:export", "Allows export operation on ocr", "ocr" },
                    { new Guid("8dc72112-1039-3cfe-b05a-c79bd13a8225"), "compliance:export", "Allows export operation on compliance", "compliance" },
                    { new Guid("9074e07d-5b60-86c6-0b76-c7767c00b9df"), "compliance:create", "Allows create operation on compliance", "compliance" },
                    { new Guid("9746829c-f3f6-b3b0-d5b6-ac14c7dbb8fa"), "billing_settlement:create", "Allows create operation on billing_settlement", "billing_settlement" },
                    { new Guid("9a1074c4-b162-26e0-bf4c-cd688f9e1c7c"), "route_planning:update", "Allows update operation on route_planning", "route_planning" },
                    { new Guid("9f3754a7-9870-42d3-78ed-fcb514dbd427"), "compliance:update", "Allows update operation on compliance", "compliance" },
                    { new Guid("a9243615-16e0-02f0-5c76-d82e58a9b9f9"), "gps_tracking:export", "Allows export operation on gps_tracking", "gps_tracking" },
                    { new Guid("b25b0111-2e88-6ba7-34ba-5a771dec7241"), "route_planning:import", "Allows import operation on route_planning", "route_planning" },
                    { new Guid("b89eae95-03b7-82fa-270f-6f5133454213"), "financial_tax:read", "Allows read operation on financial_tax", "financial_tax" },
                    { new Guid("bd28c899-6437-ba29-33c3-a59ff4c79742"), "route_planning:read", "Allows read operation on route_planning", "route_planning" },
                    { new Guid("be875c50-f686-d8a4-ae8e-b21093e76d44"), "documents:assign", "Allows assign operation on documents", "documents" },
                    { new Guid("c042d5aa-947e-76f7-2cc0-4babb6d75b4e"), "gps_tracking:assign", "Allows assign operation on gps_tracking", "gps_tracking" },
                    { new Guid("c410e9fe-f15d-ffe1-dc9a-e6b12fe8dd85"), "compliance:assign", "Allows assign operation on compliance", "compliance" },
                    { new Guid("c7f952a0-8420-7897-dde0-8dee648bba12"), "negotiation:import", "Allows import operation on negotiation", "negotiation" },
                    { new Guid("cc867d6c-139d-d0e2-e1a8-35f8c4f0ed09"), "customer_assistant:export", "Allows export operation on customer_assistant", "customer_assistant" },
                    { new Guid("ce7ce257-1af7-5e19-6204-0e627f666d39"), "documents:delete", "Allows delete operation on documents", "documents" },
                    { new Guid("d46f3cb3-ead7-3d89-41e7-a619d5739ca5"), "ocr:delete", "Allows delete operation on ocr", "ocr" },
                    { new Guid("d5fca949-8ce0-187a-0f18-4bffc54af9fa"), "financial_tax:import", "Allows import operation on financial_tax", "financial_tax" },
                    { new Guid("d7cdfd4d-50d3-e907-75ca-f015a389027d"), "ocr:update", "Allows update operation on ocr", "ocr" },
                    { new Guid("db360faa-461c-58e7-4d7d-b5f804e248c2"), "financial_tax:delete", "Allows delete operation on financial_tax", "financial_tax" },
                    { new Guid("e099cee0-e184-1ade-aafb-c89ee21e2ba3"), "route_planning:export", "Allows export operation on route_planning", "route_planning" },
                    { new Guid("e1f632dd-cfb6-9fd5-2447-3c14abab995b"), "customer_assistant:create", "Allows create operation on customer_assistant", "customer_assistant" },
                    { new Guid("e6b052c0-458c-279e-fc41-5483ceac1bc0"), "negotiation:read", "Allows read operation on negotiation", "negotiation" },
                    { new Guid("e7537c5f-f67b-2c09-9997-eeb6e976f5f6"), "billing_settlement:assign", "Allows assign operation on billing_settlement", "billing_settlement" },
                    { new Guid("e76d628b-4d81-0d8f-6f33-c1bd95c06d73"), "negotiation:export", "Allows export operation on negotiation", "negotiation" },
                    { new Guid("e7ecab79-93ce-0d34-affe-1c2a77e4405c"), "route_planning:delete", "Allows delete operation on route_planning", "route_planning" },
                    { new Guid("eac60635-7bf8-3672-09d5-8741b0d59532"), "iam:update", "Allows update operation on iam", "iam" },
                    { new Guid("f01a710f-a4e0-948b-e200-cc6e8de105b7"), "documents:create", "Allows create operation on documents", "documents" },
                    { new Guid("f217e581-9e02-462f-61cd-55111753ebb8"), "ocr:import", "Allows import operation on ocr", "ocr" },
                    { new Guid("f2a3cb70-fd5d-c6d0-2433-f42f51964b27"), "billing_settlement:read", "Allows read operation on billing_settlement", "billing_settlement" },
                    { new Guid("f2abfd09-091f-44ee-4d13-8b2cc475a24c"), "ocr:create", "Allows create operation on ocr", "ocr" },
                    { new Guid("f7d47ebe-9ca0-826b-cf43-3a55abc0df6c"), "negotiation:update", "Allows update operation on negotiation", "negotiation" },
                    { new Guid("faa8cf4e-c7aa-d9a2-82e6-8cf44cfc27da"), "gps_tracking:read", "Allows read operation on gps_tracking", "gps_tracking" },
                    { new Guid("faf272e6-dfdc-1357-6441-aeeb60df6874"), "compliance:import", "Allows import operation on compliance", "compliance" }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "Description", "IsSystemRole", "Name", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "SYSTEM_ADMIN", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, true, "System Administrator", new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "TENANT_ADMIN", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, true, "Tenant Administrator", new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "TENANT_STAFF", new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", null, true, "Tenant Staff", new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("068e32ca-33e5-70a5-6604-c194694bc1e3"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("081e77ae-7147-2809-2f39-e9b9cb280902"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("0dbd7403-68e6-a892-6469-df2e70e1e833"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("15625c6c-9b22-c1ae-6983-4251cb40be02"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("15b7ea39-f96f-729d-a8f0-9ab100a70f9d"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("187716d8-3c74-0e02-747e-02e1615e2866"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("19857379-200c-97d0-5ff9-6f0b9badda77"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("1c3ca6c8-4329-e474-b556-57f0038debf8"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("1c8018e6-7b49-9272-7205-62a82f3e3c40"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("1e712188-2f5a-dd70-6f50-ac0a66320d32"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("1ff37fd5-4256-1381-f7d1-00ecd272c99e"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("2cfedbf2-c0ae-3639-54a0-56a8179d0bcc"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("2ed631a4-5ed7-f20b-90d5-4672f613bb3b"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("31d1e9ec-14cc-f2dd-a7c9-5d7a46fb7178"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("3d0ae68a-a252-8a70-af99-411d4e24da5e"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("42417a0a-4563-a7c4-c2ef-347d7334faf9"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("4fac1c69-8449-ac46-7950-02a1bbbccc28"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("5269576b-1b7c-0564-5877-6be5b44805ea"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("5852a524-2a93-1166-f085-abac92302476"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("590cd177-2903-543a-2ccc-c2fa3e8139f1"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("5a4cbf72-f6ad-9cef-3175-8c428c98d471"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("5a7eb9f6-3de5-db44-3b3a-cafde0e300aa"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("5c27a6c3-737a-b19d-8ab9-e0dc98c15200"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("6081e667-ca38-9248-3fbd-bb64e16673ef"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("645c65e3-9165-2bc5-10fa-aa6d110989ec"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("6fc2d4ff-4899-517b-38c7-2d72128b1624"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("71a58738-ef23-26c2-6c4b-041d371e2427"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("71d5a7d6-7606-14a6-6ad1-1e8f02f0e104"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("734a0a4e-985c-ac75-21c0-5a337882e175"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("754fb2cc-eba6-b8a2-6c94-373511245170"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("75dfde55-3ca2-ebfd-9f0b-d97976653e2e"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("7b8b7102-317b-4f4b-70fd-55709da64424"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("7fe854ea-7721-4bae-b0ef-e89b33e616f7"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("81270dd0-8652-46a4-5298-80fe31302b99"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("83602fc0-e37d-f93a-d8d0-3aac458e5f1a"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("857b24df-0d1b-73fe-7f78-3d6a32f5d243"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("8beabe90-8210-25ac-c7b0-f3827de23154"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("8dc72112-1039-3cfe-b05a-c79bd13a8225"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("9074e07d-5b60-86c6-0b76-c7767c00b9df"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("9746829c-f3f6-b3b0-d5b6-ac14c7dbb8fa"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("9a1074c4-b162-26e0-bf4c-cd688f9e1c7c"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("9f3754a7-9870-42d3-78ed-fcb514dbd427"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("a9243615-16e0-02f0-5c76-d82e58a9b9f9"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("b25b0111-2e88-6ba7-34ba-5a771dec7241"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("b89eae95-03b7-82fa-270f-6f5133454213"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("bd28c899-6437-ba29-33c3-a59ff4c79742"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("be875c50-f686-d8a4-ae8e-b21093e76d44"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("c042d5aa-947e-76f7-2cc0-4babb6d75b4e"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("c410e9fe-f15d-ffe1-dc9a-e6b12fe8dd85"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("c7f952a0-8420-7897-dde0-8dee648bba12"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("cc867d6c-139d-d0e2-e1a8-35f8c4f0ed09"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("ce7ce257-1af7-5e19-6204-0e627f666d39"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("d46f3cb3-ead7-3d89-41e7-a619d5739ca5"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("d5fca949-8ce0-187a-0f18-4bffc54af9fa"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("d7cdfd4d-50d3-e907-75ca-f015a389027d"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("db360faa-461c-58e7-4d7d-b5f804e248c2"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("e099cee0-e184-1ade-aafb-c89ee21e2ba3"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("e1f632dd-cfb6-9fd5-2447-3c14abab995b"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("e6b052c0-458c-279e-fc41-5483ceac1bc0"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("e7537c5f-f67b-2c09-9997-eeb6e976f5f6"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("e76d628b-4d81-0d8f-6f33-c1bd95c06d73"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("e7ecab79-93ce-0d34-affe-1c2a77e4405c"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("eac60635-7bf8-3672-09d5-8741b0d59532"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("f01a710f-a4e0-948b-e200-cc6e8de105b7"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("f217e581-9e02-462f-61cd-55111753ebb8"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("f2a3cb70-fd5d-c6d0-2433-f42f51964b27"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("f2abfd09-091f-44ee-4d13-8b2cc475a24c"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("f7d47ebe-9ca0-826b-cf43-3a55abc0df6c"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("faa8cf4e-c7aa-d9a2-82e6-8cf44cfc27da"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("faf272e6-dfdc-1357-6441-aeeb60df6874"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("15b7ea39-f96f-729d-a8f0-9ab100a70f9d"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("2ed631a4-5ed7-f20b-90d5-4672f613bb3b"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("5269576b-1b7c-0564-5877-6be5b44805ea"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("5a4cbf72-f6ad-9cef-3175-8c428c98d471"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("5c27a6c3-737a-b19d-8ab9-e0dc98c15200"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("71a58738-ef23-26c2-6c4b-041d371e2427"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("81270dd0-8652-46a4-5298-80fe31302b99"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("83602fc0-e37d-f93a-d8d0-3aac458e5f1a"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("9a1074c4-b162-26e0-bf4c-cd688f9e1c7c"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("b25b0111-2e88-6ba7-34ba-5a771dec7241"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("bd28c899-6437-ba29-33c3-a59ff4c79742"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("e099cee0-e184-1ade-aafb-c89ee21e2ba3"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("e7ecab79-93ce-0d34-affe-1c2a77e4405c"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("eac60635-7bf8-3672-09d5-8741b0d59532"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("068e32ca-33e5-70a5-6604-c194694bc1e3"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("0dbd7403-68e6-a892-6469-df2e70e1e833"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("15b7ea39-f96f-729d-a8f0-9ab100a70f9d"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("1c3ca6c8-4329-e474-b556-57f0038debf8"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("1c8018e6-7b49-9272-7205-62a82f3e3c40"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("2cfedbf2-c0ae-3639-54a0-56a8179d0bcc"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("2ed631a4-5ed7-f20b-90d5-4672f613bb3b"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("5a4cbf72-f6ad-9cef-3175-8c428c98d471"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("645c65e3-9165-2bc5-10fa-aa6d110989ec"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("71d5a7d6-7606-14a6-6ad1-1e8f02f0e104"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("734a0a4e-985c-ac75-21c0-5a337882e175"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("75dfde55-3ca2-ebfd-9f0b-d97976653e2e"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("7fe854ea-7721-4bae-b0ef-e89b33e616f7"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("81270dd0-8652-46a4-5298-80fe31302b99"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("857b24df-0d1b-73fe-7f78-3d6a32f5d243"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("8beabe90-8210-25ac-c7b0-f3827de23154"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("8dc72112-1039-3cfe-b05a-c79bd13a8225"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("a9243615-16e0-02f0-5c76-d82e58a9b9f9"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("b25b0111-2e88-6ba7-34ba-5a771dec7241"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("b89eae95-03b7-82fa-270f-6f5133454213"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("bd28c899-6437-ba29-33c3-a59ff4c79742"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("c7f952a0-8420-7897-dde0-8dee648bba12"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("cc867d6c-139d-d0e2-e1a8-35f8c4f0ed09"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("d5fca949-8ce0-187a-0f18-4bffc54af9fa"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("e099cee0-e184-1ade-aafb-c89ee21e2ba3"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("e6b052c0-458c-279e-fc41-5483ceac1bc0"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("e76d628b-4d81-0d8f-6f33-c1bd95c06d73"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("f217e581-9e02-462f-61cd-55111753ebb8"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("f2a3cb70-fd5d-c6d0-2433-f42f51964b27"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("faa8cf4e-c7aa-d9a2-82e6-8cf44cfc27da"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("faf272e6-dfdc-1357-6441-aeeb60df6874"), new Guid("10000000-0000-0000-0000-000000000003") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_Code",
                table: "Roles",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Code",
                table: "Tenants",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_CompanyDomain",
                table: "Tenants",
                column: "CompanyDomain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_CreatedAt",
                table: "Tenants",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Status_CreatedAt",
                table: "Tenants",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_PermissionId",
                table: "UserPermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CognitoSub",
                table: "Users",
                column: "CognitoSub",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_CreatedAt",
                table: "Users",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_Email",
                table: "Users",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_Status",
                table: "Users",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "UserPermissions");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
