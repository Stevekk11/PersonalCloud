using System.Globalization;
using System.Text.RegularExpressions;

namespace PersonalCloud.Services;

/// <summary>
/// Service for fetching temperature and humidity data from a remote sensor server.
/// </summary>
public class SensorService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SensorService> _logger;
    private readonly string _sensorServerUrl;
    
    private static readonly Regex TempRegex = new(@"Temp=([\d.]+)", RegexOptions.Compiled);
    private static readonly Regex HumRegex = new(@"Humidity=([\d.]+)", RegexOptions.Compiled);

    public SensorService(HttpClient httpClient, ILogger<SensorService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _sensorServerUrl = configuration.GetValue<string>("SensorServer:Url") ?? "http://192.168.1.90:5000";
    }

    /// <summary>
    /// Fetches the latest temperature and humidity reading from the sensor server.
    /// </summary>
    /// <returns>A tuple containing temperature and humidity values, or null values if fetch fails.</returns>
    public async Task<(double? Temperature, double? Humidity)> GetLatestReadingAsync()
    {
        try
        {
            var response = await _httpClient.GetStringAsync(_sensorServerUrl);
            
            // Parse format: "Reading #{counter}: Temp={temp:.1f} , Humidity={hum:.1f}%\r\n"
            var tempMatch = TempRegex.Match(response);
            var humMatch = HumRegex.Match(response);

            double? temperature = null;
            double? humidity = null;

            if (tempMatch.Success && double.TryParse(tempMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var temp))
            {
                temperature = temp;
            }

            if (humMatch.Success && double.TryParse(humMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var hum))
            {
                humidity = hum;
            }

            return (temperature, humidity);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch sensor data from {Url}", _sensorServerUrl);
            return (null, null);
        }
    }
}
