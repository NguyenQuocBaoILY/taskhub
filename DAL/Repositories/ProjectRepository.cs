using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using DAL.Entities;
using DAL.Interfaces;

namespace DAL.Repositories
{
    public class ProjectRepository : IProjectRepository
    {
        private readonly IDynamoDBContext _context;

        public ProjectRepository(IDynamoDBContext context)
        {
            _context = context;
        }

        private static string BuildPk() => $"PROJECT";
        private static string BuildSk(string projectId) => $"PROJECT#{projectId}";

        public async Task<ProjectModel> CreateProjectAsync(ProjectModel project)
        {
            project.PK = BuildPk();
            project.SK = BuildSk(project.ProjectId);

            await _context.SaveAsync(project);
            return project;
        }

        public async Task<bool> DeleteProjectAsync(string projectId)
        {
            var pk = BuildPk();
            var sk = BuildSk(projectId);

            await _context.DeleteAsync<ProjectModel>(pk, sk);
            return true;
        }

        public Task<ProjectModel> GetProjectAsync(string projectId)
        {
            var pk = BuildPk();
            var sk = BuildSk(projectId);

            return _context.LoadAsync<ProjectModel>(pk, sk);
        }

        public async Task<List<ProjectModel>> ListProjectsAsync(string? search = null)
        {
            var pk = BuildPk();
            var query = _context.QueryAsync<ProjectModel>(pk);
            var item = await query.GetRemainingAsync();

            if (!string.IsNullOrEmpty(search))
            {
                item = item.Where(p => p.Name != null && p.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            return item;
        }

        public async Task<ProjectModel> UpdateProjectAsync(ProjectModel project)
        {
            project.PK = BuildPk();
            project.SK = BuildSk(project.ProjectId);

            await _context.SaveAsync(project);
            return project;
        }

        // New: scan to find project by projectId only (inefficient for large tables)
        public async Task<ProjectModel?> GetProjectByIdAsync(string projectId)
        {
            if (string.IsNullOrEmpty(projectId))
                return null;
            var sk = BuildSk(projectId);
            var pk = BuildPk();

            return await _context.LoadAsync<ProjectModel>(pk, sk);

        }
    }
}
