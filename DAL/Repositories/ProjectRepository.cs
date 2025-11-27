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

        private static string BuildPk(string orgId) => $"ORG#{orgId}";
        private static string BuildSk(string projectId) => $"PROJECT#{projectId}";

        public async Task<ProjectModel> CreateProjectAsync(ProjectModel project)
        {
            project.PK = BuildPk(project.OrgId);
            project.SK = BuildSk(project.ProjectId);

            await _context.SaveAsync(project);
            return project;
        }

        public async Task<bool> DeleteProjectAsync(string orgId, string projectId)
        {
            var pk = BuildPk(orgId);
            var sk = BuildSk(projectId);

            await _context.DeleteAsync<ProjectModel>(pk, sk);
            return true;
        }

        public Task<ProjectModel?> GetProjectAsync(string orgId, string projectId)
        {
            var pk = BuildPk(orgId);
            var sk = BuildSk(projectId);

            return _context.LoadAsync<ProjectModel>(pk, sk);
        }

        public async Task<List<ProjectModel>> ListProjectsAsync(string orgId, string? status = null)
        {
            var pk = BuildPk(orgId);
            var query = _context.QueryAsync<ProjectModel>(pk);
            var item = await query.GetRemainingAsync();

            if (!string.IsNullOrEmpty(status))
            {
                item = item.Where(p => p.Status == status).ToList();
            }
            return item;
        }

        public async Task<ProjectModel> UpdateProjectAsync(ProjectModel project)
        {
            project.PK = BuildPk(project.OrgId);
            project.SK = BuildSk(project.ProjectId);

            await _context.SaveAsync(project);
            return project;
        }

        // New: scan to find project by projectId only (inefficient for large tables)
        public async Task<ProjectModel?> GetProjectByIdAsync(string projectId)
        {
            if (string.IsNullOrEmpty(projectId)) return null;

            var sk = BuildSk(projectId);

            var scanConfig = new ScanOperationConfig
            {
                FilterExpression = new Expression
                {
                    ExpressionStatement = "SK = :sk",
                    ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>
                    {
                        { ":sk", sk }
                    }
                },
                Limit = 1
            };

            var search = _context.FromScanAsync<ProjectModel>(scanConfig);
            var results = await search.GetRemainingAsync();
            return results.FirstOrDefault();
        }
    }
}
