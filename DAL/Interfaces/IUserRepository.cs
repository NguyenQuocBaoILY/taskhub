using DAL.Entities;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IUserRepository
    {
        Task<UserModel> GetUserByIdAsync(string userId);
        Task<UserModel> GetUserByUsernameAsync(string username);
        Task<UserModel> GetUserByEmailAsync(string email);
        Task<UserModel> CreateUserAsync(UserModel user);
        Task<UserModel> UpdateUserAsync(UserModel user);
        Task<bool> IsUsernameExistsAsync(string username);
        Task<bool> IsEmailExistsAsync(string email);
        Task<bool> UpdateFailedLoginAttemptsAsync(string userId, int attempts, string lockedUntil = null);
        Task<bool> UpdateLastLoginAsync(string userId, string ipAddress, string userAgent);
    }
}