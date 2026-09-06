using Microsoft.OpenApi.Models;

namespace BuildingBlocks.BFF.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddCustomSwagger(this IServiceCollection services, string apiTitle)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = apiTitle,
                Version = "v1",
                Description = @"### 🔐 Authentication (BFF Cookie Session)
* **Login via Cognito**: Click nút **🔑 Login (Cognito)** ở trên thanh tiêu đề hoặc [👉 Bấm vào đây để Đăng nhập](/api/v1/auth/login)
* **Current User**: [👉 Xem thông tin User hiện tại (/api/v1/auth/me)](/api/v1/auth/me)
* **Logout**: Click nút **🚪 Logout** hoặc [👉 Bấm vào đây để Đăng xuất](/api/v1/auth/logout)"
            });

            // Tránh trùng lặp DTO model (bắt chước logic E-Verland)
            options.CustomSchemaIds(type =>
            {
                static string PrefixByModule(Type t, string baseName)
                {
                    var ns = t.Namespace ?? "";
                    if (ns.Contains("System")) return "System_" + baseName;
                    if (ns.Contains("Admin")) return "Admin_" + baseName;
                    if (ns.Contains("Staff")) return "Staff_" + baseName;
                    return baseName;
                }

                if (!type.IsGenericType)
                {
                    return PrefixByModule(type, type.Name);
                }

                var genericName = type.GetGenericTypeDefinition().Name.Split('`')[0];
                genericName = PrefixByModule(type, genericName);

                var args = string.Join("_",
                    type.GetGenericArguments().Select(a =>
                    {
                        var argBase = a.IsGenericType
                            ? a.GetGenericTypeDefinition().Name.Split('`')[0]
                            : a.Name;
                        return PrefixByModule(a, argBase);
                    }));

                return $"{genericName}_{args}";
            });

            // Sử dụng Cookie Authentication thay vì Bearer Header
            // Swagger UI khi gọi "Try it out" sẽ tự động gửi HttpOnly cookie (.Aurora.Auth) của trình duyệt.
            // Để hiển thị biểu tượng khoá bảo mật trên Swagger UI, chúng ta định nghĩa ApiKeySecurityScheme In Cookie.
            options.AddSecurityDefinition("cookieAuth", new OpenApiSecurityScheme
            {
                Name = ".Aurora.Auth",
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Cookie,
                Description = "BFF Cookie Session Authentication (.Aurora.Auth)"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "cookieAuth"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }

    public static WebApplication UseCustomSwagger(this WebApplication app, string apiTitle)
    {
        app.MapGet("/swagger-download/v1", async (HttpContext context) =>
        {
            var url = $"{context.Request.Scheme}://{context.Request.Host}/swagger/v1/swagger.json";

            using var httpClient = new HttpClient();
            var json = await httpClient.GetStringAsync(url);

            return Results.File(
                System.Text.Encoding.UTF8.GetBytes(json),
                "application/json",
                "swagger-v1.json");
        });

        app.UseSwagger();

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("./v1/swagger.json", apiTitle);
            options.RoutePrefix = "swagger";
            // Kích hoạt tính năng gửi credential (Cookie) cho "Try it out"
            options.ConfigObject.AdditionalItems["withCredentials"] = true;

            // Thêm nút Login / Logout trực tiếp trên Swagger UI
            options.HeadContent = @"
                <style>
                    .swagger-auth-container {
                        display: inline-flex;
                        align-items: center;
                        gap: 8px;
                        margin-left: 16px;
                    }
                    .swagger-btn {
                        display: inline-block;
                        padding: 6px 14px;
                        border-radius: 4px;
                        font-size: 13px;
                        font-weight: 600;
                        text-decoration: none !important;
                        cursor: pointer;
                        transition: background-color 0.2s;
                    }
                    .swagger-btn-login {
                        background-color: #4990e2;
                        color: #ffffff !important;
                    }
                    .swagger-btn-login:hover {
                        background-color: #357ae8;
                    }
                    .swagger-btn-logout {
                        background-color: #e53e3e;
                        color: #ffffff !important;
                    }
                    .swagger-btn-logout:hover {
                        background-color: #c53030;
                    }
                </style>
                <script>
                    window.addEventListener('DOMContentLoaded', function () {
                        var interval = setInterval(function () {
                            var topbar = document.querySelector('.topbar-wrapper') || document.querySelector('.information-container .title');
                            if (topbar && !document.getElementById('swagger-auth-actions')) {
                                clearInterval(interval);
                                var currentUrl = encodeURIComponent(window.location.href);
                                
                                var container = document.createElement('div');
                                container.id = 'swagger-auth-actions';
                                container.className = 'swagger-auth-container';
                                
                                var loginBtn = document.createElement('a');
                                loginBtn.className = 'swagger-btn swagger-btn-login';
                                loginBtn.href = '/api/v1/auth/login?returnUrl=' + currentUrl;
                                loginBtn.innerText = '🔑 Login (Cognito)';
                                
                                var logoutBtn = document.createElement('a');
                                logoutBtn.className = 'swagger-btn swagger-btn-logout';
                                logoutBtn.href = '/api/v1/auth/logout?returnUrl=' + currentUrl;
                                logoutBtn.innerText = '🚪 Logout';
                                
                                container.appendChild(loginBtn);
                                container.appendChild(logoutBtn);
                                topbar.appendChild(container);
                            }
                        }, 300);
                    });
                </script>";
        });

        // Hỗ trợ redirect từ /api-docs sang /swagger
        app.MapGet("/api-docs", () => Results.Redirect("/swagger"));

        return app;
    }
}
