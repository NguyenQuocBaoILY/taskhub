using BLL.DTOs;
using DAL.Entities;
using DAL.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BCrypt.Net;
using System.Linq;

namespace BLL.Services
{
    public class AuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IAuthSessionRepository _authSessionRepository;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly IGoogleAuthService _googleAuthService;
        private readonly ICognitoAuthService _cognitoAuthService;

        public AuthService(
            IUserRepository userRepository,
            IAuthSessionRepository authSessionRepository,
            IConfiguration configuration,
            IEmailService emailService,
            IGoogleAuthService googleAuthService,
            ICognitoAuthService cognitoAuthService)
        {
            _userRepository = userRepository;
            _authSessionRepository = authSessionRepository;
            _configuration = configuration;
            _emailService = emailService;
            _googleAuthService = googleAuthService;
            _cognitoAuthService = cognitoAuthService;
        }

        // ✅ Thêm tất cả methods bị thiếu
        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            // Validate username and email uniqueness
            if (await _userRepository.IsUsernameExistsAsync(request.Username))
            {
                return new AuthResponse { Message = "Username already exists" };
            }

            if (await _userRepository.IsEmailExistsAsync(request.Email))
            {
                return new AuthResponse { Message = "Email already exists" };
            }

            // Create user
            var user = new UserModel
            {
                Username = request.Username,
                LowerUsername = request.Username.ToLower(),
                Email = request.Email,
                NormalizedEmail = request.Email.ToLower(),
                Name = request.Name,
                PhoneNumber = request.PhoneNumber,
                Timezone = request.Timezone,
                Language = request.Language,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                AuthProvider = "LOCAL",
                Status = "ACTIVE"
            };

            var createdUser = await _userRepository.CreateUserAsync(user);
            if (createdUser == null)
            {
                return new AuthResponse { Message = "Failed to create user" };
            }

            return new AuthResponse
            {
                Message = "User registered successfully. Please verify your email.",
                User = MapToUserProfile(createdUser)
            };
        }

        public async Task<AuthResponse> RegisterWithCognitoAsync(RegisterRequest request)
        {
            return await _cognitoAuthService.RegisterWithCognitoAsync(request);
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress, string userAgent)
        {
            // Try to find user by username or email
            UserModel user = null;

            if (request.UsernameOrEmail.Contains("@"))
            {
                user = await _userRepository.GetUserByEmailAsync(request.UsernameOrEmail);
            }
            else
            {
                user = await _userRepository.GetUserByUsernameAsync(request.UsernameOrEmail);
            }

            if (user == null)
            {
                return new AuthResponse { Message = "Invalid credentials" };
            }

            // Check if account is locked
            if (!string.IsNullOrEmpty(user.LockedUntil))
            {
                if (DateTime.TryParse(user.LockedUntil, out DateTime lockTime) && DateTime.UtcNow < lockTime)
                {
                    return new AuthResponse { Message = "Account is temporarily locked. Please try again later." };
                }
            }

            // Check if account is suspended
            if (user.Status == "SUSPENDED")
            {
                return new AuthResponse { Message = "Account is suspended" };
            }

            // Check if this is a Google account trying to login with password
            if (user.AuthProvider == "GOOGLE")
            {
                return new AuthResponse { Message = "This account was created with Google. Please use Google Sign-In." };
            }

            // Check if this is a Cognito account trying to login with password
            if (user.AuthProvider == "COGNITO")
            {
                return new AuthResponse { Message = "This account was created with Cognito. Please use Cognito Sign-In." };
            }

            // Verify password
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                // Increment failed login attempts
                var newFailedAttempts = user.FailedLoginAttempts + 1;
                string lockUntil = null;

                if (newFailedAttempts >= 5)
                {
                    lockUntil = DateTime.UtcNow.AddMinutes(15).ToString("o");
                }

                await _userRepository.UpdateFailedLoginAttemptsAsync(user.UserId, newFailedAttempts, lockUntil);
                return new AuthResponse { Message = "Invalid credentials" };
            }

            // Update last login info
            await _userRepository.UpdateLastLoginAsync(user.UserId, ipAddress, userAgent);

            // Generate tokens
            var accessToken = GenerateAccessToken(user);
            var refreshToken = GenerateOpaqueRefreshToken();
            var refreshTokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken);

            // Create session
            var session = new AuthSessionModel
            {
                UserId = user.UserId,
                RefreshTokenHash = refreshTokenHash,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds()
            };

            await _authSessionRepository.CreateSessionAsync(session);

            return new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                User = MapToUserProfile(user),
                Message = "Login successful"
            };
        }

        public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
        {
            try
            {
                var validSession = await _authSessionRepository.GetSessionByRefreshTokenAsync(request.RefreshToken);

                if (validSession == null || validSession.ExpiresAt < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                {
                    return new AuthResponse { Message = "Invalid or expired refresh token" };
                }

                var user = await _userRepository.GetUserByIdAsync(validSession.UserId);
                if (user == null || user.Status != "ACTIVE")
                {
                    return new AuthResponse { Message = "User not found or inactive" };
                }

                // Handle Cognito refresh token separately
                if (validSession.AuthProvider == "COGNITO" && !string.IsNullOrEmpty(validSession.CognitoRefreshToken))
                {
                    var cognitoRefreshResult = await _cognitoAuthService.RefreshCognitoTokenAsync(validSession.CognitoRefreshToken);

                    if (string.IsNullOrEmpty(cognitoRefreshResult.AccessToken))
                    {
                        return cognitoRefreshResult;
                    }

                    // ✅ Cập nhật Cognito token trong session
                    validSession.CognitoAccessToken = cognitoRefreshResult.AccessToken;

                    // ✅ QUAN TRỌNG: Tạo LOCAL JWT mới thay vì return Cognito token
                    var localAccessToken = GenerateAccessToken(user);

                    return new AuthResponse
                    {
                        AccessToken = localAccessToken, // ✅ Return local JWT
                        RefreshToken = request.RefreshToken,
                        User = MapToUserProfile(user),
                        Message = "Cognito token refreshed successfully"
                    };
                }
                else
                {
                    // Generate new local access token for LOCAL/GOOGLE users
                    var accessToken = GenerateAccessToken(user);

                    return new AuthResponse
                    {
                        AccessToken = accessToken,
                        RefreshToken = request.RefreshToken,
                        User = MapToUserProfile(user),
                        Message = "Token refreshed successfully"
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RefreshToken error: {ex.Message}");
                return new AuthResponse { Message = "Invalid or expired refresh token" };
            }
        }

        public async Task<bool> LogoutAsync(LogoutRequest request, string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(request?.RefreshToken))
                {
                    var session = await _authSessionRepository.GetSessionByRefreshTokenAsync(request.RefreshToken);
                    if (session != null)
                    {
                        userId = session.UserId;
                    }
                }

                if (string.IsNullOrEmpty(userId))
                {
                    return false;
                }

                if (request.LogoutAllDevices)
                {
                    var allSessions = await _authSessionRepository.GetUserSessionsAsync(userId);

                    foreach (var session in allSessions.Where(s => s.AuthProvider == "COGNITO" && !string.IsNullOrEmpty(s.CognitoAccessToken)))
                    {
                        try
                        {
                            // Optionally invalidate Cognito token
                        }
                        catch
                        {
                            // Log error but continue
                        }
                    }

                    return await _authSessionRepository.DeleteAllUserSessionsAsync(userId);
                }
                else if (!string.IsNullOrEmpty(request.RefreshToken))
                {
                    var session = await _authSessionRepository.GetSessionByRefreshTokenAsync(request.RefreshToken);

                    if (session != null && session.UserId == userId)
                    {
                        return await _authSessionRepository.DeleteSessionAsync(session.SessionId);
                    }
                }

                return await _authSessionRepository.DeleteAllUserSessionsAsync(userId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Logout error: {ex.Message}");
                return false;
            }
        }

        public async Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            try
            {
                var user = await _userRepository.GetUserByEmailAsync(request.Email);

                if (user == null)
                {
                    return new ForgotPasswordResponse
                    {
                        Success = true,
                        Message = "Nếu email tồn tại trong hệ thống, bạn sẽ nhận được liên kết đặt lại mật khẩu."
                    };
                }

                if (user.Status != "ACTIVE" || user.AuthProvider != "LOCAL")
                {
                    return new ForgotPasswordResponse
                    {
                        Success = true,
                        Message = "Nếu email tồn tại trong hệ thống, bạn sẽ nhận được liên kết đặt lại mật khẩu."
                    };
                }

                var resetToken = GenerateResetPasswordToken();
                var tokenExpiry = DateTime.UtcNow.AddHours(1);

                await _userRepository.UpdatePasswordResetTokenAsync(user.UserId, resetToken, tokenExpiry.ToString("o"));

                var emailSent = await _emailService.SendResetPasswordEmailAsync(user.Email, resetToken, user.Name);

                return new ForgotPasswordResponse
                {
                    Success = true,
                    Message = "Nếu email tồn tại trong hệ thống, bạn sẽ nhận được liên kết đặt lại mật khẩu."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ForgotPassword error: {ex.Message}");
                return new ForgotPasswordResponse
                {
                    Success = false,
                    Message = "Đã xảy ra lỗi. Vui lòng thử lại sau."
                };
            }
        }

        public async Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request)
        {
            try
            {
                var user = await _userRepository.GetUserByPasswordResetTokenAsync(request.Token);

                if (user == null)
                {
                    return new AuthResponse { Message = "Token đặt lại mật khẩu không hợp lệ hoặc đã hết hạn." };
                }

                if (DateTime.TryParse(user.PasswordResetTokenExpiry, out DateTime expiry) && DateTime.UtcNow > expiry)
                {
                    return new AuthResponse { Message = "Token đặt lại mật khẩu đã hết hạn." };
                }

                var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                await _userRepository.UpdatePasswordAsync(user.UserId, newPasswordHash);

                await _userRepository.ClearPasswordResetTokenAsync(user.UserId);

                await _authSessionRepository.DeleteAllUserSessionsAsync(user.UserId);

                return new AuthResponse
                {
                    Message = "Mật khẩu đã được đặt lại thành công. Vui lòng đăng nhập lại.",
                    User = MapToUserProfile(user)
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ResetPassword error: {ex.Message}");
                return new AuthResponse { Message = "Đã xảy ra lỗi. Vui lòng thử lại sau." };
            }
        }

        public async Task<GoogleLoginResponse> GoogleLoginAsync(GoogleLoginRequest request, string ipAddress, string userAgent)
        {
            try
            {
                var googleUser = await _googleAuthService.VerifyGoogleTokenAsync(request.IdToken);
                if (googleUser == null)
                {
                    return new GoogleLoginResponse { Message = "Invalid Google token" };
                }

                var existingUser = await _userRepository.GetUserByEmailAsync(googleUser.Email);

                if (existingUser != null)
                {
                    if (existingUser.Status == "SUSPENDED")
                    {
                        return new GoogleLoginResponse { Message = "Account is suspended" };
                    }

                    if (existingUser.AuthProvider == "LOCAL")
                    {
                        existingUser.AuthProvider = "GOOGLE";
                        existingUser.EmailVerified = true;
                        existingUser.Status = "ACTIVE";
                        existingUser.Avatar = googleUser.Picture;
                        existingUser.UpdatedAt = DateTime.UtcNow.ToString("o");

                        await _userRepository.UpdateUserAsync(existingUser);
                    }

                    await _userRepository.UpdateLastLoginAsync(existingUser.UserId, ipAddress, userAgent);

                    var accessToken = GenerateAccessToken(existingUser);
                    var refreshToken = GenerateOpaqueRefreshToken();
                    var refreshTokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken);

                    var session = new AuthSessionModel
                    {
                        UserId = existingUser.UserId,
                        RefreshTokenHash = refreshTokenHash,
                        IpAddress = ipAddress,
                        UserAgent = userAgent,
                        ExpiresAt = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds()
                    };

                    await _authSessionRepository.CreateSessionAsync(session);

                    return new GoogleLoginResponse
                    {
                        AccessToken = accessToken,
                        RefreshToken = refreshToken,
                        User = MapToUserProfile(existingUser),
                        Message = "Login successful",
                        IsNewUser = false
                    };
                }
                else
                {
                    var newUser = new UserModel
                    {
                        Username = GenerateUsernameFromEmail(googleUser.Email),
                        LowerUsername = GenerateUsernameFromEmail(googleUser.Email).ToLower(),
                        Email = googleUser.Email,
                        NormalizedEmail = googleUser.Email.ToLower(),
                        Name = googleUser.Name,
                        Avatar = googleUser.Picture,
                        AuthProvider = "GOOGLE",
                        Status = "ACTIVE",
                        EmailVerified = true,
                        Language = "vi",
                        Timezone = "Asia/Ho_Chi_Minh"
                    };

                    var createdUser = await _userRepository.CreateUserAsync(newUser);
                    if (createdUser == null)
                    {
                        return new GoogleLoginResponse { Message = "Failed to create user account" };
                    }

                    var accessToken = GenerateAccessToken(createdUser);
                    var refreshToken = GenerateOpaqueRefreshToken();
                    var refreshTokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken);

                    var session = new AuthSessionModel
                    {
                        UserId = createdUser.UserId,
                        RefreshTokenHash = refreshTokenHash,
                        IpAddress = ipAddress,
                        UserAgent = userAgent,
                        ExpiresAt = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds()
                    };

                    await _authSessionRepository.CreateSessionAsync(session);

                    return new GoogleLoginResponse
                    {
                        AccessToken = accessToken,
                        RefreshToken = refreshToken,
                        User = MapToUserProfile(createdUser),
                        Message = "Account created and login successful",
                        IsNewUser = true
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GoogleLogin error: {ex.Message}");
                return new GoogleLoginResponse { Message = "An error occurred during Google login" };
            }
        }

        public async Task<AuthResponse> CognitoLoginAsync(LoginRequest request, string ipAddress, string userAgent)
        {
            try
            {
                var cognitoResult = await _cognitoAuthService.LoginWithCognitoAsync(request);

                if (string.IsNullOrEmpty(cognitoResult.AccessToken))
                    return cognitoResult;

                var cognitoUser = await _cognitoAuthService.GetUserFromTokenAsync(cognitoResult.AccessToken);

                var localUser = await GetOrCreateLocalUserFromCognito(cognitoUser);

                // ✅ THAY ĐỔI: Tạo JWT local thay vì dùng Cognito token
                var localAccessToken = GenerateAccessToken(localUser);

                var refreshToken = GenerateOpaqueRefreshToken();
                var refreshTokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken);

                var session = new AuthSessionModel
                {
                    UserId = localUser.UserId,
                    RefreshTokenHash = refreshTokenHash,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds(),
                    CognitoAccessToken = cognitoResult.AccessToken, // Giữ lại để tham chiếu
                    CognitoRefreshToken = cognitoResult.RefreshToken,
                    AuthProvider = "COGNITO"
                };

                await _authSessionRepository.CreateSessionAsync(session);

                return new AuthResponse
                {
                    AccessToken = localAccessToken, // ✅ THAY ĐỔI: Return local JWT
                    RefreshToken = refreshToken,
                    User = MapToUserProfile(localUser),
                    Message = "Cognito login successful"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CognitoLogin error: {ex.Message}");
                return new AuthResponse { Message = "Cognito authentication failed" };
            }
        }

        public async Task<CognitoConfirmSignUpResponse> ConfirmCognitoSignUpAsync(CognitoConfirmSignUpRequest request)
        {
            return await _cognitoAuthService.ConfirmSignUpAsync(request);
        }

        public async Task<CognitoConfirmSignUpResponse> ResendCognitoConfirmationCodeAsync(CognitoResendConfirmationRequest request)
        {
            return await _cognitoAuthService.ResendConfirmationCodeAsync(request);
        }

        // ✅ Thêm methods mới cho Cognito Forgot Password
        public async Task<CognitoForgotPasswordResponse> CognitoForgotPasswordAsync(CognitoForgotPasswordRequest request)
        {
            return await _cognitoAuthService.ForgotPasswordAsync(request);
        }

        public async Task<CognitoConfirmForgotPasswordResponse> CognitoConfirmForgotPasswordAsync(CognitoConfirmForgotPasswordRequest request)
        {
            return await _cognitoAuthService.ConfirmForgotPasswordAsync(request);
        }

        // ✅ THÊM METHOD MỚI - Exchange OAuth code for tokens
        public async Task<AuthResponse> ExchangeCognitoOAuthCodeAsync(CognitoOAuthCodeRequest request)
        {
            try
            {
                var cognitoDomain = _configuration["AWS:Cognito:Domain"];
                var clientId = _configuration["AWS:Cognito:ClientId"];
                var clientSecret = _configuration["AWS:Cognito:ClientSecret"];

                using var httpClient = new HttpClient();
                var tokenEndpoint = $"{cognitoDomain}/oauth2/token";

                var requestBody = new Dictionary<string, string>
                {
                    { "grant_type", "authorization_code" },
                    { "client_id", clientId },
                    { "code", request.Code },
                    { "redirect_uri", request.RedirectUri }
                };

                var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {authHeader}");

                var response = await httpClient.PostAsync(tokenEndpoint, new FormUrlEncodedContent(requestBody));
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Token exchange failed: {responseContent}");
                    return new AuthResponse { Message = "Failed to exchange authorization code" };
                }

                var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
                var idToken = tokenResponse["id_token"].ToString();
                var accessToken = tokenResponse["access_token"].ToString();
                var refreshToken = tokenResponse["refresh_token"].ToString();

                // Decode id_token to get user info
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(idToken);
                
                var email = jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
                var name = jwtToken.Claims.FirstOrDefault(c => c.Type == "name")?.Value;
                var sub = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

                // Get or create local user
                var localUser = await GetOrCreateLocalUserFromOAuth(email, name, sub);

                // Generate local JWT
                var localAccessToken = GenerateAccessToken(localUser);
                var localRefreshToken = GenerateOpaqueRefreshToken();
                var refreshTokenHash = BCrypt.Net.BCrypt.HashPassword(localRefreshToken);

                var session = new AuthSessionModel
                {
                    UserId = localUser.UserId,
                    RefreshTokenHash = refreshTokenHash,
                    IpAddress = "oauth",
                    UserAgent = "cognito_oauth",
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds(),
                    CognitoAccessToken = accessToken,
                    CognitoRefreshToken = refreshToken,
                    AuthProvider = "COGNITO_OAUTH"
                };

                await _authSessionRepository.CreateSessionAsync(session);

                return new AuthResponse
                {
                    AccessToken = localAccessToken,
                    RefreshToken = localRefreshToken,
                    User = MapToUserProfile(localUser),
                    Message = "OAuth login successful"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ExchangeCognitoOAuthCode error: {ex.Message}");
                return new AuthResponse { Message = $"OAuth exchange failed: {ex.Message}" };
            }
        }

        private async Task<UserModel> GetOrCreateLocalUserFromOAuth(string email, string name, string sub)
        {
            var existingUser = await _userRepository.GetUserByEmailAsync(email);

            if (existingUser != null)
            {
                existingUser.AuthProvider = "COGNITO_OAUTH";
                existingUser.EmailVerified = true;
                existingUser.Status = "ACTIVE";
                existingUser.UpdatedAt = DateTime.UtcNow.ToString("o");
                await _userRepository.UpdateUserAsync(existingUser);
                return existingUser;
            }

            var newUser = new UserModel
            {
                Username = GenerateUsernameFromEmail(email),
                LowerUsername = GenerateUsernameFromEmail(email).ToLower(),
                Email = email,
                NormalizedEmail = email.ToLower(),
                Name = name ?? email.Split('@')[0],
                AuthProvider = "COGNITO_OAUTH",
                Status = "ACTIVE",
                EmailVerified = true,
                Language = "vi",
                Timezone = "Asia/Ho_Chi_Minh"
            };

            return await _userRepository.CreateUserAsync(newUser);
        }

        // ✅ THÊM TẤT CẢ PRIVATE METHODS BỊ THIẾU
        private async Task<UserModel> GetOrCreateLocalUserFromCognito(CognitoUser cognitoUser)
        {
            if (cognitoUser == null || string.IsNullOrEmpty(cognitoUser.Email) || string.IsNullOrEmpty(cognitoUser.Username))
            {
                throw new ArgumentException("Invalid Cognito user data");
            }

            var existingUser = await _userRepository.GetUserByEmailAsync(cognitoUser.Email);

            if (existingUser != null)
            {
                existingUser.AuthProvider = "COGNITO";
                existingUser.EmailVerified = cognitoUser.EmailVerified;
                existingUser.Status = "ACTIVE";
                existingUser.UpdatedAt = DateTime.UtcNow.ToString("o");

                await _userRepository.UpdateUserAsync(existingUser);
                return existingUser;
            }

            var newUser = new UserModel
            {
                Username = cognitoUser.Username,
                LowerUsername = cognitoUser.Username.ToLower(),
                Email = cognitoUser.Email,
                NormalizedEmail = cognitoUser.Email.ToLower(),
                Name = cognitoUser.Name ?? cognitoUser.Username,
                PhoneNumber = cognitoUser.PhoneNumber,
                AuthProvider = "COGNITO",
                Status = "ACTIVE",
                EmailVerified = cognitoUser.EmailVerified,
                Language = "en",
                Timezone = "UTC",
                CreatedAt = DateTime.UtcNow.ToString("o"),
                UpdatedAt = DateTime.UtcNow.ToString("o")
            };

            return await _userRepository.CreateUserAsync(newUser);
        }

        private string GenerateUsernameFromEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return "user" + Guid.NewGuid().ToString("N")[..8];

            var emailPrefix = email.Split('@')[0];
            var cleanUsername = emailPrefix.Replace(".", "").Replace("+", "").Replace("-", "");

            return $"{cleanUsername}{new Random().Next(1000, 9999)}";
        }

        private string GenerateAccessToken(UserModel user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["JWT:SecretKey"] ?? "your-super-secret-jwt-key-here-make-it-long-enough");

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("userId", user.UserId),
                    new Claim("name", user.Name),
                    new Claim("email", user.Email),
                    new Claim("authProvider", user.AuthProvider),
                    new Claim("isAdmin", user.IsAdmin == 1 ? "1" : "0")
            }),
                Expires = DateTime.UtcNow.AddMinutes(15), // Short-lived access token
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["JWT:Issuer"] ?? "TaskHub",
                Audience = _configuration["JWT:Audience"] ?? "TaskHub"
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private string GenerateOpaqueRefreshToken()
        {
            var randomBytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
        }

        private string GenerateResetPasswordToken()
        {
            var randomBytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
        }

        private UserProfile MapToUserProfile(UserModel user)
        {
            return new UserProfile
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                Name = user.Name,
                Avatar = user.Avatar,
                Status = user.Status,
                EmailVerified = user.EmailVerified,
                AuthProvider = user.AuthProvider,
                IsAdmin = user.IsAdmin,
                CreatedAt = user.CreatedAt,
                LastActiveAt = user.LastActiveAt
            };
        }

        public async Task<AuthSessionModel> GetSessionByRefreshTokenAsync(string refreshToken)
        {
            return await _authSessionRepository.GetSessionByRefreshTokenAsync(refreshToken);
        }
    }
}