namespace PersonalCloud.Models;

public class AdminConsoleViewModel
{
    public string? Password { get; set; }
    public bool IsAuthenticated { get; set; }
    public string? Command { get; set; }
    public string? Output { get; set; }
    public string? ErrorMessage { get; set; }
}
