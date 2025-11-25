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
    public class ProjectMemberRepository : IProjectMemberRepository
    {
        private readonly IDynamoDBContext _context;

        public ProjectMemberRepository(IAmazonDynamoDB client)
        {
            _context = new DynamoDBContext(client);
        }

        public async Task<ProjectMemberModel> GetProjectMemberAsync(string projectId, string userId)
        {
            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(userId))
                return null;

            var pk = $"PROJECT#{projectId}";
            var sk = $"MEMBER#{userId}";
            return await _context.LoadAsync<ProjectMemberModel>(pk, sk);
        }

        public async Task<IEnumerable<ProjectMemberModel>> GetProjectMembersAsync(string projectId, int limit = 100)
        {
            if (string.IsNullOrEmpty(projectId))
                return Enumerable.Empty<ProjectMemberModel>();

            var pk = $"PROJECT#{projectId}";

            var queryConfig = new QueryOperationConfig
            {
                KeyExpression = new Expression
                {
                    ExpressionStatement = "PK = :pk and begins_with(SK, :skPrefix)",
                    ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                    {
                        { ":pk", pk },
                        { ":skPrefix", "MEMBER#" }
                    }
                },
                Limit = limit
            };

            var search = _context.FromQueryAsync<ProjectMemberModel>(queryConfig);
            var results = await search.GetRemainingAsync();
            return results;
        }

        public async Task<IEnumerable<ProjectMemberModel>> GetUserProjectsAsync(string userId, int limit = 100)
        {
            if (string.IsNullOrEmpty(userId))
                return Enumerable.Empty<ProjectMemberModel>();

            var queryConfig = new QueryOperationConfig
            {
                IndexName = "GSI7-UserProjects",
                KeyExpression = new Expression
                {
                    ExpressionStatement = "GSI7PK = :gsi7pk",
                    ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                    {
                        { ":gsi7pk", $"USER#{userId}#PROJECT" }
                    }
                },
                Limit = limit
            };

            var search = _context.FromQueryAsync<ProjectMemberModel>(queryConfig);
            var results = await search.GetRemainingAsync();
            return results;
        }

        public async Task<ProjectMemberModel> AddOrUpdateMemberAsync(ProjectMemberModel member)
        {
            if (member == null) throw new ArgumentNullException(nameof(member));
            if (string.IsNullOrEmpty(member.ProjectId) || string.IsNullOrEmpty(member.UserId))
                throw new ArgumentException("projectId and userId required");

            var now = DateTime.UtcNow.ToString("o");

            member.PK = $"PROJECT#{member.ProjectId}";
            member.SK = $"MEMBER#{member.UserId}";
            member.EntityType = "PROJECT_MEMBER";

            member.GSI7PK = $"USER#{member.UserId}#PROJECT";
            member.GSI7SK = $"PROJECT#{member.ProjectId}#{member.Role}#{member.JoinedAt ?? now}";

            member.GSI8PK = $"PROJECT#{member.ProjectId}#MEMBER";
            member.GSI8SK = $"{member.Role}#{member.MemberStatus}#{member.JoinedAt ?? now}";

            member.UpdatedAt = now;
            if (string.IsNullOrEmpty(member.CreatedAt))
                member.CreatedAt = now;
            if (string.IsNullOrEmpty(member.JoinedAt))
                member.JoinedAt = now;
            if (member.Permissions == null)
                member.Permissions = new List<string>();

            await _context.SaveAsync(member);
            return member;
        }

        public async Task<bool> PatchMemberAsync(string projectId, string userId, string role = null, string memberStatus = null)
        {
            var existing = await GetProjectMemberAsync(projectId, userId);
            if (existing == null) return false;

            var now = DateTime.UtcNow.ToString("o");

            if (!string.IsNullOrEmpty(role))
            {
                existing.Role = role;
            }

            if (!string.IsNullOrEmpty(memberStatus))
            {
                existing.MemberStatus = memberStatus;
            }

            // Update GSIs to reflect changes
            existing.GSI7SK = $"PROJECT#{existing.ProjectId}#{existing.Role}#{existing.JoinedAt}";
            existing.GSI8SK = $"{existing.Role}#{existing.MemberStatus}#{existing.JoinedAt}";

            existing.UpdatedAt = now;
            await _context.SaveAsync(existing);
            return true;
        }

        public async Task<bool> DeleteMemberAsync(string projectId, string userId)
        {
            var existing = await GetProjectMemberAsync(projectId, userId);
            if (existing == null) return false;

            await _context.DeleteAsync(existing);
            return true;
        }
    }
}