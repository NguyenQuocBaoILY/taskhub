using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Runtime;
using Amazon.CognitoIdentityProvider;
using Amazon.S3;
using BLL.Services;
using DAL.Interfaces;
using DAL.Repositories;
using Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// =======================
// AWS CONFIG
// =======================
var awsConfig = builder.Configuration.GetSection("AWS");
var useDynamoDbLocal = awsConfig.GetValue<bool>("UseDynamoDbLocal");
var awsRegion = RegionEndpoint.GetBySystemName(awsConfig["Region"]);

Console.WriteLine($"[Config] Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"[Config] UseDynamoDbLocal: {useDynamoDbLocal}");
Console.WriteLine($"[Config] AWS Region: {awsRegion?.SystemName}");
Console.WriteLine($"[VERSION] CODE_VERSION_2025_12_11_FORCE_S3");


// =======================
// LOAD SECRETS FROM AWS SECRETS MANAGER
// =======================

// Tên secret lưu trong appsettings.json cho linh hoạt:
var secretName = builder.Configuration["Secrets:BackendConfigName"] ?? "taskhub/dev/backend-config";

Dictionary<string, string> secrets = new();

try
{
    secrets = LoadAppSecrets(secretName, awsRegion);
    Console.WriteLine("[Secrets] Loaded successfully.");
}
catch
{
    Console.WriteLine("[Secrets] Cannot load secrets, using appsettings.");
}

string GetSecret(string key, string? fallback = null)
    => secrets.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value)
        ? value
        : (fallback ?? "");

builder.Configuration["AWS:Cognito:ClientSecret"] =
    GetSecret("AWS:Cognito:ClientSecret", builder.Configuration["AWS:Cognito:ClientSecret"]);

builder.Configuration["GoogleAuth:ClientSecret"] =
    GetSecret("GoogleAuth:ClientSecret", builder.Configuration["GoogleAuth:ClientSecret"]);

builder.Configuration["Email:SmtpPassword"] =
    GetSecret("Email:SmtpPassword", builder.Configuration["Email:SmtpPassword"]);

// =======================
// 1. DynamoDB (local / production)
// =======================
if (useDynamoDbLocal)
{
    var dynamoCredentials = new BasicAWSCredentials("dummy", "dummy");
    var dynamoConfig = new AmazonDynamoDBConfig { ServiceURL = awsConfig["ServiceURL"] };

    builder.Services.AddSingleton<IAmazonDynamoDB>(_ =>
        new AmazonDynamoDBClient(dynamoCredentials, dynamoConfig));
}
else
{
var dynamoCredentials = new BasicAWSCredentials(
    GetSecret("AWS:AccessKey", awsConfig["AccessKey"]),
    GetSecret("AWS:SecretKey", awsConfig["SecretKey"])
);

    builder.Services.AddSingleton<IAmazonDynamoDB>(_ =>
        new AmazonDynamoDBClient(dynamoCredentials, awsRegion));
}

builder.Services.AddSingleton<IDynamoDBContext>(sp =>
{
    var client = sp.GetRequiredService<IAmazonDynamoDB>();
    return new DynamoDBContext(client);
});


// =======================
// 2. Cognito
// =======================
var cognitoCredentials = new BasicAWSCredentials(
    GetSecret("AWS:AccessKey", awsConfig["AccessKey"]),
    GetSecret("AWS:SecretKey", awsConfig["SecretKey"]));

var cognitoConfig = new AmazonCognitoIdentityProviderConfig
{
    RegionEndpoint = awsRegion
};

builder.Services.AddSingleton<IAmazonCognitoIdentityProvider>(_ =>
    new AmazonCognitoIdentityProviderClient(cognitoCredentials, cognitoConfig));

builder.Services.AddScoped<ICognitoAuthService, CognitoAuthService>();


// =======================
// 3. S3 client
// =======================
if (builder.Environment.IsDevelopment())
{
    // Development: Sử dụng default AWS configuration
    builder.Services.AddAWSService<IAmazonS3>();
}
else
{
    // Production: Sử dụng IAM role của Lambda
    builder.Services.AddSingleton<IAmazonS3>(_ =>
        new AmazonS3Client(awsRegion));
}


// =======================
// 4. FILE STORAGE (LOCAL / S3) — FORCE S3 IN PRODUCTION
// =======================
// FORCE: Luôn dùng S3FileStorage trong production
builder.Services.AddSingleton<IFileStorage, S3FileStorage>();
Console.WriteLine("[FileStorage] FORCED S3FileStorage for Production");
Console.WriteLine($"[FileStorage] Deployment Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
Console.WriteLine($"[FileStorage] S3 Bucket Config: {builder.Configuration["AWS:S3:BucketName"] ?? "NOT_SET"}");

// =======================
// 5. REPOSITORIES
// =======================
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthSessionRepository, AuthSessionRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IProjectMemberRepository, ProjectMemberRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<ITaskFileRepository, TaskFileRepository>();


// =======================
// 6. SERVICES
// =======================
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IGoogleAuthService, GoogleAuthService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<ProjectMemberService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<TaskFileService>();
builder.Services.AddScoped<UserService>();


// =======================
// 7. JWT AUTH
// =======================
var jwtSettings = builder.Configuration.GetSection("JWT");
var jwtSecretKey = GetSecret("JWT:SecretKey", jwtSettings["SecretKey"]);

builder.Services.AddAuthentication(opt =>
{
    opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(opt =>
{
    opt.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtSecretKey)),
        ClockSkew = TimeSpan.Zero
    };
});


// =======================
// 8. SWAGGER
// =======================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "TaskHub API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Nhập JWT: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});


// =======================
// 9. CORS
// =======================
var allowedOrigins = builder.Configuration
    .GetSection("App:AllowedOrigins")
    .Get<string[]>() ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});


// =======================
// 10. Secret Manager
// =======================
static Dictionary<string, string> LoadAppSecrets(string secretName, RegionEndpoint region)
{
    using var client = new AmazonSecretsManagerClient(region);

    var request = new GetSecretValueRequest
    {
        SecretId = secretName
    };

    var response = client.GetSecretValueAsync(request).GetAwaiter().GetResult();

    var secretString = response.SecretString;

    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(secretString) ?? new Dictionary<string, string>();

    return dict;
}

// =======================
// BUILD APP
// =======================
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();

app.MapControllers();
app.Run();
