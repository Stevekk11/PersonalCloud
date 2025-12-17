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
    private readonly Helpers.Dht22SerialService _dht22Service;

    public HomeController(ILogger<HomeController> logger, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, DocumentService documentService)
    {
        _logger = logger;
        _userManager = userManager;
        _signInManager = signInManager;
        _documentService = documentService;
        _dht22Service = new Helpers.Dht22SerialService(); // Default COM3, 9600
    }

    public async Task<IActionResult> Index(string? comPort = null, bool retry = false)
    {
        if (comPort != null)
        {
            _dht22Service.ChangePort(comPort);
        }
        if (retry)
        {
            // Force a retry by closing and reopening the port
            _dht22Service.ChangePort(_dht22Service.PortName);
        }
        var model = new HomeDashboardViewModel
        {
            ServerTimeUtc = DateTime.UtcNow,
            ServerTemperature = _dht22Service.GetTemperature(),
            ServerHumidity = _dht22Service.GetHumidity(),
            SensorError = _dht22Service.LastError,
            SelectedComPort = _dht22Service.PortName,
            AvailableComPorts = SerialPort.GetPortNames().ToList()
        };

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

    [HttpPost]
    public IActionResult ChangeComPort(string comPort)
    {
        _dht22Service.ChangePort(comPort);
        return RedirectToAction("Index", new { comPort });
    }

    [HttpPost]
    public IActionResult RetrySensor(string comPort)
    {
        _dht22Service.ChangePort(comPort);
        return RedirectToAction("Index", new { comPort, retry = true });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}