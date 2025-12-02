using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs
{
    public class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }

    public class ResetPasswordRequest
    {
        [Required]
        public string Token { get; set; }

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; }
    }

    public class ForgotPasswordResponse
    {
        public string Message { get; set; }
        public bool Success { get; set; }
    }
}