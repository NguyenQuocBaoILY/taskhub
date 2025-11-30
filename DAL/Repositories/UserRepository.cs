using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using DAL.Entities;
using DAL.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DAL.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IDynamoDBContext _context;

        public UserRepository(IAmazonDynamoDB client)
        {
            _context = new DynamoDBContext(client);
        }

        public async Task<UserModel> GetUserByIdAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return null;

            var pk = $"USER#{userId}";
            var sk = "PROFILE";

            return await _context.LoadAsync<UserModel>(pk, sk);
        }

        public async Task<UserModel> GetUserByUsernameAsync(string username)
        {
            if (string.IsNullOrEmpty(username))
                return null;

            var queryConfig = new QueryOperationConfig
            {
                IndexName = "GSI6-UsernameLookup",
                KeyExpression = new Expression
                {
                    ExpressionStatement = "GSI6PK = :username",
                    ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                    {
                        { ":username", $"USERNAME#{username.ToLower()}" }
                    }
                },
                Limit = 1
            };

            var search = _context.FromQueryAsync<UserModel>(queryConfig);
            var users = await search.GetRemainingAsync();
            return users.FirstOrDefault();
        }

        public async Task<UserModel> GetUserByEmailAsync(string email)
        {
            if (string.IsNullOrEmpty(email))
                return null;

            var queryConfig = new QueryOperationConfig
            {
                IndexName = "GSI1-EmailLookup",
                KeyExpression = new Expression
                {
                    ExpressionStatement = "GSI1PK = :email",
                    ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                    {
                        { ":email", $"EMAIL#{email.ToLower()}" }
                    }
                },
                Limit = 1
            };

            var search = _context.FromQueryAsync<UserModel>(queryConfig);
            var users = await search.GetRemainingAsync();
            return users.FirstOrDefault();
        }

        public async Task<UserModel> CreateUserAsync(UserModel user)
        {
            if (user == null)
                return null;

            if (string.IsNullOrEmpty(user.UserId))
                user.UserId = Guid.NewGuid().ToString();

            var timestamp = DateTime.UtcNow.ToString("o");
            user.CreatedAt = timestamp;
            user.UpdatedAt = timestamp;
            user.LastActiveAt = timestamp;
            user.LastPasswordChangedAt = timestamp;

            // Set keys
            user.PK = $"USER#{user.UserId}";
            user.SK = "PROFILE";

            // Set GSI keys for lookups
            user.GSI1PK = $"EMAIL#{user.NormalizedEmail}";
            user.GSI1SK = $"USER#{user.UserId}";
            user.GSI6PK = $"USERNAME#{user.LowerUsername}";
            user.GSI6SK = $"USER#{user.UserId}";

            await _context.SaveAsync(user);
            return user;
        }

        public async Task<UserModel> UpdateUserAsync(UserModel user)
        {
            if (user == null || string.IsNullOrEmpty(user.UserId))
                return null;

            user.UpdatedAt = DateTime.UtcNow.ToString("o");
            await _context.SaveAsync(user);
            return user;
        }

        public async Task<bool> IsUsernameExistsAsync(string username)
        {
            var user = await GetUserByUsernameAsync(username);
            return user != null;
        }

        public async Task<bool> IsEmailExistsAsync(string email)
        {
            var user = await GetUserByEmailAsync(email);
            return user != null;
        }

        public async Task<bool> UpdateFailedLoginAttemptsAsync(string userId, int attempts, string lockedUntil = null)
        {
            var user = await GetUserByIdAsync(userId);
            if (user == null) return false;

            user.FailedLoginAttempts = attempts;
            user.LockedUntil = lockedUntil;
            user.UpdatedAt = DateTime.UtcNow.ToString("o");

            await _context.SaveAsync(user);
            return true;
        }

        public async Task<bool> UpdateLastLoginAsync(string userId, string ipAddress, string userAgent)
        {
            var user = await GetUserByIdAsync(userId);
            if (user == null) return false;

            user.LastLoginIp = ipAddress;
            user.LastUserAgent = userAgent;
            user.LastActiveAt = DateTime.UtcNow.ToString("o");
            user.FailedLoginAttempts = 0; // Reset failed attempts on successful login
            user.LockedUntil = null;

            await _context.SaveAsync(user);
            return true;
        }

        // Search users for FE. If query is null/empty, returns up to 'limit' users (scan restricted to EntityType = USER).
        public async Task<IEnumerable<UserModel>> SearchUsersAsync(string query, int limit)
        {
            // If no query provided -> scan only items with EntityType == "USER" to avoid mapping non-user entities
            if (string.IsNullOrWhiteSpace(query))
            {
                var scan = _context.ScanAsync<UserModel>(new List<ScanCondition>
                {
                    new ScanCondition("EntityType", ScanOperator.Equal, "USER")
                });

                var users = await scan.GetRemainingAsync();

                // Dedupe by UserId and limit
                return users
                    .Where(u => !string.IsNullOrEmpty(u.UserId))
                    .GroupBy(u => u.UserId)
                    .Select(g => g.First())
                    .Take(limit);
            }

            query = query.Trim();

            var results = new List<UserModel>();

            // 1) Try exact username lookup via GSI6 (fast and authoritative)
            var exact = await GetUserByUsernameAsync(query);
            if (exact != null)
                results.Add(exact);

            // 2) Fallback: scan USER items only and filter by Username or Name containing the query
            var scanFiltered = _context.ScanAsync<UserModel>(new List<ScanCondition>
            {
                new ScanCondition("EntityType", ScanOperator.Equal, "USER")
            });

            var allUsers = await scanFiltered.GetRemainingAsync();

            var filtered = allUsers
                .Where(u =>
                    (!string.IsNullOrEmpty(u.Username) && u.Username.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(u.Name) && u.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            results.AddRange(filtered);

            // Deduplicate by UserId, preserve first occurrence (exact match first), limit
            var combined = results
                .Where(u => !string.IsNullOrEmpty(u.UserId))
                .GroupBy(u => u.UserId)
                .Select(g => g.First())
                .Take(limit);

            return combined;
        }
    }
}