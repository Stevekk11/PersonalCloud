using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PersonalCloud.Models;
using PersonalCloud.Services;

namespace PersonalCloud.ViewComponents;

/// <summary>
/// ViewComponent that displays the current user's storage usage as a progress bar.
/// </summary>
public class StorageUsageViewComponent : ViewComponent
{
    private readonly DocumentService _documentService;
    private readonly UserManager<ApplicationUser> _userManager;

    public StorageUsageViewComponent(DocumentService documentService, UserManager<ApplicationUser> userManager)
    {
        _documentService = documentService;
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Content(string.Empty);
        }

        var user = await _userManager.GetUserAsync(HttpContext.User);
        if (user == null)
        {
            return Content(string.Empty);
        }

        var usedBytes = await _documentService.GetUserStorageUsedAsync(user.Id);

                // Use different quota for premium vs. regular users
                var maxBytes = user.IsPremium
                    ? DocumentService.MaxStoragePerPremiumUser
                    : DocumentService.MaxStoragePerUser;

        var percentageUsed = (double)usedBytes / maxBytes * 100;

        var model = new StorageUsageViewModel
        {
            UsedBytes = usedBytes,
            MaxBytes = maxBytes,
            PercentageUsed = percentageUsed,
            UsedFormatted = FormatFileSize(usedBytes),
            MaxFormatted = FormatFileSize(maxBytes)
        };

        return View(model);
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>
/// View model for the storage usage view component.
/// </summary>
public class StorageUsageViewModel
{
    public long UsedBytes { get; set; }
    public long MaxBytes { get; set; }
    public double PercentageUsed { get; set; }
    public string UsedFormatted { get; set; } = string.Empty;
    public string MaxFormatted { get; set; } = string.Empty;
}
