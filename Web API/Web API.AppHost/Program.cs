using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Runtime;
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
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// AWS DynamoDB Local
var awsConfig = builder.Configuration.GetSection("AWS");
var accessKey = awsConfig["AccessKey"];
var secretKey = awsConfig["SecretKey"];
var serviceUrl = awsConfig["ServiceURL"];

var credentials = new BasicAWSCredentials(accessKey, secretKey);
var clientConfig = new AmazonDynamoDBConfig { ServiceURL = serviceUrl };

builder.Services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient(credentials, clientConfig));
builder.Services.AddSingleton<IDynamoDBContext>(sp =>
{
    var client = sp.GetRequiredService<IAmazonDynamoDB>();
    return new DynamoDBContext(client);
});

// Repositories
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IProjectRepository, ProjectRepository>();
builder.Services.AddSingleton<IProjectMemberRepository, ProjectMemberRepository>();
builder.Services.AddSingleton<ITaskRepository, TaskRepository>();
builder.Services.AddSingleton<ITaskFileRepository, TaskFileRepository>();

// Services
builder.Services.AddSingleton<ProjectService>();
builder.Services.AddSingleton<ProjectMemberService>();
builder.Services.AddSingleton<TaskService>();
builder.Services.AddSingleton<TaskFileService>();
builder.Services.AddScoped<UserService>();

// File storage (Local)
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

// Static files (uploads)
app.UseStaticFiles(); // phục vụ wwwroot + /uploads

app.MapControllers();

app.Run();