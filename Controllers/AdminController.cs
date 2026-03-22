using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace PersonalCloud.Controllers;

public class AdminController : Controller
{
    private const string SessionKey = "AdminLogsAuthenticated";
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminController> _logger;
    private readonly string _logsDirectory;
    private readonly string _workDirectory;

    public AdminController(IConfiguration configuration, ILogger<AdminController> logger, IWebHostEnvironment env)
    {
        _configuration = configuration;
        _logger = logger;
        _logsDirectory = Path.Combine(env.ContentRootPath, "Logs");
        _workDirectory = Path.Combine(env.ContentRootPath);
    }

    [HttpGet]
    public IActionResult Logs(string? selectedFile = null)
    {
        var model = new Models.AdminLogsViewModel();

        if (HttpContext.Session.GetString(SessionKey) != "true")
            return View(model);

        model.IsAuthenticated = true;
        PopulateLogContent(model, selectedFile);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logs(Models.AdminLogsViewModel model)
    {
        var adminPassword = _configuration["AdminSettings:Password"];

        if (string.IsNullOrEmpty(adminPassword))
        {
            model.ErrorMessage = "Admin password is not configured.";
            return View(model);
        }

        var inputBytes = Encoding.UTF8.GetBytes(model.Password ?? string.Empty);
        var expectedBytes = Encoding.UTF8.GetBytes(adminPassword);

        if (!CryptographicOperations.FixedTimeEquals(inputBytes, expectedBytes))
        {
            _logger.LogWarning("Failed admin log access attempt from IP: {IP}", HttpContext.Connection.RemoteIpAddress);
            model.ErrorMessage = "Invalid password.";
            model.Password = null;
            return View(model);
        }

        HttpContext.Session.SetString(SessionKey, "true");
        return RedirectToAction(nameof(Logs), new { selectedFile = model.SelectedFile });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Remove(SessionKey);
        return RedirectToAction(nameof(Logs));
    }

    [HttpGet]
    public IActionResult Console()
    {
        var model = new Models.AdminConsoleViewModel();

        if (HttpContext.Session.GetString(SessionKey) != "true")
            return View(model);

        model.IsAuthenticated = true;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Console(Models.AdminConsoleViewModel model)
    {
        if (HttpContext.Session.GetString(SessionKey) != "true")
        {
            var adminPassword = _configuration["AdminSettings:Password"];
            if (string.IsNullOrEmpty(adminPassword))
            {
                model.ErrorMessage = "Admin password is not configured.";
                return View(model);
            }

            var inputBytes = Encoding.UTF8.GetBytes(model.Password ?? string.Empty);
            var expectedBytes = Encoding.UTF8.GetBytes(adminPassword);

            if (!CryptographicOperations.FixedTimeEquals(inputBytes, expectedBytes))
            {
                _logger.LogWarning("Failed admin console access attempt from IP: {IP}", HttpContext.Connection.RemoteIpAddress);
                model.ErrorMessage = "Invalid password.";
                model.Password = null;
                return View(model);
            }

            HttpContext.Session.SetString(SessionKey, "true");
        }

        model.IsAuthenticated = true;

        if (!string.IsNullOrWhiteSpace(model.Command))
        {
            try
            {
                model.Output = await ExecuteBashCommand(model.Command);
            }
            catch (Exception ex)
            {
                model.Output = $"Error: {ex.Message}";
            }
        }

        return View(model);
    }

    private async Task<string> ExecuteBashCommand(string command)
    {
        var isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
        var fileName = isWindows ? "cmd.exe" : "/bin/bash";
        var arguments = isWindows ? $"/c {command}" : $"-c \"{command}\"";

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _workDirectory
            }
        };

        var output = new StringBuilder();
        process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        return output.ToString();
    }

    private void PopulateLogContent(Models.AdminLogsViewModel model, string? selectedFile)
    {
        if (!Directory.Exists(_logsDirectory))
        {
            model.LogContent = "No log directory found.";
            return;
        }

        model.LogFiles = Directory.GetFiles(_logsDirectory, "*.log")
            .OrderByDescending(f => new FileInfo(f).LastWriteTime)
            .Select(f => Path.GetFileName(f))
            .ToList();

        var safeName = !string.IsNullOrEmpty(selectedFile)
            ? Path.GetFileName(selectedFile)
            : model.LogFiles.FirstOrDefault();

        model.SelectedFile = safeName;

        if (string.IsNullOrEmpty(safeName))
        {
            model.LogContent = "No log files found.";
            return;
        }

        var fullPath = Path.GetFullPath(Path.Combine(_logsDirectory, safeName));
        if (!fullPath.StartsWith(Path.GetFullPath(_logsDirectory) + Path.DirectorySeparatorChar)
            && fullPath != Path.GetFullPath(_logsDirectory))
        {
            model.LogContent = "Invalid file path.";
            return;
        }

        if (!System.IO.File.Exists(fullPath))
        {
            model.LogContent = "Log file not found.";
            return;
        }

        try
        {
            model.LogContent = ReadLastLines(fullPath, 500);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading log file: {File}", safeName);
            model.LogContent = "Error reading log file.";
        }
    }

    private static string ReadLastLines(string path, int lineCount)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);
        var lines = new Queue<string>(lineCount + 1);
        while (reader.ReadLine() is { } line)
        {
            lines.Enqueue(line);
            if (lines.Count > lineCount)
                lines.Dequeue();
        }
        return string.Join('\n', lines);
    }
}
