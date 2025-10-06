using System.Text;
using CloudCrate.Api.Middleware;
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
using CloudCrate.Application.Interfaces.Admin;
using CloudCrate.Application.Interfaces.Auth;
using CloudCrate.Application.Interfaces.Crate;
using CloudCrate.Application.Interfaces.Email;
using CloudCrate.Application.Interfaces.File;
using CloudCrate.Application.Interfaces.Folder;
using CloudCrate.Application.Interfaces.Invite;
using CloudCrate.Application.Interfaces.Permissions;
using CloudCrate.Application.Interfaces.Storage;
using CloudCrate.Application.Interfaces.User;
using CloudCrate.Application.Settings;
using CloudCrate.Infrastructure.Middleware;
using CloudCrate.Infrastructure.Services;
using CloudCrate.Infrastructure.Services.Admin;
using CloudCrate.Infrastructure.Services.Auth;
using CloudCrate.Infrastructure.Services.Crates;
using CloudCrate.Infrastructure.Services.Database;
using CloudCrate.Infrastructure.Services.Demo;
using CloudCrate.Infrastructure.Services.Email;
using CloudCrate.Infrastructure.Services.Files;
using CloudCrate.Infrastructure.Services.Folder;
using CloudCrate.Infrastructure.Services.Invites;
using CloudCrate.Infrastructure.Services.RolesAndPermissions;
using CloudCrate.Infrastructure.Services.Storage;
using CloudCrate.Infrastructure.Services.User;
using RazorLight;
using Resend;
using Scalar.AspNetCore;
using Npgsql;
using RazorLight.Razor;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
Console.WriteLine("🚀 App starting...");

// --- Dynamic port for Coolify ---
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://*:{port}");

// --- Database ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
    throw new InvalidOperationException("Database connection string is not configured!");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// --- Identity ---
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddErrorDescriber<CustomIdentityErrorDescriber>()
    .AddDefaultTokenProviders();

// --- Storage / Resend ---
builder.Services.Configure<StorageSettings>(builder.Configuration.GetSection("Storage"));
builder.Services.AddOptions();
builder.Services.AddHttpClient<ResendClient>();
builder.Services.Configure<ResendClientOptions>(o =>
{
    o.ApiToken = builder.Configuration["Resend:ApiKey"] ?? throw new Exception("Resend API key missing");
});
builder.Services.AddTransient<IResend, ResendClient>();

// --- RazorLight engine  ---
builder.Services.AddSingleton(sp =>
{
    var templateFolder = Path.Combine(AppContext.BaseDirectory, "EmailTemplates");

    var project = new FileSystemRazorProject(templateFolder);

    return new RazorLightEngineBuilder()
        .UseProject(project)
        .UseMemoryCachingProvider()
        .Build();
});


// --- JWT Authentication ---
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

// --- Controllers / JSON ---
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// --- FluentValidation ---
builder.Services.AddValidatorsFromAssemblyContaining<CreateCrateRequestValidator>();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddOpenApi();

// --- App Services ---
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICrateService, CrateService>();
builder.Services.AddScoped<ICrateMemberService, CrateMemberService>();
builder.Services.AddScoped<ICrateInviteService, CrateInviteService>();
builder.Services.AddScoped<ICrateRoleService, CrateRoleService>();
builder.Services.AddScoped<IFolderService, FolderService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IBatchMembershipService, BatchMembershipService>();
builder.Services.AddTransient<IEmailService, ResendEmailService>();
builder.Services.AddScoped<IUserInviteService, UserInviteService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<DemoService>();
builder.Services.AddScoped<DatabaseSeederService>();

// --- Minio Storage ---
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var config = builder.Configuration.GetSection("Storage").Get<StorageSettings>();
    var s3Config = new AmazonS3Config
    {
        ServiceURL = config.Endpoint,
        ForcePathStyle = true,
        UseHttp = config.Endpoint.StartsWith("http://")
    };
    return new AmazonS3Client(config.AccessKey, config.SecretKey, s3Config);
});
builder.Services.AddScoped<IStorageService, MinioStorageService>();

// --- CORS ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "https://localhost:5173",
                "http://localhost:5173",
                "https://cloudcrate.codystine.com"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders("Cross-Origin-Opener-Policy", "Cross-Origin-Embedder-Policy");
    });
});

var app = builder.Build();

// --- DATABASE MIGRATION WITH RETRY ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var context = services.GetRequiredService<AppDbContext>();

    const int maxRetries = 10;
    var retryDelay = TimeSpan.FromSeconds(5);
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            logger.LogInformation("Checking database connection...");
            using var conn = new NpgsqlConnection(connectionString);
            conn.Open();
            conn.Close();

            logger.LogInformation("Database reachable, applying migrations...");
            context.Database.Migrate();

            logger.LogInformation("Seeding main database...");
            var seeder = services.GetRequiredService<DatabaseSeederService>();
            await seeder.SeedAsync();

            // --- ENSURE MINIO BUCKET EXISTS ---
            logger.LogInformation("Ensuring MinIO bucket exists...");
            var storageService = services.GetRequiredService<IStorageService>();
            var bucketResult = await storageService.EnsureBucketExistsAsync();
            if (bucketResult.IsFailure)
            {
                logger.LogError("Failed to create MinIO bucket: {Error}", bucketResult.GetError().Message);
                throw new Exception($"MinIO bucket creation failed: {bucketResult.GetError().Message}");
            }

            logger.LogInformation("MinIO bucket ready");

            logger.LogInformation("Seeding demo accounts...");
            var demoService = services.GetRequiredService<DemoService>();
            await demoService.SeedDemoAccountsAsync();

            logger.LogInformation("Database setup complete.");
            break;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Attempt {attempt} failed, retrying in {retryDelay.TotalSeconds}s...");
            if (attempt == maxRetries) throw;
            await Task.Delay(retryDelay);
        }
    }
}

// --- OpenAPI / dev tools ---
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// --- Middleware ---
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseCookiePolicy(new CookiePolicyOptions
{
    MinimumSameSitePolicy = SameSiteMode.Strict,
    Secure = app.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always
});

app.UseCors("AllowFrontend");
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<JwtBanCheckMiddleware>();
app.UseAuthorization();
app.MapControllers();

// --- Run App ---
app.Run();