// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PersonalCloud.Models;
using PersonalCloud.Services;

namespace PersonalCloud.Areas.Identity.Pages.Account.Manage
{
    public class PremiumModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPremiumCapacityService _premiumCapacityService;
        private readonly ILogger<PremiumModel> _logger;

        public PremiumModel(
            UserManager<ApplicationUser> userManager,
            IPremiumCapacityService premiumCapacityService,
            ILogger<PremiumModel> logger)
        {
            _userManager = userManager;
            _premiumCapacityService = premiumCapacityService;
            _logger = logger;
        }

        [TempData]
        public string StatusMessage { get; set; }

        public bool IsPremium { get; set; }
        public bool CanUpgradeToPremium { get; set; }
        public double AvailableDiskSpaceGB { get; set; }
        public int MaxPremiumUsers { get; set; }
        public int CurrentPremiumUsers { get; set; }
        public int AvailablePremiumSlots { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            await LoadAsync(user);
            return Page();
        }

        private async Task LoadAsync(ApplicationUser user)
        {
            IsPremium = user.IsPremium;
            AvailableDiskSpaceGB = _premiumCapacityService.GetAvailableDiskSpaceGB();
            MaxPremiumUsers = _premiumCapacityService.GetMaxPremiumUsers();
            CurrentPremiumUsers = await _premiumCapacityService.GetCurrentPremiumUserCountAsync();
            AvailablePremiumSlots = Math.Max(0, MaxPremiumUsers - CurrentPremiumUsers);
            CanUpgradeToPremium = await _premiumCapacityService.CanAddPremiumUserAsync();
        }

        public async Task<IActionResult> OnPostUpgradeAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (user.IsPremium)
            {
                StatusMessage = "You are already a premium user.";
                return RedirectToPage();
            }

            // Re-check capacity to prevent race conditions
            if (!await _premiumCapacityService.CanAddPremiumUserAsync())
            {
                _logger.LogWarning("Premium upgrade failed for user {UserId}: No slots available.", user.Id);
                StatusMessage = "Error: No premium slots available. Insufficient disk space.";
                return RedirectToPage();
            }

            user.IsPremium = true;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                _logger.LogError("Failed to update user {UserId} to premium. Errors: {Errors}", user.Id, string.Join(", ", result.Errors.Select(e => e.Description)));
                StatusMessage = "Error: Failed to upgrade to premium. Please try again.";
                return RedirectToPage();
            }

            _logger.LogInformation("User {UserId} upgraded to premium.", user.Id);
            StatusMessage = "Congratulations! You have been upgraded to Premium!";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDowngradeAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!user.IsPremium)
            {
                StatusMessage = "You are not a premium user.";
                return RedirectToPage();
            }

            user.IsPremium = false;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                _logger.LogError("Failed to update user {UserId} (downgrade). Errors: {Errors}", user.Id, string.Join(", ", result.Errors.Select(e => e.Description)));
                StatusMessage = "Error: Failed to downgrade from premium. Please try again.";
                return RedirectToPage();
            }

            _logger.LogInformation("User {UserId} downgraded from premium.", user.Id);
            StatusMessage = "You have been downgraded from Premium.";
            return RedirectToPage();
        }
    }
}
