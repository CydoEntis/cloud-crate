using CloudCrate.Api.Models;
using CloudCrate.Api.Validators;
using CloudCrate.Application.Common.Interfaces;
using CloudCrate.Infrastructure.Services;
using FluentValidation;
using FluentValidation.AspNetCore;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddScoped<IValidator<UploadFileRequest>, UploadFileRequestValidator>();

// Register your validators explicitly or via scanning:
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();