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
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

namespace API;

public class StartupLambda
{
    public IConfiguration Configuration { get; }

    public StartupLambda(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        // ✅ AWS CONFIG - Separate credentials for different services
        var awsConfig = Configuration.GetSection("AWS");

        // 1. DynamoDB (use environment-based configuration)
        var useDynamoDbLocal = Configuration.GetValue<bool>("AWS:UseDynamoDbLocal");

        if (useDynamoDbLocal)
        {
            // DynamoDB Local for development
            var dynamoCredentials = new BasicAWSCredentials("dummy", "dummy");
            var dynamoConfig = new AmazonDynamoDBConfig { ServiceURL = awsConfig["ServiceURL"] };
            services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient(dynamoCredentials, dynamoConfig));
        }
        else
        {
            var dynamoCredentials = new BasicAWSCredentials(
                awsConfig["AccessKey"],
                awsConfig["SecretKey"]);

            var dynamoConfig = new AmazonDynamoDBConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(awsConfig["Region"])
            };

            services.AddSingleton<IAmazonDynamoDB>(_ =>
                new AmazonDynamoDBClient(dynamoCredentials, dynamoConfig));
        }

        services.AddSingleton<IDynamoDBContext>(sp =>
        {
            var client = sp.GetRequiredService<IAmazonDynamoDB>();
            return new DynamoDBContext(client);
        });

        // 2. AWS Cognito
        var cognitoCredentials = new BasicAWSCredentials(
            awsConfig["AccessKey"],
            awsConfig["SecretKey"]);
        var cognitoConfig = new AmazonCognitoIdentityProviderConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(awsConfig["Region"])
        };

        services.AddSingleton<IAmazonCognitoIdentityProvider>(_ =>
            new AmazonCognitoIdentityProviderClient(cognitoCredentials, cognitoConfig));
        services.AddScoped<ICognitoAuthService, CognitoAuthService>();

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuthSessionRepository, AuthSessionRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectMemberRepository, ProjectMemberRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<ITaskFileRepository, TaskFileRepository>();

        // Services
        services.AddScoped<AuthService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<ProjectMemberService>();
        services.AddScoped<TaskService>();
        services.AddScoped<TaskFileService>();
        services.AddScoped<UserService>();

        // File storage - FORCE S3 for Lambda production
        services.AddSingleton<IFileStorage, S3FileStorage>();
        
        // Add S3 client for production using IAM role
        services.AddAWSService<Amazon.S3.IAmazonS3>();
        
        // Add logging for debugging
        Console.WriteLine("[StartupLambda] FORCED S3FileStorage for Lambda production");
        Console.WriteLine($"[StartupLambda] S3 Bucket: {Configuration["AWS:S3:BucketName"] ?? "NOT_SET"}");
        Console.WriteLine($"[StartupLambda] Deployment Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

        // JWT Authentication
        var jwtSettings = Configuration.GetSection("JWT");
        var jwtSecretKey = jwtSettings["SecretKey"] ?? "your-super-secret-jwt-key-here-make-it-long-enough-minimum-32-characters";

        services.AddAuthentication(opt =>
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

        // Controllers
        services.AddControllers();

        // Swagger
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "TaskHub API",
                Version = "v1"
            });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Description = "Nhập JWT theo dạng: Bearer {token}",
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
                    Array.Empty<string>()
                }
            });
        });

        // CORS
        var allowedOrigins = Configuration.GetSection("App:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:5173" };

        services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
            });
        });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors("AllowFrontend");
        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}