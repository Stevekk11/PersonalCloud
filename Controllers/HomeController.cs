using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PersonalCloud.Models;
using Microsoft.AspNetCore.Identity;
using PersonalCloud.Services;
using System.IO.Ports;

namespace PersonalCloud.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly DocumentService _documentService;
    private readonly SensorService _sensorService;

    public HomeController(ILogger<HomeController> logger, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, DocumentService documentService, SensorService sensorService)
    {
        _logger = logger;
        _userManager = userManager;
        _signInManager = signInManager;
        _documentService = documentService;
        _sensorService = sensorService;
    }

    public async Task<IActionResult> Index(string? comPort = null, bool retry = false)
    {
        var model = new HomeDashboardViewModel
        {
            ServerTimeUtc = DateTime.UtcNow,
        };

        // Fetch sensor data
        var (temperature, humidity) = await _sensorService.GetLatestReadingAsync();
        model.Temperature = temperature;
        model.Humidity = humidity;

        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                model.UserDisplayName = user.UserName ?? user.Email ?? user.Id;
                model.IsPremium = user.IsPremium;
                model.LastLoginTime = user.LastLoginTime;

                var latestDoc = await _documentService.GetLatestUserDocumentAsync(user.Id);
                if (latestDoc != null)
                {
                    model.LatestFileName = latestDoc.FileName;
                    model.LatestFileUploadedAt = latestDoc.UploadedAt;
                }
            }
        }

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult PidBoard()
    {
        return View();
    }


    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}