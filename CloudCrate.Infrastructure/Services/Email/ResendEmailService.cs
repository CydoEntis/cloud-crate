using CloudCrate.Application.Errors;
using CloudCrate.Application.Interfaces.Email;
using CloudCrate.Application.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RazorLight;
using Resend;

namespace CloudCrate.Infrastructure.Services.Email;

public class ResendEmailService : IEmailService
{
    private readonly IResend _resend;
    private readonly RazorLightEngine _razor;
    private readonly ILogger<ResendEmailService> _logger;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public ResendEmailService(
        IResend resend,
        RazorLightEngine razor,
        IConfiguration configuration,
        ILogger<ResendEmailService> logger)
    {
        _resend = resend;
        _razor = razor;
        _logger = logger;
        _fromEmail = configuration["Resend:FromEmail"] ?? "noreply@cloudcrate.codystine.com";
        _fromName = configuration["Resend:FromName"] ?? "CloudCrate";
    }

    public async Task<Result> SendEmailAsync(string toEmail, string subject, string templateName, object model)
    {
        try
        {
            string html = await _razor.CompileRenderAsync($"{templateName}.cshtml", model);

            var message = new EmailMessage
            {
                From = $"{_fromName} <{_fromEmail}>",
                To = toEmail,
                Subject = subject,
                HtmlBody = html
            };

            await _resend.EmailSendAsync(message);

            _logger.LogInformation("Email sent successfully to {ToEmail}", toEmail);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToEmail}", toEmail);
            return Result.Failure(new EmailSendError($"Failed to send email: {ex.Message}"));
        }
    }
}