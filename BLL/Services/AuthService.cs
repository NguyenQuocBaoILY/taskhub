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

namespace BLL.Services
{
    public class AuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IAuthSessionRepository _authSessionRepository;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly IGoogleAuthService _googleAuthService;

        public AuthService(
            IUserRepository userRepository,
            IAuthSessionRepository authSessionRepository,
            IConfiguration configuration,
            IEmailService emailService,
            IGoogleAuthService googleAuthService)
        {
            _userRepository = userRepository;
            _authSessionRepository = authSessionRepository;
            _configuration = configuration;
            _emailService = emailService;
            _googleAuthService = googleAuthService;
        }

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
                Status = "ACTIVE" // ✅ Thay đổi thành ACTIVE để test dễ hơn
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
            var refreshToken = GenerateOpaqueRefreshToken(); // Random string
            var refreshTokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken);

            // Create session
            var session = new AuthSessionModel
            {
                UserId = user.UserId,
                RefreshTokenHash = refreshTokenHash, // Store hashed version
                IpAddress = ipAddress,
                UserAgent = userAgent,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds() // 30 days
            };

            await _authSessionRepository.CreateSessionAsync(session);

            return new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken, // Return plain text to client
                User = MapToUserProfile(user),
                Message = "Login successful"
            };
        }

        public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
        {
            try
            {
                // ✅ Sử dụng method mới
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

                // Generate new access token
                var accessToken = GenerateAccessToken(user);

                return new AuthResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = request.RefreshToken,
                    User = MapToUserProfile(user),
                    Message = "Token refreshed successfully"
                };
            }
            catch (Exception ex)
            {
                // Log exception for debugging
                Console.WriteLine($"RefreshToken error: {ex.Message}");
                return new AuthResponse { Message = "Invalid or expired refresh token" };
            }
        }

        public async Task<bool> LogoutAsync(LogoutRequest request, string userId)
        {
            try
            {
                // ✅ Nếu không có userId từ JWT, tìm từ refresh token
                if (string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(request?.RefreshToken))
                {
                    var session = await _authSessionRepository.GetSessionByRefreshTokenAsync(request.RefreshToken);
                    if (session != null)
                    {
                        userId = session.UserId;
                    }
                }

                // ✅ Kiểm tra có userId không
                if (string.IsNullOrEmpty(userId))
                {
                    return false;
                }

                if (request.LogoutAllDevices)
                {
                    return await _authSessionRepository.DeleteAllUserSessionsAsync(userId);
                }
                else if (!string.IsNullOrEmpty(request.RefreshToken))
                {
                    // ✅ Tìm session bằng refresh token
                    var session = await _authSessionRepository.GetSessionByRefreshTokenAsync(request.RefreshToken);
                    
                    if (session != null && session.UserId == userId)
                    {
                        return await _authSessionRepository.DeleteSessionAsync(session.SessionId);
                    }
                }

                // ✅ Fallback: xóa tất cả session của user
                return await _authSessionRepository.DeleteAllUserSessionsAsync(userId);
            }
            catch (Exception ex)
            {
                // Log exception for debugging
                Console.WriteLine($"Logout error: {ex.Message}");
                return false;
            }
        }

        public async Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            try
            {
                var user = await _userRepository.GetUserByEmailAsync(request.Email);
                
                // Luôn trả về success để không leak thông tin user có tồn tại hay không
                if (user == null)
                {
                    return new ForgotPasswordResponse 
                    { 
                        Success = true, 
                        Message = "Nếu email tồn tại trong hệ thống, bạn sẽ nhận được liên kết đặt lại mật khẩu." 
                    };
                }

                // Chỉ gửi email cho user active và LOCAL auth
                if (user.Status != "ACTIVE" || user.AuthProvider != "LOCAL")
                {
                    return new ForgotPasswordResponse 
                    { 
                        Success = true, 
                        Message = "Nếu email tồn tại trong hệ thống, bạn sẽ nhận được liên kết đặt lại mật khẩu." 
                    };
                }

                // Tạo reset token
                var resetToken = GenerateResetPasswordToken();
                var tokenExpiry = DateTime.UtcNow.AddHours(1); // 1 giờ

                // Lưu token vào database (cần thêm fields vào UserModel)
                await _userRepository.UpdatePasswordResetTokenAsync(user.UserId, resetToken, tokenExpiry.ToString("o"));

                // Gửi email
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

                // Kiểm tra token có hết hạn không
                if (DateTime.TryParse(user.PasswordResetTokenExpiry, out DateTime expiry) && DateTime.UtcNow > expiry)
                {
                    return new AuthResponse { Message = "Token đặt lại mật khẩu đã hết hạn." };
                }

                // Cập nhật mật khẩu mới
                var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                await _userRepository.UpdatePasswordAsync(user.UserId, newPasswordHash);

                // Xóa token reset
                await _userRepository.ClearPasswordResetTokenAsync(user.UserId);

                // Xóa tất cả sessions của user (logout khỏi tất cả thiết bị)
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
                // Verify Google ID token
                var googleUser = await _googleAuthService.VerifyGoogleTokenAsync(request.IdToken);
                if (googleUser == null)
                {
                    return new GoogleLoginResponse { Message = "Invalid Google token" };
                }

                // Check if user exists
                var existingUser = await _userRepository.GetUserByEmailAsync(googleUser.Email);
                
                if (existingUser != null)
                {
                    // User exists - login
                    if (existingUser.Status == "SUSPENDED")
                    {
                        return new GoogleLoginResponse { Message = "Account is suspended" };
                    }

                    // Update user info from Google if needed
                    if (existingUser.AuthProvider == "LOCAL")
                    {
                        // Convert local account to Google account
                        existingUser.AuthProvider = "GOOGLE";
                        existingUser.EmailVerified = true;
                        existingUser.Status = "ACTIVE";
                        existingUser.Avatar = googleUser.Picture;
                        existingUser.UpdatedAt = DateTime.UtcNow.ToString("o");

                        await _userRepository.UpdateUserAsync(existingUser);
                    }

                    // Update last login info
                    await _userRepository.UpdateLastLoginAsync(existingUser.UserId, ipAddress, userAgent);

                    // Generate tokens
                    var accessToken = GenerateAccessToken(existingUser);
                    var refreshToken = GenerateOpaqueRefreshToken();
                    var refreshTokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken);

                    // Create session
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
                    // User doesn't exist - create new account
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
                        Language = "vi", // Default language
                        Timezone = "Asia/Ho_Chi_Minh" // Default timezone
                    };

                    var createdUser = await _userRepository.CreateUserAsync(newUser);
                    if (createdUser == null)
                    {
                        return new GoogleLoginResponse { Message = "Failed to create user account" };
                    }

                    // Generate tokens for new user
                    var accessToken = GenerateAccessToken(createdUser);
                    var refreshToken = GenerateOpaqueRefreshToken();
                    var refreshTokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken);

                    // Create session
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

        private string GenerateUsernameFromEmail(string email)
        {
            // Extract username part from email and make it unique if needed
            var emailPrefix = email.Split('@')[0];
            var cleanUsername = emailPrefix.Replace(".", "").Replace("+", "").Replace("-", "");
            
            // Ensure it's unique by appending random numbers if needed
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
                    new Claim("username", user.Username),
                    new Claim("email", user.Email),
                    new Claim("authProvider", user.AuthProvider),
                    new Claim("isAdmin", user.IsAdmin.ToString().ToLower()) // Default role
                }),
                Expires = DateTime.UtcNow.AddMinutes(15), // Short-lived access token
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["JWT:Issuer"] ?? "TaskHub",
                Audience = _configuration["JWT:Audience"] ?? "TaskHub"
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        private string GenerateOpaqueRefreshToken()
        {
            // Cryptographically secure random string
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