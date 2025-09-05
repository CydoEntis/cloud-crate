using CloudCrate.Application.Models;

namespace CloudCrate.Application.Interfaces.Email;

public interface IEmailService
{
    Task<Result> SendEmailAsync(string toEmail, string subject, string templateName, object model);
}