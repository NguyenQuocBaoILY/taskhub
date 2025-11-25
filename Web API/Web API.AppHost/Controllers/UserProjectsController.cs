using BLL.Services;
using DAL.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace Web_API.AppHost.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly ProjectMemberService _projectMemberService;
        private readonly IUserRepository _userRepository;

        public UsersController(ProjectMemberService projectMemberService, IUserRepository userRepository)
        {
            _projectMemberService = projectMemberService;
            _userRepository = userRepository;
        }

        // GET /api/users/{userId}/projects
        [HttpGet("{userId}/projects")]
        public async Task<IActionResult> GetUserProjects([FromRoute] string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest(new { message = "userId is required" });

            var projects = await _projectMemberService.GetUserProjectsAsync(userId);
            return Ok(projects);
        }

        // GET /api/users?query={q}&limit={n}
        // Returns list of users for FE selection (userId, username, name, avatar).
        [HttpGet]
        public async Task<IActionResult> SearchUsers([FromQuery] string query = null, [FromQuery] int limit = 50)
        {
            if (limit <= 0) limit = 50;
            if (limit > 200) limit = 200; // safety cap

            var users = await _userRepository.SearchUsersAsync(query, limit);

            var result = users?.Select(u => new
            {
                userId = u.UserId,
                username = u.Username,
                name = u.Name,
                avatar = u.Avatar,
                email = u.Email
            }) ?? Enumerable.Empty<object>();

            return Ok(result);
        }
    }
}