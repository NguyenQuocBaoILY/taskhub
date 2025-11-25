using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using DAL.Entities;
using DAL.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using BCrypt.Net;

namespace DAL.Repositories
{
    public class AuthSessionRepository : IAuthSessionRepository
    {
        private readonly IDynamoDBContext _context;

        public AuthSessionRepository(IAmazonDynamoDB client)
        {
            _context = new DynamoDBContext(client);
        }

        public async Task<AuthSessionModel> CreateSessionAsync(AuthSessionModel session)
        {
            if (session == null)
                return null;

            if (string.IsNullOrEmpty(session.SessionId))
                session.SessionId = Guid.NewGuid().ToString();

            var timestamp = DateTime.UtcNow.ToString("o");
            session.CreatedAt = timestamp;
            session.LastLoginAt = timestamp;
            session.EntityType = "SESSION"; // ✅ Set EntityType

            // Set keys
            session.PK = $"SESSION#{session.SessionId}";
            session.SK = $"USER#{session.UserId}";

            // GSI1 for user sessions lookup
            session.GSI1PK = $"USER#{session.UserId}#SESSION";
            session.GSI1SK = timestamp;

            await _context.SaveAsync(session);
            return session;
        }

        public async Task<AuthSessionModel> GetSessionByTokenHashAsync(string tokenHash)
        {
            if (string.IsNullOrEmpty(tokenHash))
                return null;

            var scanConfig = new ScanOperationConfig
            {
                FilterExpression = new Expression
                {
                    ExpressionStatement = "RefreshTokenHash = :tokenHash",
                    ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                    {
                        { ":tokenHash", tokenHash }
                    }
                },
                Limit = 1
            };

            var search = _context.FromScanAsync<AuthSessionModel>(scanConfig);
            var sessions = await search.GetRemainingAsync();
            return sessions.FirstOrDefault();
        }

        public async Task<AuthSessionModel> GetSessionByRefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrEmpty(refreshToken))
                return null;

            try
            {
                // ✅ Tối ưu hóa: chỉ lấy active sessions để giảm số lượng cần kiểm tra
                var activeSessions = await GetAllActiveSessionsAsync();
                
                foreach (var session in activeSessions)
                {
                    if (!string.IsNullOrEmpty(session.RefreshTokenHash) &&
                        BCrypt.Net.BCrypt.Verify(refreshToken, session.RefreshTokenHash))
                    {
                        return session;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                // Log lỗi để debug
                Console.WriteLine($"GetSessionByRefreshTokenAsync error: {ex.Message}");
                return null;
            }
        }

        public async Task<List<AuthSessionModel>> GetUserSessionsAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return new List<AuthSessionModel>();

            var queryConfig = new QueryOperationConfig
            {
                IndexName = "GSI1-UserSessions",
                KeyExpression = new Expression
                {
                    ExpressionStatement = "GSI1PK = :userSessionPK",
                    ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                    {
                        { ":userSessionPK", $"USER#{userId}#SESSION" }
                    }
                }
            };

            var search = _context.FromQueryAsync<AuthSessionModel>(queryConfig);
            return await search.GetRemainingAsync();
        }

        public async Task<List<AuthSessionModel>> GetAllActiveSessionsAsync()
        {
            var scanConfig = new ScanOperationConfig
            {
                FilterExpression = new Expression
                {
                    ExpressionStatement = "ExpiresAt > :now AND EntityType = :entityType",
                    ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                    {
                        { ":now", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                        { ":entityType", "SESSION" }
                    }
                }
            };

            var search = _context.FromScanAsync<AuthSessionModel>(scanConfig);
            return await search.GetRemainingAsync();
        }

        public async Task<bool> DeleteSessionAsync(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                return false;

            try
            {
                var pk = $"SESSION#{sessionId}";
                
                // First, find the session to get the SK
                var scanConfig = new ScanOperationConfig
                {
                    FilterExpression = new Expression
                    {
                        ExpressionStatement = "PK = :pk",
                        ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                        {
                            { ":pk", pk }
                        }
                    },
                    Limit = 1
                };

                var search = _context.FromScanAsync<AuthSessionModel>(scanConfig);
                var sessions = await search.GetRemainingAsync();
                var session = sessions.FirstOrDefault();

                if (session == null)
                    return false;

                await _context.DeleteAsync(session);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeleteSessionAsync error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteAllUserSessionsAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return false;

            try
            {
                var sessions = await GetUserSessionsAsync(userId);
                
                foreach (var session in sessions)
                {
                    await _context.DeleteAsync(session);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DeleteAllUserSessionsAsync error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateSessionLastLoginAsync(string sessionId, string lastLoginAt)
        {
            if (string.IsNullOrEmpty(sessionId))
                return false;

            try
            {
                // Find the session first
                var pk = $"SESSION#{sessionId}";
                var scanConfig = new ScanOperationConfig
                {
                    FilterExpression = new Expression
                    {
                        ExpressionStatement = "PK = :pk",
                        ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                        {
                            { ":pk", pk }
                        }
                    },
                    Limit = 1
                };

                var search = _context.FromScanAsync<AuthSessionModel>(scanConfig);
                var sessions = await search.GetRemainingAsync();
                var session = sessions.FirstOrDefault();

                if (session == null)
                    return false;

                // Update last login time
                session.LastLoginAt = lastLoginAt;
                await _context.SaveAsync(session);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateSessionLastLoginAsync error: {ex.Message}");
                return false;
            }
        }
    }
}