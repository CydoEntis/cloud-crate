﻿using CloudCrate.Application.Common.Models;
using CloudCrate.Application.DTOs.Auth;

namespace CloudCrate.Application.Common.Interfaces;

public interface IAuthService
{
    Task<Result> RegisterAsync(string email, string password);
    Task<Result<string>> LoginAsync(string email, string password);
    Task<Result<UserResponse>> GetUserByIdAsync(string userId);
}