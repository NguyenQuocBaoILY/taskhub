using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.DTOs
{
    // Authentication Response
    public class AuthResponse
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public string Message { get; set; }
        public UserProfile User { get; set; }
    }

    // Registration Request
    public class RegisterRequest
    {
        [Required]
        public string Username { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }

        [Required]
        public string Name { get; set; }

        public string PhoneNumber { get; set; }
        public string Timezone { get; set; }
        public string Language { get; set; }
    }

    // Login Request
    public class LoginRequest
    {
        [Required]
        public string UsernameOrEmail { get; set; }

        [Required]
        public string Password { get; set; }
    }

    // User Profile
    public class UserProfile
    {
        public string UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public string Avatar { get; set; }
        public string Status { get; set; }
        public bool EmailVerified { get; set; }
        public string AuthProvider { get; set; }
        public bool IsAdmin { get; set; }
        public string CreatedAt { get; set; }
        public string LastActiveAt { get; set; }
    }

    // Refresh Token Request
    public class RefreshTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; }
    }

    // Logout Request
    public class LogoutRequest
    {
        public string RefreshToken { get; set; }
        public bool LogoutAllDevices { get; set; } = false;
    }

    // Google Login Request
    public class GoogleLoginRequest
    {
        [Required]
        public string IdToken { get; set; }
    }

    // Google Login Response
    public class GoogleLoginResponse
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public string Message { get; set; }
        public UserProfile User { get; set; }
        public bool IsNewUser { get; set; }
    }

    // Google User (from Google API)
    public class GoogleUser
    {
        public string Email { get; set; }
        public string Name { get; set; }
        public string Picture { get; set; }
        public bool EmailVerified { get; set; }
    }

    // Forgot Password Request
    public class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }

    // Forgot Password Response
    public class ForgotPasswordResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    // Reset Password Request
    public class ResetPasswordRequest
    {
        [Required]
        public string Token { get; set; }

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; }
    }

    // Cognito User
    public class CognitoUser
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public bool EmailVerified { get; set; }
    }

    // Cognito Confirm Sign Up Request
    public class CognitoConfirmSignUpRequest
    {
        [Required]
        public string Username { get; set; }

        [Required]
        public string ConfirmationCode { get; set; }
    }

    // Cognito Confirm Sign Up Response
    public class CognitoConfirmSignUpResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    // Cognito Resend Confirmation Request
    public class CognitoResendConfirmationRequest
    {
        [Required]
        public string Username { get; set; }
    }
}
