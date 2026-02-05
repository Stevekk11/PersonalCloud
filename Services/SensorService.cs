using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace PersonalCloud.Services;

/// <summary>
/// Service for fetching temperature and humidity data from a remote sensor server.
/// </summary>
public class SensorService
{
    private readonly ILogger<SensorService> _logger;
    private readonly string _sensorServerUrl;
    private bool _isSensorDisabled = false;
    
    private static readonly Regex TempRegex = new(@"Temp=([\d.]+)", RegexOptions.Compiled);
    private static readonly Regex HumRegex = new(@"Humidity=([\d.]+)", RegexOptions.Compiled);

    public SensorService(ILogger<SensorService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _sensorServerUrl = configuration.GetValue<string>("SensorServer:Url") ?? "http://192.168.1.90:5000";
    }

    /// <summary>
    /// Fetches the latest temperature and humidity reading from the sensor server.
    /// </summary>
    /// <returns>A tuple containing temperature and humidity values, or null values if fetch fails.</returns>
    public async Task<(double? Temperature, double? Humidity)> GetLatestReadingAsync()
    {
        if (_isSensorDisabled)
        {
            return (null, null);
        }

        try
        {
            var uri = new Uri(_sensorServerUrl);
            using (var client = new TcpClient())
            {
                // Set a reasonable timeout for the connection attempt (e.g., 2 seconds)
                var connectTask = client.ConnectAsync(uri.Host, uri.Port);
                var timeoutTask = Task.Delay(2000);

                if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
                {
                     throw new TimeoutException("Connection to sensor timed out.");
                }
                await connectTask; // Ensure any exception from ConnectAsync is thrown

                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream))
                {
                    var response = await reader.ReadLineAsync();
                    
                    if (response == null)
                    {
                        return (null, null);
                    }
                    
                    // Parse format: "Reading #{counter}: Temp={temp:.1f} , Humidity={hum:.1f}%"
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
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch sensor data from {Url}. Sensor will be disabled until restart.", _sensorServerUrl);
            _isSensorDisabled = true;
            return (null, null);
        }
    }
}
