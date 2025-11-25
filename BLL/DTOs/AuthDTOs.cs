using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs
{
    public class LoginRequest
    {
        [Required]
        public string UsernameOrEmail { get; set; }

        [Required]
        [MinLength(6)]
        public string Password { get; set; }
    }

    public class RegisterRequest
    {
        [Required]
        [MinLength(3)]
        [MaxLength(30)]
        public string Username { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [MinLength(6)]
        public string Password { get; set; }

        [Required]
        public string Name { get; set; }

        public string PhoneNumber { get; set; }
        public string Timezone { get; set; }
        public string Language { get; set; } = "en";
    }

    public class AuthResponse
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public UserProfile User { get; set; }
        public string Message { get; set; }
    }

    public class UserProfile
    {
        public string UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
        public string Avatar { get; set; }
        public string Status { get; set; }
        public bool EmailVerified { get; set; }
        public string CreatedAt { get; set; }
        public string LastActiveAt { get; set; }
    }

    public class RefreshTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; }
    }

    public class LogoutRequest
    {
        public string RefreshToken { get; set; }
        public bool LogoutAllDevices { get; set; } = false;
    }
}