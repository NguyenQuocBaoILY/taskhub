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

        public AuthService(
            IUserRepository userRepository,
            IAuthSessionRepository authSessionRepository,
            IConfiguration configuration)
        {
            _userRepository = userRepository;
            _authSessionRepository = authSessionRepository;
            _configuration = configuration;
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
                    new Claim("authProvider", user.AuthProvider)
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