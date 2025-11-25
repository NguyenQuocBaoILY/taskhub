using DAL.Interfaces;
using Microsoft.AspNetCore.Mvc;
using DAL.Entities;
using BLL.Services;

namespace API.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectsController : ControllerBase
    {
        private readonly ProjectService _projectService;

        public ProjectsController(ProjectService projectService)
        {
            _projectService = projectService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProjectModel>>> GetProjects([FromQuery] string orgId, [FromQuery] string? status)
        {
            var projects = await _projectService.GetProjectAsync(orgId, status);
            return Ok(projects);
        }

        [HttpGet("{orgId}/{projectId}")]
        public async Task<ActionResult<ProjectModel>> GetProject(string orgId, string projectId)
        {
            var project = await _projectService.GetProjectAsync(orgId, projectId);
            if (project == null)
                return NotFound();
            return Ok(project);
        }

        [HttpPost]
        public async Task<ActionResult<ProjectModel>> CreateProject([FromBody] ProjectModel project)
        {
            var createdProject = await _projectService.CreateProjectAsync(project);
            return Ok(createdProject);
        }

        [HttpPut("{orgId}/{projectId}")]
        public async Task<ActionResult<ProjectModel>> UpdateProject(string orgId, string projectId, [FromBody] ProjectModel project)
        {
            if (project == null || orgId != project.OrgId || projectId != project.ProjectId)
                return BadRequest("OrgId or ProjectId mismatch");
            var updatedProject = await _projectService.UpdateProjectAsync(project);
            return Ok(updatedProject);
        }

        [HttpDelete("{orgId}/{projectId}")]
        public async Task<ActionResult> DeleteProject(string orgId, string projectId)
        {
            var result = await _projectService.DeleteProjectAsync(orgId, projectId);
            if (!result) return NotFound();
            return NoContent();
        }
    }
}
