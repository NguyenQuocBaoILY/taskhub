using DAL.Entities;
using BLL.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Web_API.AppHost.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}/members")]
    public class ProjectMembersController : ControllerBase
    {
        private readonly ProjectMemberService _projectMemberService;

        public ProjectMembersController(ProjectMemberService projectMemberService)
        {
            _projectMemberService = projectMemberService;
        }

        // GET /api/projects/{projectId}/members/{userId}
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetProjectMember([FromRoute] string projectId, [FromRoute] string userId)
        {
            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(userId))
                return BadRequest(new { message = "projectId and userId are required" });

            var member = await _projectMemberService.GetProjectMemberAsync(projectId, userId);
            if (member == null)
                return NotFound(new { message = "Member not found" });

            return Ok(member);
        }

        // GET /api/projects/{projectId}/members
        [HttpGet]
        public async Task<IActionResult> GetProjectMembers([FromRoute] string projectId)
        {
            if (string.IsNullOrEmpty(projectId))
                return BadRequest(new { message = "projectId is required" });

            var members = await _projectMemberService.GetProjectMembersAsync(projectId);
            return Ok(members);
        }

        // POST /api/projects/{projectId}/members
        // Body: ProjectMemberModel with UserId, Role
        [HttpPost]
        public async Task<IActionResult> AddProjectMember([FromRoute] string projectId, [FromBody] ProjectMemberModel request)
        {
            if (request == null)
                return BadRequest(new { message = "request body required" });

            if (string.IsNullOrEmpty(projectId))
                return BadRequest(new { message = "projectId is required" });

            if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.Role))
                return BadRequest(new { message = "userId and role are required" });

            string addedBy = User?.Identity?.IsAuthenticated == true ? User.FindFirst("userId")?.Value : null;

            var result = await _projectMemberService.AddMemberAsync(projectId, request, addedBy);
            return Ok(result);
        }

        // PATCH /api/projects/{projectId}/members/{userId}
        // Body: ProjectMemberModel with Role and/or MemberStatus
        [HttpPatch("{userId}")]
        public async Task<IActionResult> PatchProjectMember([FromRoute] string projectId, [FromRoute] string userId, [FromBody] ProjectMemberModel request)
        {
            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(userId))
                return BadRequest(new { message = "projectId and userId are required" });

            if (request == null)
                return BadRequest(new { message = "request body required" });

            var success = await _projectMemberService.PatchMemberAsync(projectId, userId, request.Role, request.MemberStatus);
            if (!success)
                return NotFound(new { message = "Member not found" });

            return Ok(new { message = "Updated" });
        }

        // DELETE /api/projects/{projectId}/members/{userId}
        [HttpDelete("{userId}")]
        public async Task<IActionResult> DeleteProjectMember([FromRoute] string projectId, [FromRoute] string userId)
        {
            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(userId))
                return BadRequest(new { message = "projectId and userId are required" });

            var success = await _projectMemberService.DeleteMemberAsync(projectId, userId);
            if (!success)
                return NotFound(new { message = "Member not found" });

            return Ok(new { message = "Deleted" });
        }
    }
}