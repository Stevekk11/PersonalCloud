using System;

namespace PersonalCloud.Models;

public class HomeDashboardViewModel
{
    public string? UserDisplayName { get; set; }

    public string? LatestFileName { get; set; }

    public DateTime? LatestFileUploadedAt { get; set; }

    public DateTime? LastLoginTime { get; set; }

    public bool IsPremium { get; set; }

    public string AccountTypeLabel => IsPremium ? "Premium" : "Free";

    public DateTime ServerTimeUtc { get; set; }
}
