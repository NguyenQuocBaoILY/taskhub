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
                    GivenName = payload.GivenName,
                    FamilyName = payload.FamilyName
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
        public string Email { get; set; }
        public string Name { get; set; }
        public string Picture { get; set; }
        public bool EmailVerified { get; set; }
        public string GoogleId { get; set; }
        public string GivenName { get; set; }
        public string FamilyName { get; set; }
    }
}