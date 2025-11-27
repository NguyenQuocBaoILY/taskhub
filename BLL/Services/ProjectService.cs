using DAL.Entities;
using DAL.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services
{
    public class ProjectService
    {
        private readonly IProjectRepository _projectRepository;
        public ProjectService(IProjectRepository projectRepository)
        {
            _projectRepository = projectRepository;
        }
        
        public async Task<List<ProjectModel>> ListProjectsAsync(string orgId, string? status = null)
        {
            return await _projectRepository.ListProjectsAsync(orgId, status);
        }

        public async Task<ProjectModel> CreateProjectAsync(ProjectModel project)
        {
            return await _projectRepository.CreateProjectAsync(project);
        }

        public async Task<ProjectModel> UpdateProjectAsync(ProjectModel project)
        {
            return await _projectRepository.UpdateProjectAsync(project);
        }

        public async Task<bool> DeleteProjectAsync(string orgId, string projectId)
        {
            return await _projectRepository.DeleteProjectAsync(orgId, projectId);
        }

        public async Task<ProjectModel?> GetProjectAsync(string orgId, string projectId)
        {
            return await _projectRepository.GetProjectAsync(orgId, projectId);
        }

        // New: find project by projectId alone
        public async Task<ProjectModel?> GetProjectByIdAsync(string projectId)
        {
            return await _projectRepository.GetProjectByIdAsync(projectId);
        }
    }
}
