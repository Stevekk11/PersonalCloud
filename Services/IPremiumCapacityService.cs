namespace PersonalCloud.Services;

/// <summary>
/// Defines methods for calculating and checking premium account capacity based on disk space.
/// </summary>
public interface IPremiumCapacityService
{
    /// <summary>
    /// Gets the maximum number of premium users allowed based on available disk space.
    /// </summary>
    /// <returns>The maximum number of premium users.</returns>
    int GetMaxPremiumUsers();

    /// <summary>
    /// Gets the current number of premium users.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the number of premium users.</returns>
    Task<int> GetCurrentPremiumUserCountAsync();

    /// <summary>
    /// Checks if a new premium user can be added based on available disk space.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result indicates whether a new premium user can be added.</returns>
    Task<bool> CanAddPremiumUserAsync();

    /// <summary>
    /// Gets the available disk space in gigabytes.
    /// </summary>
    /// <returns>The available disk space in GB.</returns>
    double GetAvailableDiskSpaceGB();
}
