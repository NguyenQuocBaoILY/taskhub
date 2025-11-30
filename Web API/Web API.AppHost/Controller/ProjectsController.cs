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

        // GET /api/projects?orgId=...&status=...
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

        // }
        [HttpPost]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // 1. Resolve ManagerUserId
            string managerUserId = request.ManagerUserId;
            if (string.IsNullOrEmpty(managerUserId) && !string.IsNullOrEmpty(request.ManagerUsername))
            {
                var user = await _userService.GetUserByUsernameAsync(request.ManagerUsername);
                if (user == null)
                    return BadRequest(new { message = "Manager username not found" });

                managerUserId = user.UserId;
            }

            if (string.IsNullOrEmpty(managerUserId))
                return BadRequest(new { message = "ManagerUserId or ManagerUsername is required" });

            // Gán lại vào request để service dùng
            request.ManagerUserId = managerUserId;

            // 2. Gọi service tạo project (service sẽ map DTO -> ProjectModel)
            var created = await _projectService.CreateProjectAsync(request);

            // 3. Thêm manager vào ProjectMember
            var managerMember = new ProjectMemberModel
            {
                ProjectId = created.ProjectId,
                UserId = managerUserId,
                Role = string.IsNullOrEmpty(request.ManagerRole)
                        ? "PROJECT_OWNER"
                        : request.ManagerRole
            };

            await _projectMemberService.AddMemberAsync(
                created.ProjectId,
                managerMember,
                addedBy: managerUserId
            );

            // 4. Trả về 201
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
            if (string.IsNullOrEmpty(projectId))
                return BadRequest(new { message = "projectId required" });

            var success = await _projectService.DeleteProjectAsync(projectId);
            if (!success)
                return NotFound();

            return NoContent();
        }
    }
}