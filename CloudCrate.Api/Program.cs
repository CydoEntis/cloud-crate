using System.Text;
using CloudCrate.Api.Middleware;
using CloudCrate.Api.Validators;
using CloudCrate.Application.Common.Settings;
using CloudCrate.Infrastructure.Identity;
using CloudCrate.Infrastructure.Persistence;
using CloudCrate.Infrastructure.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Amazon.S3;
using CloudCrate.Api.Validators.Crate;
using CloudCrate.Application.Interfaces.Auth;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Interfaces.Email;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Application.Interfaces.Folder;
using CloudCrate.Application.Interfaces.Persistence;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Infrastructure.Services.Auth;
using CloudCrate.Infrastructure.Services.Crates;
using CloudCrate.Infrastructure.Services.Files;
using CloudCrate.Infrastructure.Services.Folder;
using CloudCrate.Infrastructure.Services.Storage;
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

builder.Services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

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
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// ✅ FluentValidation Setup
builder.Services.AddValidatorsFromAssemblyContaining<CreateCrateRequestValidator>();
builder.Services.AddFluentValidationAutoValidation();

// OpenAPI (Swagger)
builder.Services.AddOpenApi();

// Your app services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<ICrateService, CrateService>();
builder.Services.AddScoped<ICrateUserRoleService, CrateUserRolesService>();
builder.Services.AddScoped<ICrateInviteService, CrateInviteService>();
builder.Services.AddScoped<IFolderService, FolderService>();
builder.Services.AddScoped<IFileService, FileService>();
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


// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("https://localhost:5173", "http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders("Cross-Origin-Opener-Policy", "Cross-Origin-Embedder-Policy");
    });
});

var app = builder.Build();

// Dev tools
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();