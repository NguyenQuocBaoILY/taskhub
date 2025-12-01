using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Runtime;
using BLL.Services;
using DAL.Interfaces;
using DAL.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Lấy cấu hình AWS từ appsettings.json
var awsConfig = builder.Configuration.GetSection("AWS");
var serviceUrl = awsConfig["ServiceURL"];
var region = awsConfig["Region"];
var accessKey = awsConfig["AccessKey"];
var secretKey = awsConfig["SecretKey"];

// Tạo AWS Credentials cho DynamoDB Local
var credentials = new BasicAWSCredentials(accessKey, secretKey);
var clientConfig = new AmazonDynamoDBConfig
{
    ServiceURL = serviceUrl, // local DynamoDB
};

// Đăng ký DynamoDB Client
builder.Services.AddSingleton<IAmazonDynamoDB>(sp => new AmazonDynamoDBClient(credentials, clientConfig));

builder.Services.AddSingleton<IDynamoDBContext>(sp =>
{
    var client = sp.GetRequiredService<IAmazonDynamoDB>();
    return new DynamoDBContext(client);
});

// Đăng ký Repository và Service
builder.Services.AddSingleton<ITaskRepository, TaskRepository>();
builder.Services.AddSingleton<TaskService>();

// Repositories used across services
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IAuthSessionRepository, AuthSessionRepository>();
builder.Services.AddSingleton<IProjectMemberRepository, ProjectMemberRepository>();

// ----- ADD: register project repository + service so Project CRUD works -----
builder.Services.AddSingleton<IProjectRepository, ProjectRepository>();
builder.Services.AddSingleton<ProjectService>();
// ---------------------------------------------------------------------------

// Business services
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<ProjectMemberService>();
builder.Services.AddScoped<UserService>();

// Add Email and Google Auth services
builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddSingleton<IGoogleAuthService, GoogleAuthService>();

// JWT Authentication Configuration
var jwtSettings = builder.Configuration.GetSection("JWT");
var jwtSecretKey = jwtSettings["SecretKey"] ?? "your-super-secret-jwt-key-here-make-it-long-enough-minimum-32-characters";

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
        ValidIssuer = jwtSettings["Issuer"] ?? "TaskHub",
        ValidAudience = jwtSettings["Audience"] ?? "TaskHub",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtSecretKey)),
        ClockSkew = TimeSpan.Zero
    };
});

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
    });
});

var app = builder.Build();

// Chỉ bật Swagger khi dev
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Add CORS middleware - must come before authentication and authorization
app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();