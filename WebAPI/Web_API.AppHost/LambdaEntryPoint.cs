namespace API;

/// <summary>
/// This class extends from APIGatewayProxyFunction which contains the method FunctionHandlerAsync which is the 
/// actual Lambda function entry point. The Lambda handler field should be set to:
/// 
/// API::API.LambdaEntryPoint::FunctionHandlerAsync
/// </summary>
public class LambdaEntryPoint : Amazon.Lambda.AspNetCoreServer.APIGatewayProxyFunction
{
    /// <summary>
    /// The builder has configuration, logging and Amazon API Gateway specific services.
    /// </summary>
    protected override void Init(IWebHostBuilder builder)
    {
        builder
            .UseStartup<StartupLambda>();
    }

    /// <summary>
    /// Use this override to customize the services registered with the IHostBuilder. 
    /// 
    /// It is recommended not to call ConfigureWebHostDefaults to configure the IWebHostBuilder inside this method.
    /// Instead customize the IWebHostBuilder in the Init(IWebHostBuilder) overload.
    /// </summary>
    protected override void Init(IHostBuilder builder)
    {
    }
}