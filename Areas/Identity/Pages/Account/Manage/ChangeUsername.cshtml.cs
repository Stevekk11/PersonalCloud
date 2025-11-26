// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PersonalCloud.Models;

namespace PersonalCloud.Areas.Identity.Pages.Account.Manage
{
    public class ChangeUsernameModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public ChangeUsernameModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Display(Name = "Current username")]
            public string CurrentUsername { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 3)]
            [RegularExpression(@"^[a-zA-Z0-9_\-\.]+$", ErrorMessage = "Username can only contain letters, numbers, underscores, hyphens, and periods.")]
            [Display(Name = "New username")]
            public string NewUsername { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            Input = new InputModel
            {
                CurrentUsername = user.UserName
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                Input = new InputModel
                {
                    CurrentUsername = user.UserName
                };
                return Page();
            }

            // Check if the new username is the same as the current one
            if (user.UserName == Input.NewUsername)
            {
                StatusMessage = "Your username is unchanged.";
                return RedirectToPage();
            }

            // Check if the username is already taken
            var existingUser = await _userManager.FindByNameAsync(Input.NewUsername);
            if (existingUser != null && existingUser.Id != user.Id)
            {
                ModelState.AddModelError(string.Empty, "Username is already taken.");
                Input = new InputModel
                {
                    CurrentUsername = user.UserName
                };
                return Page();
            }

            var setUserNameResult = await _userManager.SetUserNameAsync(user, Input.NewUsername);
            if (!setUserNameResult.Succeeded)
            {
                foreach (var error in setUserNameResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                Input = new InputModel
                {
                    CurrentUsername = user.UserName
                };
                return Page();
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Your username has been updated.";
            return RedirectToPage();
        }
    }
}
