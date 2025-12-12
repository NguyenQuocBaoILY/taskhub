# TaskHub Backend

Backend API for TaskHub project management application, built with .NET 8 and AWS services.

## Table of Contents

- [Technologies Used](#technologies-used)
- [Project Architecture](#project-architecture)
- [System Requirements](#system-requirements)
- [Installation and Running the Project](#installation-and-running-the-project)
- [Configuration](#configuration)
- [API Endpoints](#api-endpoints)
- [Deployment](#deployment)

## Technologies Used

### Language & Framework
- **C# / .NET 8.0** - Main programming language
- **ASP.NET Core Web API** - REST API framework
- **Entity Framework Core 9.0** - ORM

### AWS Services
- **AWS Lambda** - Serverless compute (production deployment)
- **Amazon DynamoDB** - NoSQL Database
- **Amazon S3** - File storage (uploads)
- **Amazon Cognito** - User authentication
- **AWS Secrets Manager** - Secrets/credentials management

### Authentication & Security
- **JWT (JSON Web Token)** - Token-based authentication
- **Google OAuth 2.0** - Google login
- **BCrypt** - Password encryption

### Other Tools
- **Swagger/OpenAPI** - API documentation
- **GitLab CI/CD** - Automated build and deployment

## Project Architecture

```
taskhub_backend/
├── WebAPI/
│   └── Web_API.AppHost/          # API Layer - Controllers & Entry points
│       ├── Controller/           # API Controllers
│       ├── Program.cs            # Application startup
│       ├── appsettings.json      # Configuration
│       └── wwwroot/uploads/      # Local file storage
│
├── BLL/                          # Business Logic Layer
│   ├── DTOs/                     # Data Transfer Objects
│   └── Services/                 # Business Services
│
├── DAL/                          # Data Access Layer
│   ├── Entities/                 # Database Models
│   ├── Interfaces/               # Repository Interfaces
│   └── Repositories/             # Repository Implementations
│
├── Infrastructure/               # Infrastructure Layer
│   └── Storage/                  # File Storage (Local/S3)
│
└── Taskhub.sln                   # Solution file
```

## System Requirements

### Required
- **.NET 8.0 SDK** or higher
- **Visual Studio 2022** or **VS Code** with C# extension
- **Git**

### Optional (for local development)
- **AWS CLI** - If you want to test with AWS services
- **DynamoDB Local** - If you want to test database locally

## Installation and Running the Project

### Step 1: Clone repository

```bash
git clone https://gitlab.com/taskhub_group/taskhub_backend.git
cd taskhub_backend
```

### Step 2: Install .NET SDK

Download and install .NET 8.0 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0

Verify installation:
```bash
dotnet --version
```

### Step 3: Restore packages

```bash
dotnet restore
```

### Step 4: Configure appsettings

Open `WebAPI/Web_API.AppHost/appsettings.json` and update the following values:

```json
{
  "AWS": {
    "Region": "ap-southeast-1",
    "AccessKey": "YOUR_AWS_ACCESS_KEY",
    "SecretKey": "YOUR_AWS_SECRET_KEY",
    "S3": {
      "BucketName": "your-s3-bucket-name"
    },
    "Cognito": {
      "UserPoolId": "YOUR_USER_POOL_ID",
      "ClientId": "YOUR_CLIENT_ID",
      "ClientSecret": "YOUR_CLIENT_SECRET"
    }
  },
  "JWT": {
    "SecretKey": "your-secret-key-at-least-32-characters-long",
    "Issuer": "TaskHub",
    "Audience": "TaskHub"
  },
  "GoogleAuth": {
    "ClientId": "YOUR_GOOGLE_CLIENT_ID",
    "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET"
  },
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": "587",
    "FromName": "TaskHub",
    "SmtpPassword": "YOUR_EMAIL_APP_PASSWORD"
  },
  "App": {
    "FrontendUrl": "http://localhost:5173"
  }
}
```

### Step 5: Run the project

#### Using CLI:
```bash
cd WebAPI/Web_API.AppHost
dotnet run
```

#### Using Visual Studio:
1. Open `Taskhub.sln` file
2. Set `API` project as Startup Project
3. Press `F5` or click **Run** button

#### Using VS Code:
1. Open project folder in VS Code
2. Open Terminal and run:
```bash
dotnet run --project WebAPI/Web_API.AppHost/API.csproj
```

### Step 6: Access API

- **Swagger UI**: https://localhost:5001/swagger or http://localhost:5000/swagger
- **API Base URL**: https://localhost:5001 or http://localhost:5000

## Configuration

### Development Environment

`appsettings.Development.json` file is used when running in development environment:

```json
{
  "AWS": {
    "UseDynamoDbLocal": false,
    "Region": "ap-southeast-1"
  }
}
```

### Production Environment

`appsettings.Production.json` file is used during deployment:
- Uses AWS Secrets Manager to store credentials
- Uses Lambda IAM Role instead of Access Keys

### Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `ASPNETCORE_ENVIRONMENT` | Environment (Development/Production) | Yes |
| `AWS_ACCESS_KEY_ID` | AWS Access Key | Yes (Dev) |
| `AWS_SECRET_ACCESS_KEY` | AWS Secret Key | Yes (Dev) |
| `AWS_DEFAULT_REGION` | AWS Region | Yes |

## API Endpoints

### Authentication
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/register` | Register account |
| POST | `/api/auth/login` | Login |
| POST | `/api/auth/google` | Login with Google |
| POST | `/api/auth/refresh-token` | Refresh token |
| POST | `/api/auth/forgot-password` | Forgot password |
| POST | `/api/auth/reset-password` | Reset password |

### Projects
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/projects` | Get project list |
| GET | `/api/projects/{id}` | Get project details |
| POST | `/api/projects` | Create new project |
| PUT | `/api/projects/{id}` | Update project |
| DELETE | `/api/projects/{id}` | Delete project |

### Tasks
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/tasks` | Get task list |
| GET | `/api/tasks/{id}` | Get task details |
| POST | `/api/tasks` | Create new task |
| PUT | `/api/tasks/{id}` | Update task |
| PATCH | `/api/tasks/{id}/status` | Update task status |
| DELETE | `/api/tasks/{id}` | Delete task |

### Project Members
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/projects/{id}/members` | Get member list |
| POST | `/api/projects/{id}/members` | Add member |
| DELETE | `/api/projects/{id}/members/{userId}` | Remove member |

## Deployment

### Deploy to AWS Lambda

The project uses GitLab CI/CD for automatic deployment:

1. Push code to `main` branch
2. GitLab CI will automatically:
   - Build project
   - Publish and package as ZIP file
   - Deploy to AWS Lambda

### Manual Deploy

```bash
# Build and publish
dotnet publish WebAPI/Web_API.AppHost/API.csproj -c Release -o publish

# Create ZIP file
Compress-Archive -Path publish/* -DestinationPath backend.zip

# Deploy to Lambda (requires AWS CLI)
aws lambda update-function-code --function-name YOUR_LAMBDA_NAME --zip-file fileb://backend.zip --region ap-southeast-1
```

## Troubleshooting

### Common Errors

**1. DynamoDB connection error**
```
Check AWS credentials and region in appsettings.json
```

**2. CORS error**
```
Ensure FrontendUrl in appsettings.json matches frontend URL
```

**3. JWT error**
```
Ensure SecretKey has at least 32 characters length
```

**4. Email error**
```
Use Gmail App Password instead of regular password
```

## Notes

- Project uses 3-tier architecture
- API is protected by JWT Authentication
- File uploads are stored on S3 (production) or local (development)
- Uses DynamoDB as primary database

## Contributing

1. Fork the repository
2. Create new branch (`git checkout -b feature/AmazingFeature`)
3. Commit changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to branch (`git push origin feature/AmazingFeature`)
5. Create Pull Request

## License

© 2025 TaskHub. All rights reserved.
