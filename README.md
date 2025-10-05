# CloudCrate API

Backend for CloudCrate - a collaborative cloud storage platform built with .NET 8 and Clean Architecture.

## Tech Stack

- .NET 8 Web API
- PostgreSQL + Entity Framework Core
- MinIO (S3-compatible object storage)
- ASP.NET Core Identity + JWT
- FluentValidation

## Features

- Role-based file sharing (Owner, Manager, Member)
- Storage quota management with tiered plans
- Soft delete with trash recovery
- Presigned URLs for secure file access
- Demo account with auto-reset functionality

## Architecture
CloudCrate/
├── CloudCrate.Api/              # Controllers, Middleware
├── CloudCrate.Application/      # DTOs, Interfaces, Validators
├── CloudCrate.Domain/           # Entities, Value Objects
└── CloudCrate.Infrastructure/   # Data access, Services

## Setup

### Prerequisites
- .NET 8 SDK
- PostgreSQL 15+
- Docker (for MinIO)

### Database
```sql
CREATE DATABASE cloud_crate;
```

### Update appsettings.Development.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=cloud_crate;Username=postgres;Password=yourpass"
  },
  "Jwt": {
    "Key": "your-super-secret-key-minimum-32-characters",
    "Issuer": "CloudCrateApi",
    "Audience": "CloudCrateClient"
  },
  "Storage": {
    "Endpoint": "http://localhost:9000",
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin"
  }
}
```
### Migrations & Run
dotnet ef database update
dotnet run

## Documentation
API available at https://localhost:7295
Swagger docs at https://localhost:7295/scalar/v1


## Demo Account
Email: demo@cloudcrate.com
Password: Demo123!

## Key Endpoints
```txt
POST /api/auth/login - Authentication
GET /api/crates - List crates
POST /api/files/upload - Upload file
GET /api/files/{id}/download - Download file
```
License
MIT
