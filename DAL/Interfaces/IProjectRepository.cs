using DAL.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IProjectRepository
    {
        Task<List<ProjectModel>> ListProjectsAsync(string orgId, string? status = null);

        Task<ProjectModel?> GetProjectAsync(string orgId, string projectId);

        Task<ProjectModel> CreateProjectAsync(ProjectModel project);

        Task<ProjectModel> UpdateProjectAsync(ProjectModel project);

        Task<bool> DeleteProjectAsync(string orgId, string projectId);
    }
}