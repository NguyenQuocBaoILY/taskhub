using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel; 
using Amazon.Runtime;
using BLL.Services;
using DAL.Interfaces;
using DAL.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddSingleton<TaskService>();
builder.Services.AddScoped<ProjectService>();

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Chỉ bật Swagger khi dev
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// KHÔNG bắt buộc phải dùng HTTPS khi bạn đang test HTTP
// app.UseHttpsRedirection();

app.UseCors();          // <- phải trước UseAuthorization
app.UseAuthorization();

app.MapControllers();

app.Run();