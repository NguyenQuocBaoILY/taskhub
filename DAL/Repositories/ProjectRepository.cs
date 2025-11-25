using DAL.Entities;
using DAL.Interfaces;
using Amazon.DynamoDBv2.DataModel;

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
    }
}
