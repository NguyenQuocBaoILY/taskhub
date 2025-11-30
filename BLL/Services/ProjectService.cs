using BLL.DTOs;
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
        
        public async Task<List<ProjectModel>> ListProjectsAsync(string? search = null)
        {
            return await _projectRepository.ListProjectsAsync(search);
        }

        public async Task<ProjectModel> CreateProjectAsync(CreateProjectRequest request)
        {
            var projectId = string.IsNullOrEmpty(request.ProjectId) ? Guid.NewGuid().ToString() : request.ProjectId;
            
            var project = new ProjectModel
            {
                ProjectId = projectId,
                Name = request.Name,
                Description = request.Description,
                StartDate = request.StartDate,
                EndDate = request.EndDate,

                CreatedAt = DateTime.UtcNow.ToString("o"),
                UpdatedAt = DateTime.UtcNow.ToString("o")
            };

            return await _projectRepository.CreateProjectAsync(project);
        }

        public async Task<ProjectModel> UpdateProjectAsync(ProjectModel project)
        {
            return await _projectRepository.UpdateProjectAsync(project);
        }

        public async Task<bool> DeleteProjectAsync(string projectId)
        {
            return await _projectRepository.DeleteProjectAsync(projectId);
        }

        public async Task<ProjectModel?> GetProjectAsync(string projectId)
        {
            return await _projectRepository.GetProjectAsync(projectId);
        }

        // New: find project by projectId alone
        public async Task<ProjectModel?> GetProjectByIdAsync(string projectId)
        {
            return await _projectRepository.GetProjectByIdAsync(projectId);
        }
    }
}
