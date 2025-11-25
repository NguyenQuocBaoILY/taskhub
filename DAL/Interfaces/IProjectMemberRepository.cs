using DAL.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IProjectMemberRepository
    {
        Task<ProjectMemberModel> GetProjectMemberAsync(string projectId, string userId);
        Task<IEnumerable<ProjectMemberModel>> GetProjectMembersAsync(string projectId, int limit = 100);
        Task<IEnumerable<ProjectMemberModel>> GetUserProjectsAsync(string userId, int limit = 100);
        Task<ProjectMemberModel> AddOrUpdateMemberAsync(ProjectMemberModel member);
        Task<bool> PatchMemberAsync(string projectId, string userId, string role = null, string memberStatus = null);
        Task<bool> DeleteMemberAsync(string projectId, string userId);
    }
}