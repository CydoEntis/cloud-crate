using System.Net;
using System.Net.Mail;
using CloudCrate.Application.Errors;
using CloudCrate.Application.Interfaces.Email;
using CloudCrate.Application.Models;
using Microsoft.Extensions.Configuration;
using RazorLight;

namespace CloudCrate.Infrastructure.Services.Storage;

public class MailtrapEmailService : IEmailService
{
    private readonly RazorLightEngine _razor;
    private readonly string _smtpServer;
    private readonly int _smtpPort;
    private readonly string _smtpUsername;
    private readonly string _smtpPassword;
    private readonly string _fromEmail;

    public MailtrapEmailService(IConfiguration configuration, RazorLightEngine razor)
    {
        _razor = razor;
        _smtpServer = configuration["Mailtrap:SmtpServer"] ?? "smtp.mailtrap.io";
        _smtpPort = int.TryParse(configuration["Mailtrap:SmtpPort"], out var port) ? port : 2525;
        _smtpUsername = configuration["Mailtrap:SmtpUsername"] ?? "";
        _smtpPassword = configuration["Mailtrap:SmtpPassword"] ?? "";
        _fromEmail = configuration["Mailtrap:FromEmail"] ?? "no-reply@example.com";
    }

    public async Task<Result> SendEmailAsync(string toEmail, string subject, string templateName, object model)
    {
        try
        {
            string html = await _razor.CompileRenderAsync($"{templateName}.cshtml", model);

            using var client = new SmtpClient(_smtpServer, _smtpPort)
            {
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                EnableSsl = true,
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(_fromEmail),
                Subject = subject,
                Body = html,
                IsBodyHtml = true,
            };

            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(new EmailSendError($"Failed to send email: {ex.Message}"));
        }
    }
}
