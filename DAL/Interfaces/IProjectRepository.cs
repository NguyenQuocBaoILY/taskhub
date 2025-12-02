using DAL.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface IProjectRepository
    {
        Task<List<ProjectModel>> ListProjectsAsync(string? search = null);

        Task<ProjectModel?> GetProjectAsync(string projectId);

        Task<ProjectModel> CreateProjectAsync(ProjectModel project);

        Task<ProjectModel> UpdateProjectAsync(ProjectModel project);

        Task<bool> DeleteProjectAsync(string projectId);

        // New: find project when only projectId is known (uses Scan; not optimal for large tables)
        Task<ProjectModel?> GetProjectByIdAsync(string projectId);
    }
}