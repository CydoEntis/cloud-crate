using System.Text;
using CloudCrate.Api.Middleware;
using CloudCrate.Application.Common.Settings;
using CloudCrate.Infrastructure.Identity;
using CloudCrate.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text.Json;
using Amazon.S3;
using CloudCrate.Api.Validators.Crate;
using CloudCrate.Application.Interfaces;
using CloudCrate.Application.Interfaces.Auth;
using CloudCrate.Application.Interfaces.Bulk;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Interfaces.Email;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Application.Interfaces.Folder;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Application.Interfaces.Transactions;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Infrastructure.Services;
using CloudCrate.Infrastructure.Services.Auth;
using CloudCrate.Infrastructure.Services.Bulk;
using CloudCrate.Infrastructure.Services.Crates;
using CloudCrate.Infrastructure.Services.Files;
using CloudCrate.Infrastructure.Services.Folder;
using CloudCrate.Infrastructure.Services.RolesAndPermissions;
using CloudCrate.Infrastructure.Services.Storage;
using CloudCrate.Infrastructure.Services.User;
using RazorLight;
using Resend;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
Console.WriteLine("🚀 App starting...");

// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity setup
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddErrorDescriber<CustomIdentityErrorDescriber>()
    .AddDefaultTokenProviders();

// Configure StorageSettings
builder.Services.Configure<StorageSettings>(
    builder.Configuration.GetSection("Storage"));

// Configure Resend Settings
builder.Services.AddOptions();
builder.Services.AddHttpClient<ResendClient>();
builder.Services.Configure<ResendClientOptions>(o =>
{
    o.ApiToken = builder.Configuration["Resend:ApiKey"] ?? throw new Exception("Resend API key missing");
});
builder.Services.AddTransient<IResend, ResendClient>();

// Setup RazorLightEngine
builder.Services.AddSingleton(sp =>
    new RazorLightEngineBuilder()
        .UseFileSystemProject(Path.Combine(Directory.GetCurrentDirectory(), "..", "CloudCrate.Infrastructure",
            "EmailTemplates"))
        .UseMemoryCachingProvider()
        .Build());

// === Authentication & JWT Setup ===
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role,
        };
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine("Token validated successfully.");
                return Task.CompletedTask;
            }
        };
    });

// Controllers & JSON
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// ✅ FluentValidation Setup
builder.Services.AddValidatorsFromAssemblyContaining<CreateCrateRequestValidator>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddOpenApi();

// App services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICrateService, CrateService>();
builder.Services.AddScoped<ICrateMemberService, CrateMemberService>();
builder.Services.AddScoped<ICrateInviteService, CrateInviteService>();
builder.Services.AddScoped<ICrateRoleService, CrateRoleService>();
builder.Services.AddScoped<IFolderService, FolderService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IBulkService, BulkService>();
builder.Services.AddScoped<IBatchDeleteService, BatchDeleteService>();
builder.Services.AddScoped<IBatchMembershipService, BatchMembershipService>();
builder.Services.AddTransient<IEmailService, MailtrapEmailService>();

// Registering Minio Storage
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var config = builder.Configuration.GetSection("Storage").Get<StorageSettings>();
    var s3Config = new AmazonS3Config
    {
        ServiceURL = config.Endpoint,
        ForcePathStyle = true, // required for MinIO
        UseHttp = config.Endpoint.StartsWith("http://")
    };
    return new AmazonS3Client(config.AccessKey, config.SecretKey, s3Config);
});
builder.Services.AddScoped<IStorageService, MinioStorageService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "https://localhost:5173", 
                "http://localhost:5173" 
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials() 
            .WithExposedHeaders("Cross-Origin-Opener-Policy", "Cross-Origin-Embedder-Policy");
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Cookie policy for HTTPS cookies
app.UseCookiePolicy(new CookiePolicyOptions
{
    MinimumSameSitePolicy = SameSiteMode.Strict,
    Secure = app.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always
});

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();