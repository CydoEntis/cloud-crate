namespace CloudCrate.Application.DTOs.Auth.Request;

public class RegisterRequest
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
}