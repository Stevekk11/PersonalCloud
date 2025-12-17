using Microsoft.AspNetCore.Identity;

namespace PersonalCloud.Models;

/// <summary>
/// Represents an application user with extended properties for the PersonalCloud application.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// Gets or sets a value indicating whether the user has a premium account.
    /// </summary>
    public bool IsPremium { get; set; }

    /// <summary>
    /// Gets or sets the time of the user's last successful login.
    /// </summary>
    public DateTime? LastLoginTime { get; set; }
}
