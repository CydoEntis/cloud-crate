using CloudCrate.Application.Common.Models;

namespace CloudCrate.Application.Common.Interfaces;

public interface IEmailService
{
    Task<Result> SendEmailAsync(string toEmail, string subject, string templateName, object model);
}