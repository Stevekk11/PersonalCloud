
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Identity.UI.Services;
using MimeKit;

namespace PersonalCloud.Helpers;

/// <summary>
/// Provides functionality to send emails using the SMTP protocol.
/// </summary>
/// <remarks>
/// This class is an implementation of the <see cref="Microsoft.AspNetCore.Identity.UI.Services.IEmailSender"/> interface.
/// It utilizes the SMTP protocol to send emails, with configurable settings supplied through the application configuration.
/// </remarks>
public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;

    public SmtpEmailSender(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        using (var client = new SmtpClient())
        {
            await client.ConnectAsync(
                _configuration["Email:Smtp:Host"],
                int.Parse(_configuration["Email:Smtp:Port"] ?? throw new InvalidOperationException("Port cannot be null.")));

            await client.AuthenticateAsync(
                _configuration["Email:Smtp:Username"],
                _configuration["Email:Smtp:Password"]);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Cloudové úložiště - potvrzení emailu", _configuration["Email:From"]));
            message.To.Add(new MailboxAddress(email, email));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlMessage + "<br><br><p style='color: #666; font-size: 12px;'>Děkujeme, že používáte naše cloudové úložiště.<br>S přáním hezkého dne,<br>Stevek 😘😘</p>" };

            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}