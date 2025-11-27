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
        private readonly IUserRepository _userRepository;

        public ProjectsController(ProjectService projectService, ProjectMemberService projectMemberService, IUserRepository userRepository)
        {
            _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
            _projectMemberService = projectMemberService ?? throw new ArgumentNullException(nameof(projectMemberService));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        }

        // GET /api/projects?orgId=...&status=...
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProjectModel>>> GetProjects([FromQuery] string orgId, [FromQuery] string? status)
        {
            if (string.IsNullOrEmpty(orgId))
                return BadRequest(new { message = "orgId is required" });

            var projects = await _projectService.ListProjectsAsync(orgId, status);
            return Ok(projects);
        }

        // Existing: GET /api/projects/{orgId}/{projectId}
        [HttpGet("{orgId}/{projectId}")]
        public async Task<IActionResult> GetProject([FromRoute] string orgId, [FromRoute] string projectId)
        {
            if (string.IsNullOrEmpty(orgId) || string.IsNullOrEmpty(projectId))
                return BadRequest(new { message = "orgId and projectId required" });

            var project = await _projectService.GetProjectAsync(orgId, projectId);
            if (project == null) return NotFound();
            return Ok(project);
        }

        // New: GET by projectId only (uses scan)
        // GET /api/projects/by-id/{projectId}
        [HttpGet("by-id/{projectId}")]
        public async Task<IActionResult> GetProjectById([FromRoute] string projectId)
        {
            if (string.IsNullOrEmpty(projectId))
                return BadRequest(new { message = "projectId required" });

            var project = await _projectService.GetProjectByIdAsync(projectId);
            if (project == null) return NotFound();
            return Ok(project);
        }

        [HttpPost]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Resolve manager userId if FE provided username
            string managerUserId = request.ManagerUserId;
            if (string.IsNullOrEmpty(managerUserId) && !string.IsNullOrEmpty(request.ManagerUsername))
            {
                var user = await _userRepository.GetUserByUsernameAsync(request.ManagerUsername);
                if (user == null) return BadRequest(new { message = "Manager username not found" });
                managerUserId = user.UserId;
            }

            if (string.IsNullOrEmpty(managerUserId))
                return BadRequest(new { message = "ManagerUserId or ManagerUsername is required" });

            var projectId = string.IsNullOrEmpty(request.ProjectId) ? Guid.NewGuid().ToString() : request.ProjectId;
            var now = DateTime.UtcNow.ToString("o");

            var project = new ProjectModel
            {
                ProjectId = projectId,
                OrgId = request.OrgId,
                Name = request.Name,
                Key = request.Key,
                Description = request.Description,
                Visibility = request.Visibility ?? "PRIVATE",
                Status = request.Status ?? "ACTIVE",
                Priority = request.Priority,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                OwnerUserId = $"USER#{managerUserId}",
                CreatedAt = now,
                UpdatedAt = now,
                MemberCount = (request.Members?.Count ?? 0) + 1, // +1 for manager
                GSI4PK = $"USER#{managerUserId}",
                GSI4SK = $"PROJECT#{projectId}"
            };

            // persist project
            var created = await _projectService.CreateProjectAsync(project);

            // add manager as member
            var managerMember = new ProjectMemberModel
            {
                ProjectId = created.ProjectId,
                UserId = managerUserId,
                Role = string.IsNullOrEmpty(request.ManagerRole) ? "PROJECT_OWNER" : request.ManagerRole
            };
            await _projectMemberService.AddMemberAsync(created.ProjectId, managerMember, addedBy: managerUserId);

            // add other members
            if (request.Members != null && request.Members.Any())
            {
                foreach (var m in request.Members)
                {
                    if (string.IsNullOrEmpty(m.UserId) || string.IsNullOrEmpty(m.Role)) continue;
                    var memberModel = new ProjectMemberModel
                    {
                        ProjectId = created.ProjectId,
                        UserId = m.UserId,
                        Role = m.Role
                    };
                    await _projectMemberService.AddMemberAsync(created.ProjectId, memberModel, addedBy: managerUserId);
                }
            }

            // return created project
            return CreatedAtAction(nameof(GetProject), new { orgId = created.OrgId, projectId = created.ProjectId }, created);
        }

        // Existing: update by orgId + projectId
        [HttpPut("{orgId}/{projectId}")]
        public async Task<ActionResult<ProjectModel>> UpdateProject(string orgId, string projectId, [FromBody] ProjectModel project)
        {
            if (project == null || orgId != project.OrgId || projectId != project.ProjectId)
                return BadRequest("OrgId or ProjectId mismatch");
            var updatedProject = await _projectService.UpdateProjectAsync(project);
            return Ok(updatedProject);
        }

        // New: update by projectId only (resolve orgId first)
        // PUT /api/projects/by-id/{projectId}
        [HttpPut("by-id/{projectId}")]
        public async Task<IActionResult> UpdateProjectById([FromRoute] string projectId, [FromBody] ProjectModel project)
        {
            if (string.IsNullOrEmpty(projectId) || project == null || project.ProjectId != projectId)
                return BadRequest("projectId mismatch or missing body");

            var existing = await _projectService.GetProjectByIdAsync(projectId);
            if (existing == null) return NotFound();

            // ensure OrgId preserved if not provided
            if (string.IsNullOrEmpty(project.OrgId))
                project.OrgId = existing.OrgId;

            var updated = await _projectService.UpdateProjectAsync(project);
            return Ok(updated);
        }

        [HttpDelete("{orgId}/{projectId}")]
        public async Task<ActionResult> DeleteProject(string orgId, string projectId)
        {
            var result = await _projectService.DeleteProjectAsync(orgId, projectId);
            if (!result) return NotFound();
            return NoContent();
        }

        // New: delete by projectId only (resolve orgId first)
        // DELETE /api/projects/by-id/{projectId}
        [HttpDelete("by-id/{projectId}")]
        public async Task<ActionResult> DeleteProjectById([FromRoute] string projectId)
        {
            if (string.IsNullOrEmpty(projectId))
                return BadRequest(new { message = "projectId required" });

            var existing = await _projectService.GetProjectByIdAsync(projectId);
            if (existing == null) return NotFound();

            var success = await _projectService.DeleteProjectAsync(existing.OrgId, projectId);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}