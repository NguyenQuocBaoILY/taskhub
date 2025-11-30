using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services
{
    public class UserService
    {
        private readonly DAL.Interfaces.IUserRepository _userRepository;

        public UserService(DAL.Interfaces.IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public Task<DAL.Entities.UserModel> GetUserByIdAsync(string userId)
        {
            return _userRepository.GetUserByIdAsync(userId);
        }

        public Task<DAL.Entities.UserModel> GetUserByUsernameAsync(string username)
        {
            return _userRepository.GetUserByUsernameAsync(username);
        }

        public Task<DAL.Entities.UserModel> GetUserByEmailAsync(string email)
        {
            return _userRepository.GetUserByEmailAsync(email);
        }

        public Task<IEnumerable<DAL.Entities.UserModel>> SearchUsersAsync(string query, int limit) => _userRepository.SearchUsersAsync(query, limit);

        public Task<DAL.Entities.UserModel> CreateUserAsync(DAL.Entities.UserModel user)
        {
            return _userRepository.CreateUserAsync(user);
        }

        public Task<DAL.Entities.UserModel> UpdateUserAsync(DAL.Entities.UserModel user)
        {
            return _userRepository.UpdateUserAsync(user);
        }

        public Task<bool> IsUsernameExistsAsync(string username)
        {
            return _userRepository.IsUsernameExistsAsync(username);
        }

        public Task<bool> IsEmailExistsAsync(string email)
        {
            return _userRepository.IsEmailExistsAsync(email);
        }

        public Task<bool> UpdateFailedLoginAttemptsAsync(string userId, int attempts, string lockedUntil = null)
        {
            return _userRepository.UpdateFailedLoginAttemptsAsync(userId, attempts, lockedUntil);
        }

        public Task<bool> UpdateLastLoginAsync(string userId, string ipAddress, string userAgent)
        {
            return _userRepository.UpdateLastLoginAsync(userId, ipAddress, userAgent);
        }
    }
}
