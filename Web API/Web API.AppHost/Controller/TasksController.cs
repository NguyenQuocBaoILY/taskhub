using BLL.Services;
using DAL.Entities;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebAPI.APPHost.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TaskController : ControllerBase
    {
        private readonly TaskService _taskService;
        private readonly ProjectMemberService _projectMemberService;

        public TaskController(TaskService taskService, ProjectMemberService projectMemberService)
        {
            _taskService = taskService;
            _projectMemberService = projectMemberService;
        }

        // GET api/task?projectId=123&userId=...&status=TODO
        [HttpGet]
        public async Task<ActionResult<List<TaskModel>>> ListTasks([FromQuery] string projectId, [FromQuery] string userId, [FromQuery] string status = null)
        {
            if (string.IsNullOrEmpty(projectId))
                return BadRequest("ProjectId is required");

            if (string.IsNullOrEmpty(userId))
                return BadRequest("userId is required");

            // kiểm tra user có là member của project không
            var member = await _projectMemberService.GetProjectMemberAsync(projectId, userId);
            if (member == null)
                return Forbid();

            var tasks = await _taskService.ListTasksAsync(projectId, status);
            return Ok(tasks);
        }

        // POST api/task?projectId=...&userId=...
        [HttpPost]
        public async Task<ActionResult<TaskModel>> CreateTask([FromQuery] string projectId, [FromQuery] string userId, [FromBody] TaskModel task)
        {
            if (string.IsNullOrEmpty(projectId))
                return BadRequest("projectId is required");

            if (string.IsNullOrEmpty(userId))
                return BadRequest("userId is required");

            if (task == null || string.IsNullOrEmpty(task.Title))
                return BadRequest("Title is required");

            // Ensure task.ProjectId matches query projectId (or set it)
            if (string.IsNullOrEmpty(task.ProjectId))
            {
                task.ProjectId = projectId;
            }
            else if (task.ProjectId != projectId)
            {
                return BadRequest("ProjectId in body must match projectId query parameter");
            }

            // check membership
            var member = await _projectMemberService.GetProjectMemberAsync(projectId, userId);
            if (member == null)
                return Forbid();

            var createdTask = await _taskService.CreateTaskAsync(task);
            return Ok(createdTask);
        }

        // PUT api/task/{taskId}?userId=...
        [HttpPut("{taskId}")]
        public async Task<ActionResult<TaskModel>> UpdateTask(string taskId, [FromQuery] string userId, [FromBody] TaskModel task)
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest("userId is required");

            if (task == null || taskId != task.TaskId)
                return BadRequest("TaskId mismatch");

            if (string.IsNullOrEmpty(task.ProjectId))
                return BadRequest("ProjectId is required in request body");

            // check membership
            var member = await _projectMemberService.GetProjectMemberAsync(task.ProjectId, userId);
            if (member == null)
                return Forbid();

            var updatedTask = await _taskService.UpdateTaskAsync(task);
            return Ok(updatedTask);
        }

        // DELETE api/task/{taskId}?projectId=123&userId=...
        [HttpDelete("{taskId}")]
        public async Task<ActionResult> DeleteTask(string taskId, [FromQuery] string projectId, [FromQuery] string userId)
        {
            if (string.IsNullOrEmpty(projectId))
                return BadRequest("ProjectId is required");

            if (string.IsNullOrEmpty(userId))
                return BadRequest("userId is required");

            // check membership
            var member = await _projectMemberService.GetProjectMemberAsync(projectId, userId);
            if (member == null)
                return Forbid();

            var result = await _taskService.DeleteTaskAsync(taskId, projectId);
            if (!result) return NotFound();
            return NoContent();
        }
    }
}
