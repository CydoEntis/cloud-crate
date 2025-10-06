# 🌩️ CloudCrate API

Backend for **CloudCrate** — a collaborative cloud storage platform built with **.NET** and **Clean Architecture**.  

![CloudCrate Screenshot](./docs/demo.gif)  

---

## 🚀 Tech Stack

- **Backend:** .NET 8 Web API  
- **Database:** PostgreSQL + Entity Framework Core  
- **Storage:** MinIO (S3-compatible object storage)  
- **Authentication:** ASP.NET Core Identity + JWT  
- **Validation:** FluentValidation  

---

## ✨ Features

- Role-based file sharing (Owner, Manager, Member)  
- Storage quota management with tiered plans  
- Soft delete with trash recovery  
- Presigned URLs for secure file access  
- Demo account with auto-reset functionality  

---

## 🏗 Architecture

```bash
CloudCrate/
├── CloudCrate.Api/ # Controllers, Middleware
├── CloudCrate.Application/ # DTOs, Interfaces, Validators
├── CloudCrate.Domain/ # Entities, Value Objects
└── CloudCrate.Infrastructure/ # Data access, Services
```


Follows **Clean Architecture** principles for separation of concerns and maintainability.

---

## ⚙️ Setup

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)  
- PostgreSQL 15+  
- Docker (for MinIO)  

### Database

```sql
CREATE DATABASE cloud_crate;
```

```bash
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

```bash
dotnet ef database update
dotnet run
```

📚 Documentation
```bash
API base URL: https://localhost:7295
Swagger docs: https://localhost:7295/scalar/v1
```

👤 Demo Account
```bash
Email: demo@cloudcrate.com
Password: Demo123!
```

🔑 Key Endpoints
```bash
Method	Endpoint	Description
POST	/api/auth/login	Authentication
GET	/api/crates	List crates
POST	/api/files/upload	Upload file
GET	/api/files/{id}/download	Download file
```

📝 License
MIT

