using CloudCrate.Application.Common.Errors;
using Resend;
using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Application.Common.Models;
using Microsoft.Extensions.Configuration;
using RazorLight;

public class ResendEmailService : IEmailService
{
    private readonly IResend _resend;
    private readonly RazorLightEngine _razor;
    private readonly string _fromEmail;

    public ResendEmailService(IResend resend, IConfiguration config, RazorLightEngine razor)
    {
        _resend = resend;
        _razor = razor;
        _fromEmail = config["Resend:FromEmail"] ?? "noreply@cloudcrate.codystine.com";
    }

    public async Task<Result> SendEmailAsync(string toEmail, string subject, string templateName, object model)
    {
        try
        {
            string html = await _razor.CompileRenderAsync($"{templateName}.cshtml", model);

            var message = new EmailMessage
            {
                From = _fromEmail,
                Subject = subject,
                HtmlBody = html
            };
            message.To.Add(toEmail);

            await _resend.EmailSendAsync(message);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("EMAIL_SEND_ERROR", ex.Message));
        }
    }
}