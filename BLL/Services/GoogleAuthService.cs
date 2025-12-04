using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using BLL.DTOs;
using System.Threading.Tasks;

namespace BLL.Services
{
    public class GoogleAuthService : IGoogleAuthService
    {
        private readonly IConfiguration _configuration;

        public GoogleAuthService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<GoogleUser> VerifyGoogleTokenAsync(string idToken)
        {
            try
            {
                var clientId = _configuration["GoogleAuth:ClientId"];
                
                var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { clientId }
                });

                return new GoogleUser
                {
                    Email = payload.Email,
                    Name = payload.Name,
                    Picture = payload.Picture,
                    EmailVerified = payload.EmailVerified
                    // Removed GivenName and FamilyName as they don't exist in GoogleJsonWebSignature.Payload
                };
            }
            catch (InvalidJwtException)
            {
                return null;
            }
        }
    }

    public class GoogleUserInfo
    {
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Picture { get; set; } = string.Empty;
        public bool EmailVerified { get; set; }
        public string GoogleId { get; set; } = string.Empty;
        public string GivenName { get; set; } = string.Empty;
        public string FamilyName { get; set; } = string.Empty;
    }
}