namespace BuildingBlocks.BFF.Options;

/// <summary>
/// Cấu hình Cookie cho auth session.
/// Bind từ section "Auth:Cookie" trong appsettings / environment variables.
/// </summary>
public class AuthCookieOptions
{
    public const string SectionName = "Auth:Cookie";

    /// <summary>true cho production (HTTPS), false cho dev (HTTP).</summary>
    public bool Secure { get; set; } = true;

    /// <summary>
    /// Cookie domain cho subdomain sharing.
    /// VD: ".yourdomain.vn" → cookie chia sẻ giữa app.yourdomain.vn, api.yourdomain.vn.
    /// Để null/trống thì dùng domain hiện tại.
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>Thời gian buffer trước khi token hết hạn để trigger refresh (phút).</summary>
    public int TokenRefreshBufferMinutes { get; set; } = 5;

    /// <summary>Thời gian session timeout tối đa (phút). Mặc định 8 giờ.</summary>
    public int SessionTimeoutMinutes { get; set; } = 480;
}

// Backward compatibility alias nếu có
public class AuthCookieConfig : AuthCookieOptions;
