using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using System;
using MagiCloud.Configuration;
using SendGrid.Helpers.Mail;
using SendGrid;

namespace MagiCloud.Services;

public class EmailSenderService : IEmailSender
{
    private readonly ILogger _logger;
    public GeneralSettings Options { get; }

    public EmailSenderService(
        IOptions<GeneralSettings> optionsAccessor,
        ILogger<EmailSenderService> logger)
    {
        Options = optionsAccessor.Value;
        _logger = logger;
    }


    public async Task SendEmailAsync(string toEmail, string subject, string message)
    {
        if (string.IsNullOrEmpty(Options.SendGridKey))
        {
            throw new Exception("Null SendGridKey");
        }
        await Execute(Options.SendGridKey, subject, message, toEmail);
    }

    public async Task Execute(string apiKey, string subject, string message, string toEmail)
    {
        var client = new SendGridClient(apiKey);
        var msg = new SendGridMessage()
        {
            From = new EmailAddress(Options.SendGridFromAddress, Options.SendGridFromName),
            Subject = subject,
            PlainTextContent = message,
            HtmlContent = message
        };
        msg.AddTo(new EmailAddress(toEmail));

        // Disable click tracking.
        // See https://sendgrid.com/docs/User_Guide/Settings/tracking.html
        msg.SetClickTracking(false, false);
        var response = await client.SendEmailAsync(msg);
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Email to {Email} queued successfully!", toEmail);
        }
        else
        {
            _logger.LogInformation("Failure Email to {Email}", toEmail);
        }
    }
}