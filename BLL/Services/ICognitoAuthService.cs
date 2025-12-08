using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using BLL.DTOs;

namespace BLL.Services
{
    public interface ICognitoAuthService
    {
        Task<AuthResponse> RegisterWithCognitoAsync(RegisterRequest request);
        Task<AuthResponse> LoginWithCognitoAsync(LoginRequest request);
        Task<AuthResponse> RefreshCognitoTokenAsync(string refreshToken);
        Task<bool> ValidateCognitoTokenAsync(string accessToken);
        Task<CognitoUser> GetUserFromTokenAsync(string accessToken);
        
        // ✅ Sử dụng tên mới
        Task<CognitoConfirmSignUpResponse> ConfirmSignUpAsync(CognitoConfirmSignUpRequest request);
        Task<CognitoConfirmSignUpResponse> ResendConfirmationCodeAsync(CognitoResendConfirmationRequest request);
        
        // ✅ Thêm methods cho forgot password
        Task<CognitoForgotPasswordResponse> ForgotPasswordAsync(CognitoForgotPasswordRequest request);
        Task<CognitoConfirmForgotPasswordResponse> ConfirmForgotPasswordAsync(CognitoConfirmForgotPasswordRequest request);
    }
}