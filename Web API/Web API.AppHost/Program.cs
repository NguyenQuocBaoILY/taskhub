using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Runtime;
using Amazon.CognitoIdentityProvider;
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

var builder = WebApplication.CreateBuilder(args);

// ✅ AWS CONFIG - Separate credentials for different services
var awsConfig = builder.Configuration.GetSection("AWS");

// 1. DynamoDB - Support both Local and AWS
var useDynamoDbLocal = awsConfig.GetValue<bool>("UseDynamoDbLocal");


if (useDynamoDbLocal)
{
    // DynamoDB Local for development
    var dynamoCredentials = new BasicAWSCredentials("dummy", "dummy");
    var dynamoConfig = new AmazonDynamoDBConfig { ServiceURL = awsConfig["ServiceURL"] };
    builder.Services.AddSingleton<IAmazonDynamoDB>(_ =>
        new AmazonDynamoDBClient(dynamoCredentials, dynamoConfig));
}
else
{
    // AWS DynamoDB for production
    var dynamoCredentials = new BasicAWSCredentials(
        awsConfig["AccessKey"],
        awsConfig["SecretKey"]);
    var dynamoConfig = new AmazonDynamoDBConfig
    {
        RegionEndpoint = RegionEndpoint.GetBySystemName(awsConfig["Region"])
    };
    builder.Services.AddSingleton<IAmazonDynamoDB>(_ =>
        new AmazonDynamoDBClient(dynamoCredentials, dynamoConfig));
}


builder.Services.AddSingleton<IDynamoDBContext>(sp =>
{
    var client = sp.GetRequiredService<IAmazonDynamoDB>();
    return new DynamoDBContext(client);
});


// 2. AWS Cognito (real credentials từ appsettings)
var cognitoCredentials = new BasicAWSCredentials(
    awsConfig["AccessKey"],
    awsConfig["SecretKey"]);
var cognitoConfig = new AmazonCognitoIdentityProviderConfig
{
    RegionEndpoint = RegionEndpoint.GetBySystemName(awsConfig["Region"])
};


builder.Services.AddSingleton<IAmazonCognitoIdentityProvider>(_ =>
    new AmazonCognitoIdentityProviderClient(cognitoCredentials, cognitoConfig));
builder.Services.AddScoped<ICognitoAuthService, CognitoAuthService>();


// Repositories - Scoped
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthSessionRepository, AuthSessionRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<IProjectMemberRepository, ProjectMemberRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<ITaskFileRepository, TaskFileRepository>();


// Services - Scoped
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IGoogleAuthService, GoogleAuthService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<ProjectMemberService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<TaskFileService>();
builder.Services.AddScoped<UserService>();


// File storage
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();


// JWT Authentication Configuration
var jwtSettings = builder.Configuration.GetSection("JWT");
var jwtSecretKey = jwtSettings["SecretKey"] ?? "your-super-secret-jwt-key-here-make-it-long-enough-minimum-32-characters";


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
        ValidIssuer = jwtSettings["Issuer"] ?? "TaskHub",
        ValidAudience = jwtSettings["Audience"] ?? "TaskHub",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtSecretKey)),
        ClockSkew = TimeSpan.Zero
    };
});


// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TaskHub API",
        Version = "v1"
    });

    // 👇 Quan trọng: định nghĩa Bearer JWT cho Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Nhập JWT theo dạng: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    // 👇 Bắt Swagger tự động gắn token vào mọi request
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
            Array.Empty<string>()
        }
    });
});

// Add CORS services
var allowedOrigins = builder.Configuration.GetSection("App:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };


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


var app = builder.Build();


// Configure pipeline
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