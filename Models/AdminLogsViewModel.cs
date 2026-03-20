namespace PersonalCloud.Models;

public class AdminLogsViewModel
{
    public string? Password { get; set; }
    public bool IsAuthenticated { get; set; }
    public string? LogContent { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> LogFiles { get; set; } = new();
    public string? SelectedFile { get; set; }
}
