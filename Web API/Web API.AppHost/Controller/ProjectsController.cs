using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using BLL.Services;
using BLL.DTOs;
using DAL.Entities;
using DAL.Interfaces;

namespace Web_API.AppHost.Controllers
{
    [ApiController]
    [Route("api/projects")]
    public class ProjectsController : ControllerBase
    {
        private readonly ProjectService _projectService;
        private readonly ProjectMemberService _projectMemberService;
        private readonly UserService _userService;

        public ProjectsController(ProjectService projectService, ProjectMemberService projectMemberService, UserService userService)
        {
            _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            _projectMemberService = projectMemberService ?? throw new ArgumentNullException(nameof(projectMemberService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        }

        private async Task<bool> isAdminAsync()
        {
            var UserId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(UserId))
                return false;
            var user = await _userService.GetUserByIdAsync(UserId);
            return user?.IsAdmin == true;
        }


        // GET /api/projects?search=...
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProjectModel>>> GetProjects([FromQuery] string? search)
        {
            var projects = await _projectService.ListProjectsAsync(search);
            return Ok(projects);
        }

        [HttpGet("{projectId}")]
        public async Task<IActionResult> GetProject([FromRoute] string projectId)
        {
            if (string.IsNullOrEmpty(projectId))
                return BadRequest(new { message = "projectId required" });

            var project = await _projectService.GetProjectAsync(projectId);
            if (project == null) return NotFound();
            return Ok(project);
        }

        [HttpPost]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request)
        {
            if(!await isAdminAsync())
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Create project — ProjectId is generated server-side in service
            var created = await _projectService.CreateProjectAsync(request);

            return CreatedAtAction(
                nameof(GetProject),
                new { projectId = created.ProjectId },
                created
            );
        }

        // PUT /api/projects/{projectId}
        [HttpPut("{projectId}")]
        public async Task<IActionResult> UpdateProject(
            [FromRoute] string projectId,
            [FromBody] ProjectModel project)
        {
            if (!await isAdminAsync())
            {
                return Forbid();
            }

            if (string.IsNullOrEmpty(projectId) || project == null)
                return BadRequest(new { message = "projectId and body are required" });

            // Nếu body có ProjectId thì phải khớp với route
            if (!string.IsNullOrEmpty(project.ProjectId) && project.ProjectId != projectId)
                return BadRequest(new { message = "projectId mismatch" });

            // Ép body dùng đúng id từ route
            project.ProjectId = projectId;

            // Check tồn tại
            var existing = await _projectService.GetProjectAsync(projectId);
            if (existing == null)
                return NotFound();

            // Update
            var updated = await _projectService.UpdateProjectAsync(project);
            return Ok(updated);
        }

        // DELETE /api/projects/{projectId}
        [HttpDelete("{projectId}")]
        public async Task<ActionResult> DeleteProject([FromRoute] string projectId)
        {
            if (!await isAdminAsync())
            {
                return Forbid();
            }

            if (string.IsNullOrEmpty(projectId))
                return BadRequest(new { message = "projectId required" });

            var success = await _projectService.DeleteProjectAsync(projectId);
            if (!success)
                return NotFound();

            return NoContent();
        }
    }
}