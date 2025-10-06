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
            string html;
            try
            {
                html = await _razor.CompileRenderAsync(templateName, model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to render email template {TemplateName}", templateName);
                return Result.Failure(new EmailSendError($"Template rendering failed: {ex.Message}"));
            }

            var message = new EmailMessage
            {
                From = $"{_fromName} <{_fromEmail}>",
                To = toEmail,
                Subject = subject,
                HtmlBody = html,
                TextBody = $"Hello,\n\nPlease view this email in an HTML-capable client.\nSubject: {subject}"
            };

            var response = await _resend.EmailSendAsync(message);

            _logger.LogInformation("Email sent to {ToEmail}. Resend response: {@Response}", toEmail, response);

            if (string.IsNullOrWhiteSpace(response.ToString()))
            {
                _logger.LogWarning("Email was sent but no Resend message ID was returned for {ToEmail}", toEmail);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error sending email to {ToEmail} with template {TemplateName} and model {@Model}",
                toEmail, templateName, model);
            return Result.Failure(new EmailSendError($"Failed to send email: {ex.Message}"));
        }
    }
}