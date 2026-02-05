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
    private readonly ILogger<PremiumCapacityService> _logger;
    private const double GigabytesPerPremiumUser = 50.0;

    public PremiumCapacityService(UserManager<ApplicationUser> userManager, ILogger<PremiumCapacityService> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public int GetMaxPremiumUsers()
    {
        double freeGB = GetAvailableDiskSpaceGB();
        var maxUsers = (int)Math.Floor(freeGB / GigabytesPerPremiumUser);
        _logger.LogDebug("Calculated max premium users: {MaxUsers} based on {FreeGB} GB free space", maxUsers, freeGB);
        return maxUsers;
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
        bool canAdd = currentPremiumCount < maxPremium;
        if (!canAdd)
        {
            _logger.LogWarning("Cannot add premium user: current count {Current} reaches or exceeds max {Max}", currentPremiumCount, maxPremium);
        }
        return canAdd;
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
