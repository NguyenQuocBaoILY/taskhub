using BLL.DTOs;

namespace BLL.Services
{
    public interface IGoogleAuthService
    {
        Task<GoogleUser> VerifyGoogleTokenAsync(string idToken);
    }
}