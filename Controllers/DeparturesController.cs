using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using PersonalCloud.Models;
using PersonalCloud.Data;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace PersonalCloud.Controllers;

/// <summary>
/// Controller for managing public transport departures
/// </summary>
[Authorize]
public class DeparturesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly UserManager<ApplicationUser> _userManager;

    public DeparturesController(ApplicationDbContext context, IConfiguration configuration, ILogger<DeparturesController> logger, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _userManager = userManager;
    }

    /// <summary>
    /// Displays departure information for a specified stop
    /// </summary>
    /// <param name="stopName">Name of the stop to show departures for</param>
    /// <returns>View with departure information</returns>
    public IActionResult Transport(string stopName = "")
    {
        var model = BuildViewModel(stopName);
        return View(model);
    }

    /// <summary>
    /// Processes stop search form submission
    /// </summary>
    /// <param name="stopName">Name of the stop to search for</param>
    /// <returns>Redirects to Transport action with search parameters</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Search(string stopName)
    {
        _logger.LogInformation($"User {User.Identity.Name} searched for {stopName} at {DateTime.Now:o}");
        return RedirectToAction("Transport", new { stopName });
    }

    /// <summary>
    /// Builds the view model for departure information
    /// </summary>
    /// <param name="stopName">Name of the stop to fetch departures for</param>
    /// <returns>Populated DepartureViewModel instance</returns>
    private DepartureViewModel BuildViewModel(string stopName)
    {
        var vm = new DepartureViewModel
        {
            Now = DateTime.Now,
            AvailableStops = new List<string?>(),
            Placeholder = "",
        };

        try 
        {
            var stopsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "stops.json");
            if (System.IO.File.Exists(stopsPath))
            {
                var stopsJson = System.IO.File.ReadAllText(stopsPath);
                var stopsData = JObject.Parse(stopsJson);
                var stopNames = stopsData["stopGroups"]?
                    .Select(item => item["name"]?.ToString())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .ToList() ?? new List<string?>();

                vm.AvailableStops = stopNames;
                if (stopNames.Any())
                {
                    var random = new Random();
                    vm.Placeholder = stopNames[random.Next(stopNames.Count)];
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading stops.json");
        }

        if (!string.IsNullOrEmpty(stopName))
        {
            // Call Golemio API
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            var apiKey = _configuration["Golemio:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                vm.Error = "Golemio API Key is not configured.";
                return vm;
            }
            
            client.DefaultRequestHeaders.Add("X-Access-Token", apiKey);
            var url = "https://api.golemio.cz/v2/pid/departureboards";
            var query =
                $"names={Uri.EscapeDataString(stopName)}&minutesBefore=0&minutesAfter=100&timeFrom={DateTime.UtcNow:o}&includeMetroTrains=true&airCondition=true&preferredTimezone=Europe/Prague&mode=departures&order=real&filter=none&skip=canceled&limit=20";
            try
            {
                var response = client.GetAsync($"{url}?{query}").Result;
                if (response.IsSuccessStatusCode)
                {
                    var result = response.Content.ReadAsStringAsync().Result;
                    var data = JObject.Parse(result);
                    vm.Departures = (data["departures"] as JArray ?? new JArray())
                        .Select(item => new DepartureViewModel.DepartureDto
                        {
                            Route = item["route"]?["short_name"]?.ToString(),
                            Destination = item["trip"]?["headsign"]?.ToString(),
                            Minutes = int.TryParse(item["departure_timestamp"]?["minutes"]?.ToString(), out var m) ? m : 0,
                            PlatformCode = item["stop"]?["platform_code"]?.ToString(),
                            Delay = int.TryParse(item["delay"]?["minutes"]?.ToString(), out var d) ? d : 0,
                            Ac = (item["trip"]?["is_air_conditioned"]?.Type == JTokenType.Boolean)
                                ? item["trip"]["is_air_conditioned"].Value<bool>()
                                : false,
                            IsWheelchairAccessible = (item["trip"]?["is_wheelchair_accessible"]?.Type == JTokenType.Boolean)
                                ? item["trip"]["is_wheelchair_accessible"].Value<bool>()
                                : false
                        })
                        .ToList()!;
                }
                else
                {
                    vm.Error = $"API returned {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                vm.Error = "Failed to fetch departures. Please try again.";
                _logger.LogError(ex, "Failed to fetch departures for stop {stopName}", stopName);
            }

            vm.StopName = stopName;
        }

        return vm;
    }
}

