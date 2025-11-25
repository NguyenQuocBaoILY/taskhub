using DAL.Entities;
using DAL.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services
{
    public class ProjectMemberService
    {
        private readonly IProjectMemberRepository _repo;
        private readonly IUserRepository _userRepo;

        // Server-side role -> permissions mapping
        private static readonly Dictionary<string, List<string>> RolePermissionMatrix = new()
        {
            ["PROJECT_OWNER"] = new List<string> { "project.*", "member.*", "task.*", "sprint.*", "epic.*", "comment.*", "file.*" },
            ["PROJECT_ADMIN"] = new List<string> { "project.view", "project.edit", "project.settings", "member.invite", "member.remove", "member.edit_role", "task.*", "sprint.*", "epic.*", "comment.*", "file.*" },
            ["PROJECT_MANAGER"] = new List<string> { "project.view", "member.invite", "task.*", "sprint.*", "epic.*", "comment.*", "file.*" },
            ["DEVELOPER"] = new List<string> { "project.view", "task.view", "task.create", "task.edit", "task.assign", "sprint.view", "epic.view", "comment.create", "comment.edit", "file.upload" },
            ["CONTRIBUTOR"] = new List<string> { "project.view", "task.view", "task.create", "sprint.view", "epic.view", "comment.create", "file.upload" },
            ["VIEWER"] = new List<string> { "project.view", "task.view", "sprint.view", "epic.view" }
        };

        public ProjectMemberService(IProjectMemberRepository repo, IUserRepository userRepo)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _userRepo = userRepo ?? throw new ArgumentNullException(nameof(userRepo));
        }

        // GET /api/users/{userId}/projects
        public async Task<IEnumerable<ProjectMemberModel>> GetUserProjectsAsync(string userId, int limit = 100)
        {
            if (string.IsNullOrEmpty(userId)) return Enumerable.Empty<ProjectMemberModel>();

            var items = (await _repo.GetUserProjectsAsync(userId, limit))?.ToList() ?? new List<ProjectMemberModel>();

            var user = await _userRepo.GetUserByIdAsync(userId);
            var username = user?.Username;

            var sanitized = items.Select(i =>
            {
                var copy = SanitizeForResponse(i);
                copy.Username = username;
                return copy;
            });

            return sanitized;
        }

        // GET /api/projects/{projectId}/members/{userId}
        public async Task<ProjectMemberModel> GetProjectMemberAsync(string projectId, string userId)
        {
            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(userId)) return null;

            var item = await _repo.GetProjectMemberAsync(projectId, userId);
            if (item == null) return null;

            var user = await _userRepo.GetUserByIdAsync(userId);
            var copy = SanitizeForResponse(item);
            copy.Username = user?.Username;
            return copy;
        }

        // GET /api/projects/{projectId}/members
        public async Task<IEnumerable<ProjectMemberModel>> GetProjectMembersAsync(string projectId, int limit = 100)
        {
            if (string.IsNullOrEmpty(projectId)) return Enumerable.Empty<ProjectMemberModel>();

            var items = (await _repo.GetProjectMembersAsync(projectId, limit))?.ToList() ?? new List<ProjectMemberModel>();
            if (!items.Any()) return Enumerable.Empty<ProjectMemberModel>();

            // fetch usernames in parallel and build map
            var userIds = items.Select(i => i.UserId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            var userTasks = userIds.Select(id => _userRepo.GetUserByIdAsync(id)).ToArray();
            var users = await Task.WhenAll(userTasks);
            var userMap = users.Where(u => u != null).ToDictionary(u => u.UserId, u => u.Username);

            var sanitized = items.Select(i =>
            {
                var copy = SanitizeForResponse(i);
                copy.Username = userMap.ContainsKey(i.UserId) ? userMap[i.UserId] : null;
                return copy;
            });

            return sanitized;
        }

        // POST /api/projects/{projectId}/members
        // request: ProjectMemberModel with UserId & Role populated
        public async Task<ProjectMemberModel> AddMemberAsync(string projectId, ProjectMemberModel request, string addedBy = null)
        {
            if (string.IsNullOrEmpty(projectId)) throw new ArgumentException("projectId is required", nameof(projectId));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.Role))
                throw new ArgumentException("userId and role are required");

            var now = DateTime.UtcNow.ToString("o");

            var member = new ProjectMemberModel
            {
                ProjectId = projectId,
                UserId = request.UserId,
                Role = request.Role,
                MemberStatus = request.MemberStatus ?? "ACTIVE",
                JoinedAt = now,
                AddedBy = addedBy,
                CreatedAt = now,
                UpdatedAt = now,
                Permissions = request.Permissions ?? ExpandPermissions(request.Role)
            };

            member = await _repo.AddOrUpdateMemberAsync(member);

            var user = await _userRepo.GetUserByIdAsync(member.UserId);
            var copy = SanitizeForResponse(member);
            copy.Username = user?.Username;
            return copy;
        }

        // PATCH /api/projects/{projectId}/members/{userId}
        public async Task<bool> PatchMemberAsync(string projectId, string userId, string role = null, string memberStatus = null)
        {
            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(userId)) return false;

            var success = await _repo.PatchMemberAsync(projectId, userId, role, memberStatus);
            if (!success) return false;

            if (!string.IsNullOrEmpty(role))
            {
                // update permissions by reloading and saving full record through AddOrUpdate to ensure Permissions stored
                var current = await _repo.GetProjectMemberAsync(projectId, userId);
                if (current != null)
                {
                    current.Permissions = ExpandPermissions(role);
                    await _repo.AddOrUpdateMemberAsync(current);
                }
            }

            return true;
        }

        // DELETE /api/projects/{projectId}/members/{userId}
        public async Task<bool> DeleteMemberAsync(string projectId, string userId)
        {
            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(userId)) return false;
            return await _repo.DeleteMemberAsync(projectId, userId);
        }

        // Helper: return shallow copy with internal DB fields nulled
        private ProjectMemberModel SanitizeForResponse(ProjectMemberModel src)
        {
            if (src == null) return null;
            return new ProjectMemberModel
            {
                ProjectId = src.ProjectId,
                UserId = src.UserId,
                Role = src.Role,
                Permissions = src.Permissions ?? ExpandPermissions(src.Role),
                MemberStatus = src.MemberStatus,
                JoinedAt = src.JoinedAt,
                AddedBy = src.AddedBy,
                RemovedBy = src.RemovedBy,
                RemovedAt = src.RemovedAt,
                LastAccessedAt = src.LastAccessedAt,
                CreatedAt = src.CreatedAt,
                UpdatedAt = src.UpdatedAt,
                // internal fields left null so not exposed
                PK = null,
                SK = null,
                EntityType = null,
                GSI7PK = null,
                GSI7SK = null,
                GSI8PK = null,
                GSI8SK = null
            };
        }

        private List<string> ExpandPermissions(string role)
        {
            if (string.IsNullOrEmpty(role)) return new List<string>();
            if (RolePermissionMatrix.TryGetValue(role, out var perms))
                return new List<string>(perms);
            // default to empty
            return new List<string>();
        }
    }
}