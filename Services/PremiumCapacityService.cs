using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PersonalCloud.Models;

namespace PersonalCloud.Services;

/// <summary>
/// Provides services for calculating and checking premium account capacity based on disk space.
/// </summary>
public class PremiumCapacityService : IPremiumCapacityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private const double GigabytesPerPremiumUser = 50.0;

    public PremiumCapacityService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    /// <inheritdoc/>
    public int GetMaxPremiumUsers()
    {
        double freeGB = GetAvailableDiskSpaceGB();
        return (int)Math.Floor(freeGB / GigabytesPerPremiumUser);
    }

    /// <inheritdoc/>
    public async Task<int> GetCurrentPremiumUserCountAsync()
    {
        return await _userManager.Users.CountAsync(u => u.IsPremium);
    }

    /// <inheritdoc/>
    public async Task<bool> CanAddPremiumUserAsync()
    {
        int currentPremiumCount = await GetCurrentPremiumUserCountAsync();
        int maxPremium = GetMaxPremiumUsers();
        return currentPremiumCount < maxPremium;
    }

    /// <inheritdoc/>
    public double GetAvailableDiskSpaceGB()
    {
        var pathRoot = Path.GetPathRoot(AppContext.BaseDirectory);
        if (string.IsNullOrEmpty(pathRoot))
        {
            pathRoot = "/";
        }
        
        var drive = new DriveInfo(pathRoot);
        long freeBytes = drive.AvailableFreeSpace;
        return freeBytes / (1024d * 1024d * 1024d);
    }
}
