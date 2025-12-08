using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using BLL.DTOs;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace BLL.Services
{
    public class CognitoAuthService : ICognitoAuthService
    {
        private readonly IAmazonCognitoIdentityProvider _cognitoClient;
        private readonly IConfiguration _configuration;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _userPoolId;

        public CognitoAuthService(
            IAmazonCognitoIdentityProvider cognitoClient,
            IConfiguration configuration)
        {
            _cognitoClient = cognitoClient ?? throw new ArgumentNullException(nameof(cognitoClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            _clientId = _configuration["AWS:Cognito:ClientId"] ?? throw new InvalidOperationException("AWS:Cognito:ClientId is required");
            _clientSecret = _configuration["AWS:Cognito:ClientSecret"] ?? throw new InvalidOperationException("AWS:Cognito:ClientSecret is required");
            _userPoolId = _configuration["AWS:Cognito:UserPoolId"] ?? throw new InvalidOperationException("AWS:Cognito:UserPoolId is required");
        }

        public async Task<AuthResponse> RegisterWithCognitoAsync(RegisterRequest request)
        {
            try
            {
                if (request == null)
                {
                    return new AuthResponse { Message = "Request cannot be null" };
                }

                if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Email))
                {
                    return new AuthResponse { Message = "Username and Email are required" };
                }

                if (string.IsNullOrEmpty(request.Name))
                {
                    return new AuthResponse { Message = "Name is required" };
                }

                var secretHash = ComputeSecretHash(request.Username);
                
                var signUpRequest = new SignUpRequest
                {
                    ClientId = _clientId,
                    SecretHash = secretHash,
                    Username = request.Username,
                    Password = request.Password,
                    UserAttributes = new List<AttributeType>
                    {
                        new AttributeType { Name = "email", Value = request.Email },
                        new AttributeType { Name = "name", Value = request.Name }
                    }
                };

                if (!string.IsNullOrEmpty(request.PhoneNumber))
                {
                    var phoneNumber = request.PhoneNumber.StartsWith("+") 
                        ? request.PhoneNumber 
                        : $"+84{request.PhoneNumber.TrimStart('0')}";
                    signUpRequest.UserAttributes.Add(
                        new AttributeType { Name = "phone_number", Value = phoneNumber });
                }

                var response = await _cognitoClient.SignUpAsync(signUpRequest);

                return new AuthResponse
                {
                    Message = "User registered with Cognito. Please check your email for verification.",
                    User = new UserProfile
                    {
                        UserId = response.UserSub,
                        Username = request.Username,
                        Email = request.Email,
                        Name = request.Name,
                        Status = "UNCONFIRMED",
                        EmailVerified = false
                    }
                };
            }
            catch (UsernameExistsException)
            {
                return new AuthResponse { Message = "Username already exists in Cognito" };
            }
            catch (InvalidParameterException ex)
            {
                return new AuthResponse { Message = $"Invalid parameter: {ex.Message}" };
            }
            catch (AmazonCognitoIdentityProviderException ex) when (ex.Message.Contains("password"))
            {
                return new AuthResponse { Message = "Password does not meet requirements" };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cognito registration error: {ex}");
                return new AuthResponse { Message = $"Cognito registration failed: {ex.Message}" };
            }
        }

        public async Task<AuthResponse> LoginWithCognitoAsync(LoginRequest request)
        {
            try
            {
                if (request == null)
                {
                    return new AuthResponse { Message = "Request cannot be null" };
                }

                if (string.IsNullOrEmpty(request.UsernameOrEmail) || string.IsNullOrEmpty(request.Password))
                {
                    return new AuthResponse { Message = "Username/Email and Password are required" };
                }

                // ✅ Debug logging
                Console.WriteLine($"=== COGNITO LOGIN DEBUG ===");
                Console.WriteLine($"Username: {request.UsernameOrEmail}");
                Console.WriteLine($"UserPoolId: {_userPoolId}");
                Console.WriteLine($"ClientId: {_clientId}");

                var secretHash = ComputeSecretHash(request.UsernameOrEmail);
                Console.WriteLine($"SecretHash computed successfully");

                // ✅ SỬ DỤNG InitiateAuth THAY VÌ AdminInitiateAuth
                var authRequest = new InitiateAuthRequest
                {
                    ClientId = _clientId,
                    AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,  // ✅ Thay đổi auth flow
                    AuthParameters = new Dictionary<string, string>
                    {
                        { "USERNAME", request.UsernameOrEmail },
                        { "PASSWORD", request.Password },
                        { "SECRET_HASH", secretHash }
                    }
                };

                Console.WriteLine($"Calling Cognito InitiateAuth...");
                var response = await _cognitoClient.InitiateAuthAsync(authRequest);
                Console.WriteLine($"Cognito response received");

                if (response.ChallengeName != null && response.ChallengeName == ChallengeNameType.NEW_PASSWORD_REQUIRED)
                {
                    return new AuthResponse { Message = "New password required" };
                }

                if (response.AuthenticationResult == null)
                {
                    return new AuthResponse { Message = "Authentication failed - no result" };
                }

                return new AuthResponse
                {
                    AccessToken = response.AuthenticationResult.AccessToken,
                    RefreshToken = response.AuthenticationResult.RefreshToken,
                    Message = "Cognito login successful"
                };
            }
            catch (NotAuthorizedException ex)
            {
                Console.WriteLine($"NotAuthorized: {ex.Message}");
                return new AuthResponse { Message = "Invalid credentials" };
            }
            catch (UserNotConfirmedException ex)
            {
                Console.WriteLine($"UserNotConfirmed: {ex.Message}");
                return new AuthResponse { Message = "User email not verified. Please confirm your email first." };
            }
            catch (UserNotFoundException ex)
            {
                Console.WriteLine($"UserNotFound: {ex.Message}");
                return new AuthResponse { Message = "User not found" };
            }
            catch (TooManyRequestsException ex)
            {
                Console.WriteLine($"TooManyRequests: {ex.Message}");
                return new AuthResponse { Message = "Too many requests. Please try again later." };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== COGNITO LOGIN ERROR ===");
                Console.WriteLine($"Exception Type: {ex.GetType().Name}");
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                Console.WriteLine($"==========================");
                return new AuthResponse { Message = $"Cognito login failed: {ex.Message}" };
            }
        }

        // ✅ THÊM CÁC METHODS THIẾU

        public async Task<AuthResponse> RefreshCognitoTokenAsync(string refreshToken)
        {
            try
            {
                if (string.IsNullOrEmpty(refreshToken))
                {
                    return new AuthResponse { Message = "Refresh token is required" };
                }

                var refreshRequest = new InitiateAuthRequest
                {
                    ClientId = _clientId,
                    AuthFlow = AuthFlowType.REFRESH_TOKEN_AUTH,
                    AuthParameters = new Dictionary<string, string>
                    {
                        { "REFRESH_TOKEN", refreshToken }
                    }
                };

                var response = await _cognitoClient.InitiateAuthAsync(refreshRequest);

                if (response.AuthenticationResult == null)
                {
                    return new AuthResponse { Message = "Failed to refresh token" };
                }

                return new AuthResponse
                {
                    AccessToken = response.AuthenticationResult.AccessToken,
                    RefreshToken = refreshToken,
                    Message = "Cognito token refreshed successfully"
                };
            }
            catch (NotAuthorizedException)
            {
                return new AuthResponse { Message = "Invalid refresh token" };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Refresh token error: {ex}");
                return new AuthResponse { Message = $"Cognito token refresh failed: {ex.Message}" };
            }
        }

        public async Task<bool> ValidateCognitoTokenAsync(string accessToken)
        {
            try
            {
                if (string.IsNullOrEmpty(accessToken))
                    return false;

                var request = new GetUserRequest
                {
                    AccessToken = accessToken
                };

                await _cognitoClient.GetUserAsync(request);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Token validation error: {ex.Message}");
                return false;
            }
        }

        public async Task<CognitoUser> GetUserFromTokenAsync(string accessToken)
        {
            try
            {
                if (string.IsNullOrEmpty(accessToken))
                    return null;

                var request = new GetUserRequest
                {
                    AccessToken = accessToken
                };

                var response = await _cognitoClient.GetUserAsync(request);

                return new CognitoUser
                {
                    Username = response.Username,
                    Email = GetAttributeValue(response.UserAttributes, "email"),
                    Name = GetAttributeValue(response.UserAttributes, "name"),
                    PhoneNumber = GetAttributeValue(response.UserAttributes, "phone_number"),
                    EmailVerified = GetAttributeValue(response.UserAttributes, "email_verified") == "true"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get user from token error: {ex.Message}");
                return null;
            }
        }

        public async Task<CognitoConfirmSignUpResponse> ConfirmSignUpAsync(CognitoConfirmSignUpRequest request)
        {
            try
            {
                if (request == null)
                {
                    return new CognitoConfirmSignUpResponse
                    {
                        Success = false,
                        Message = "Request cannot be null"
                    };
                }

                if (string.IsNullOrEmpty(request.Username))
                {
                    return new CognitoConfirmSignUpResponse
                    {
                        Success = false,
                        Message = "Username is required"
                    };
                }

                if (string.IsNullOrEmpty(request.ConfirmationCode))
                {
                    return new CognitoConfirmSignUpResponse
                    {
                        Success = false,
                        Message = "Confirmation code is required"
                    };
                }

                var cognitoConfirmRequest = new Amazon.CognitoIdentityProvider.Model.ConfirmSignUpRequest
                {
                    ClientId = _clientId,
                    SecretHash = ComputeSecretHash(request.Username),
                    Username = request.Username,
                    ConfirmationCode = request.ConfirmationCode
                };

                await _cognitoClient.ConfirmSignUpAsync(cognitoConfirmRequest);

                return new CognitoConfirmSignUpResponse
                {
                    Success = true,
                    Message = "Email confirmed successfully. You can now login."
                };
            }
            catch (ExpiredCodeException)
            {
                return new CognitoConfirmSignUpResponse
                {
                    Success = false,
                    Message = "Confirmation code has expired. Please request a new one."
                };
            }
            catch (CodeMismatchException)
            {
                return new CognitoConfirmSignUpResponse
                {
                    Success = false,
                    Message = "Invalid confirmation code. Please check and try again."
                };
            }
            catch (UserNotFoundException)
            {
                return new CognitoConfirmSignUpResponse
                {
                    Success = false,
                    Message = "User not found"
                };
            }
            catch (NotAuthorizedException)
            {
                return new CognitoConfirmSignUpResponse
                {
                    Success = false,
                    Message = "User is already confirmed or not authorized"
                };
            }
            catch (AmazonCognitoIdentityProviderException ex) when (ex.Message.Contains("too many"))
            {
                return new CognitoConfirmSignUpResponse
                {
                    Success = false,
                    Message = "Too many failed attempts. Please try again later."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Confirm signup error: {ex}");
                return new CognitoConfirmSignUpResponse
                {
                    Success = false,
                    Message = $"Confirmation failed: {ex.Message}"
                };
            }
        }

        public async Task<CognitoConfirmSignUpResponse> ResendConfirmationCodeAsync(CognitoResendConfirmationRequest request)
        {
            try
            {
                if (request == null)
                {
                    return new CognitoConfirmSignUpResponse
                    {
                        Success = false,
                        Message = "Request cannot be null"
                    };
                }

                if (string.IsNullOrEmpty(request.Username))
                {
                    return new CognitoConfirmSignUpResponse
                    {
                        Success = false,
                        Message = "Username is required"
                    };
                }

                var cognitoResendRequest = new Amazon.CognitoIdentityProvider.Model.ResendConfirmationCodeRequest
                {
                    ClientId = _clientId,
                    SecretHash = ComputeSecretHash(request.Username),
                    Username = request.Username
                };

                await _cognitoClient.ResendConfirmationCodeAsync(cognitoResendRequest);

                return new CognitoConfirmSignUpResponse
                {
                    Success = true,
                    Message = "Confirmation code has been resent to your email."
                };
            }
            catch (UserNotFoundException)
            {
                return new CognitoConfirmSignUpResponse
                {
                    Success = false,
                    Message = "User not found"
                };
            }
            catch (InvalidParameterException ex)
            {
                return new CognitoConfirmSignUpResponse
                {
                    Success = false,
                    Message = $"Invalid parameter: {ex.Message}"
                };
            }
            catch (AmazonCognitoIdentityProviderException ex) when (ex.Message.Contains("limit"))
            {
                return new CognitoConfirmSignUpResponse
                {
                    Success = false,
                    Message = "Attempt limit exceeded. Please wait before requesting another code."
                };
            }
            catch (TooManyRequestsException)
            {
                return new CognitoConfirmSignUpResponse
                {
                    Success = false,
                    Message = "Too many requests. Please try again later."
                };
            }
            catch (NotAuthorizedException)
            {
                return new CognitoConfirmSignUpResponse
                {
                    Success = false,
                    Message = "User is already confirmed"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Resend confirmation code error: {ex}");
                return new CognitoConfirmSignUpResponse
                {
                    Success = false,
                    Message = $"Failed to resend confirmation code: {ex.Message}"
                };
            }
        }

        public async Task<CognitoForgotPasswordResponse> ForgotPasswordAsync(CognitoForgotPasswordRequest request)
        {
            try
            {
                if (request == null)
                {
                    return new CognitoForgotPasswordResponse
                    {
                        Success = false,
                        Message = "Request cannot be null"
                    };
                }

                if (string.IsNullOrEmpty(request.Username))
                {
                    return new CognitoForgotPasswordResponse
                    {
                        Success = false,
                        Message = "Username is required"
                    };
                }

                var forgotPasswordRequest = new Amazon.CognitoIdentityProvider.Model.ForgotPasswordRequest
                {
                    ClientId = _clientId,
                    SecretHash = ComputeSecretHash(request.Username),
                    Username = request.Username
                };

                var response = await _cognitoClient.ForgotPasswordAsync(forgotPasswordRequest);

                return new CognitoForgotPasswordResponse
                {
                    Success = true,
                    Message = "Password reset code has been sent to your registered email/phone.",
                    DeliveryMedium = response.CodeDeliveryDetails?.DeliveryMedium?.Value ?? "EMAIL"
                };
            }
            catch (UserNotFoundException)
            {
                return new CognitoForgotPasswordResponse
                {
                    Success = false,
                    Message = "User not found"
                };
            }
            catch (InvalidParameterException ex)
            {
                return new CognitoForgotPasswordResponse
                {
                    Success = false,
                    Message = $"Invalid parameter: {ex.Message}"
                };
            }
            catch (LimitExceededException)
            {
                return new CognitoForgotPasswordResponse
                {
                    Success = false,
                    Message = "Attempt limit exceeded. Please wait before requesting another code."
                };
            }
            catch (TooManyRequestsException)
            {
                return new CognitoForgotPasswordResponse
                {
                    Success = false,
                    Message = "Too many requests. Please try again later."
                };
            }
            catch (NotAuthorizedException)
            {
                return new CognitoForgotPasswordResponse
                {
                    Success = false,
                    Message = "Not authorized to reset password"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cognito forgot password error: {ex}");
                return new CognitoForgotPasswordResponse
                {
                    Success = false,
                    Message = $"Failed to initiate password reset: {ex.Message}"
                };
            }
        }

        public async Task<CognitoConfirmForgotPasswordResponse> ConfirmForgotPasswordAsync(CognitoConfirmForgotPasswordRequest request)
        {
            try
            {
                if (request == null)
                {
                    return new CognitoConfirmForgotPasswordResponse
                    {
                        Success = false,
                        Message = "Request cannot be null"
                    };
                }

                if (string.IsNullOrEmpty(request.Username))
                {
                    return new CognitoConfirmForgotPasswordResponse
                    {
                        Success = false,
                        Message = "Username is required"
                    };
                }

                if (string.IsNullOrEmpty(request.ConfirmationCode))
                {
                    return new CognitoConfirmForgotPasswordResponse
                    {
                        Success = false,
                        Message = "Confirmation code is required"
                    };
                }

                if (string.IsNullOrEmpty(request.NewPassword))
                {
                    return new CognitoConfirmForgotPasswordResponse
                    {
                        Success = false,
                        Message = "New password is required"
                    };
                }

                var confirmForgotPasswordRequest = new Amazon.CognitoIdentityProvider.Model.ConfirmForgotPasswordRequest
                {
                    ClientId = _clientId,
                    SecretHash = ComputeSecretHash(request.Username),
                    Username = request.Username,
                    ConfirmationCode = request.ConfirmationCode,
                    Password = request.NewPassword
                };

                await _cognitoClient.ConfirmForgotPasswordAsync(confirmForgotPasswordRequest);

                return new CognitoConfirmForgotPasswordResponse
                {
                    Success = true,
                    Message = "Password has been reset successfully. You can now login with your new password."
                };
            }
            catch (ExpiredCodeException)
            {
                return new CognitoConfirmForgotPasswordResponse
                {
                    Success = false,
                    Message = "Reset code has expired. Please request a new one."
                };
            }
            catch (CodeMismatchException)
            {
                return new CognitoConfirmForgotPasswordResponse
                {
                    Success = false,
                    Message = "Invalid reset code. Please check and try again."
                };
            }
            catch (UserNotFoundException)
            {
                return new CognitoConfirmForgotPasswordResponse
                {
                    Success = false,
                    Message = "User not found"
                };
            }
            catch (InvalidPasswordException)
            {
                return new CognitoConfirmForgotPasswordResponse
                {
                    Success = false,
                    Message = "Password does not meet security requirements"
                };
            }
            catch (TooManyFailedAttemptsException)
            {
                return new CognitoConfirmForgotPasswordResponse
                {
                    Success = false,
                    Message = "Too many failed attempts. Please try again later."
                };
            }
            catch (TooManyRequestsException)
            {
                return new CognitoConfirmForgotPasswordResponse
                {
                    Success = false,
                    Message = "Too many requests. Please try again later."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Confirm forgot password error: {ex}");
                return new CognitoConfirmForgotPasswordResponse
                {
                    Success = false,
                    Message = $"Failed to reset password: {ex.Message}"
                };
            }
        }

        // ✅ HELPER METHODS
        private string ComputeSecretHash(string username)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(_clientSecret) || string.IsNullOrEmpty(_clientId))
            {
                throw new ArgumentException("Username, ClientSecret, and ClientId are required for secret hash computation");
            }

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_clientSecret));
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(username + _clientId));
            return Convert.ToBase64String(computedHash);
        }

        private string GetAttributeValue(List<AttributeType> attributes, string attributeName)
        {
            return attributes?.FirstOrDefault(a => a.Name == attributeName)?.Value;
        }
    }
}