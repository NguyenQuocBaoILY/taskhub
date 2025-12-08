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

var builder = WebApplication.CreateBuilder(args);

// =======================
// AWS CONFIG
// =======================
var awsConfig = builder.Configuration.GetSection("AWS");
var useDynamoDbLocal = awsConfig.GetValue<bool>("UseDynamoDbLocal");
var awsRegion = RegionEndpoint.GetBySystemName(awsConfig["Region"]);


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
        awsConfig["AccessKey"],
        awsConfig["SecretKey"]);

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
    awsConfig["AccessKey"],
    awsConfig["SecretKey"]);

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
builder.Services.AddAWSService<IAmazonS3>();


// =======================
// 4. FILE STORAGE (LOCAL / S3) — FIXED VERSION
// =======================
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
}
else
{
    builder.Services.AddSingleton<IFileStorage, S3FileStorage>();
}


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
var jwtSecretKey = jwtSettings["SecretKey"];

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
