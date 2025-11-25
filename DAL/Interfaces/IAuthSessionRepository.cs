using DAL.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IAuthSessionRepository
    {
        Task<AuthSessionModel> CreateSessionAsync(AuthSessionModel session);
        Task<AuthSessionModel> GetSessionByTokenHashAsync(string tokenHash);
        Task<AuthSessionModel> GetSessionByRefreshTokenAsync(string refreshToken); // ✅ Phải có method này
        Task<List<AuthSessionModel>> GetUserSessionsAsync(string userId);
        Task<List<AuthSessionModel>> GetAllActiveSessionsAsync();                  // ✅ Và method này
        Task<bool> DeleteSessionAsync(string sessionId);
        Task<bool> DeleteAllUserSessionsAsync(string userId);
        Task<bool> UpdateSessionLastLoginAsync(string sessionId, string lastLoginAt);
    }
}