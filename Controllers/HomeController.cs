using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PersonalCloud.Models;
using Microsoft.AspNetCore.Identity;
using PersonalCloud.Services;

namespace PersonalCloud.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly DocumentService _documentService;
    private readonly SensorService _sensorService;

    public HomeController(ILogger<HomeController> logger, UserManager<ApplicationUser> userManager, DocumentService documentService, SensorService sensorService)
    {
        _logger = logger;
        _userManager = userManager;
        _documentService = documentService;
        _sensorService = sensorService;
    }

    public async Task<IActionResult> Index()
    {
        _logger.LogInformation("Loading Home Dashboard for user: {User}", User.Identity?.Name ?? "Anonymous");
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
        _logger.LogInformation("Visiting Privacy page.");
        return View();
    }

    public IActionResult PidBoard()
    {
        _logger.LogInformation("Visiting PidBoard page.");
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetSensorData()
    {
        var (temperature, humidity) = await _sensorService.GetLatestReadingAsync();
        return Json(new { temperature, humidity });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        _logger.LogError("An error occurred. Request ID: {RequestId}", requestId);
        return View(new ErrorViewModel { RequestId = requestId });
    }
}