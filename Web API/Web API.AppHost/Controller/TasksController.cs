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

        public TaskController(TaskService taskService)
        {
            _taskService = taskService;
        }

        // GET api/task?projectId=123&status=TODO
        [HttpGet]
        public async Task<ActionResult<List<TaskModel>>> ListTasks([FromQuery] string projectId, [FromQuery] string status = null)
        {
            if (string.IsNullOrEmpty(projectId))
                return BadRequest("ProjectId is required");

            var tasks = await _taskService.ListTasksAsync(projectId, status);
            return Ok(tasks);
        }

        // POST api/task
        [HttpPost]
        public async Task<ActionResult<TaskModel>> CreateTask([FromBody] TaskModel task)
        {
            if (task == null || string.IsNullOrEmpty(task.ProjectId) || string.IsNullOrEmpty(task.Title))
                return BadRequest("ProjectId and Title are required");

            var createdTask = await _taskService.CreateTaskAsync(task);
            return Ok(createdTask);
        }

        //// PUT api/task/{taskId}
        [HttpPut("{taskId}")]
        public async Task<ActionResult<TaskModel>> UpdateTask(string taskId, [FromBody] TaskModel task)
        {
            if (task == null || taskId != task.TaskId)
                return BadRequest("TaskId mismatch");

            if (string.IsNullOrEmpty(task.ProjectId))
                return BadRequest("ProjectId is required in request body");

            
            var updatedTask = await _taskService.UpdateTaskAsync(task);
            return Ok(updatedTask);
        }


        //// DELETE api/task/{taskId}?projectId=123
        [HttpDelete("{taskId}")]
        public async Task<ActionResult> DeleteTask(string taskId, [FromQuery] string projectId)
        {
            if (string.IsNullOrEmpty(projectId))
                return BadRequest("ProjectId is required");

            var result = await _taskService.DeleteTaskAsync(taskId, projectId);
            if (!result) return NotFound();
            return NoContent();
        }
    }
}
