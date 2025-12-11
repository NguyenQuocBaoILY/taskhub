using BLL.DTOs;
using BLL.Services;
using DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebAPI.APPHost.Controllers
{
    [ApiController]
    [Route("api/task")]
    public class TaskController : ControllerBase
    {
        private readonly ProjectMemberService _memberService;
        private readonly TaskFileService _taskFileService;
        private readonly TaskService _taskService;

        public TaskController(
            TaskService taskService,
            ProjectMemberService memberService,
            TaskFileService taskFileService)
        {
            _taskService = taskService;
            _memberService = memberService;
            _taskFileService = taskFileService;
        }

        private bool isAdmin()
        {
            var raw = User?.FindFirst("isAdmin")?.Value;
            return raw == "1";
        }


        private bool isManager(ProjectMemberModel member)
        {
            return member != null &&
                string.Equals(member.Role, "manager", StringComparison.OrdinalIgnoreCase);
        }

        private bool isAssignee(TaskModel task, string userId)
        {
            return task != null &&
                   !string.IsNullOrEmpty(task.Assignee) &&
                   string.Equals(task.Assignee, userId, StringComparison.OrdinalIgnoreCase);
        }


        [HttpGet]
        public async Task<ActionResult<List<TaskModel>>> ListTasks(
            [FromQuery] string projectId,
            [FromQuery] string userId,
            [FromQuery] string status = null,
            [FromQuery] string assignee = null)
        {
            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(userId))
                return BadRequest("projectId & userId required");

            Console.WriteLine($"[ListTasks] isAdmin() = {isAdmin()}");
            Console.WriteLine($"[ListTasks] userId(query) = {userId}");
            Console.WriteLine($"[ListTasks] projectId = {projectId}");

            if (!isAdmin())
            {
                var member = await _memberService.GetProjectMemberAsync(projectId, userId);
                if (member == null) return Forbid();

                if (!isManager(member))
                    assignee = userId;
            }

            var tasks = await _taskService.ListTasksAsync(projectId, status, assignee);
            return Ok(tasks);
        }

        [HttpGet("{taskId}")]
        public async Task<ActionResult<TaskModel>> GetTask(
            [FromRoute] string taskId,
            [FromQuery] string projectId,
            [FromQuery] string userId)
        {
            if (string.IsNullOrEmpty(taskId) || string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(userId))
                return BadRequest("taskId, projectId, userId required");

            var task = await _taskService.GetTaskAsync(projectId, taskId);
            if (task == null) return NotFound();

            if (!isAdmin())
            {
                var member = await _memberService.GetProjectMemberAsync(projectId, userId);
                if (member == null) return Forbid();

                if (!isManager(member) && !isAssignee(task, userId))
                    return Forbid();
            }
            return Ok(task);
        }

        [HttpPost]
        public async Task<ActionResult<TaskModel>> CreateTask(
            [FromQuery] string projectId,
            [FromQuery] string userId,
            [FromBody] TaskModel task)
        {
            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(userId))
                return BadRequest("projectId & userId required");
            if (task == null || string.IsNullOrEmpty(task.Title))
                return BadRequest("Title required");

            ProjectMemberModel member = null;

            if (!isAdmin())
            {
                member = await _memberService.GetProjectMemberAsync(projectId, userId);
                if (member == null || !isManager(member))
                    return Forbid();
            }

            task.ProjectId = projectId;
            if (string.IsNullOrEmpty(task.Assignee))
                task.Assignee = userId;

            var created = await _taskService.CreateTaskAsync(task);
            return Ok(created);
        }

        [HttpPut("{taskId}")]
        public async Task<ActionResult<TaskModel>> UpdateTask(
            [FromRoute] string taskId,
            [FromQuery] string projectId,
            [FromQuery] string userId,
            [FromBody] TaskModel task)
        {
            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(taskId) || string.IsNullOrEmpty(userId))
                return BadRequest("projectId & taskId required");
            if (task == null) return BadRequest("Body required");

            ProjectMemberModel member = null;

            if (!isAdmin())
            {
                member = await _memberService.GetProjectMemberAsync(projectId, userId);
                if (member == null || !isManager(member))
                    return Forbid();
            }

            var existing = await _taskService.GetTaskAsync(projectId, taskId);
            if (existing == null) return NotFound();

            existing.Title = task.Title ?? existing.Title;
            existing.Description = task.Description ?? existing.Description;
            existing.Assignee = string.IsNullOrEmpty(task.Assignee) ? existing.Assignee : task.Assignee;
            existing.DueDate = task.DueDate ?? existing.DueDate;
            existing.Priority = task.Priority ?? existing.Priority;
            existing.AssignedAt = string.IsNullOrEmpty(existing.AssignedAt) ? DateTime.UtcNow.ToString("o") : existing.AssignedAt;
            existing.Status = string.IsNullOrEmpty(task.Status) ? existing.Status : task.Status;

            var updated = await _taskService.UpdateTaskAsync(existing);
            return Ok(updated);
        }

        [HttpPatch("{taskId}/status")]
        public async Task<IActionResult> PatchStatus(
            [FromRoute] string taskId,
            [FromQuery] string projectId,
            [FromQuery] string userId,
            [FromBody] PatchStatusRequest body)
        {
            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(taskId) || string.IsNullOrEmpty(userId))
                return BadRequest("projectId & taskId required");

            string newStatus = body.Status;
            if (string.IsNullOrEmpty(newStatus))
                return BadRequest("status required");

            var task = await _taskService.GetTaskAsync(projectId, taskId);
            if (task == null) return NotFound();

            if (!isAdmin())
            {
                var member = await _memberService.GetProjectMemberAsync(projectId, userId);
                if (member == null)
                    return Forbid();

                if (!isManager(member) && !isAssignee(task, userId))
                    return Forbid();
            }


            var ok = await _taskService.UpdateStatusAsync(projectId, taskId, newStatus);
            if (!ok) return NotFound();
            return NoContent();
        }

        [HttpDelete("{taskId}")]
        public async Task<IActionResult> DeleteTask(
            [FromRoute] string taskId,
            [FromQuery] string userId,
            [FromQuery] string projectId)
        {
            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(taskId) || string.IsNullOrEmpty(userId))
                return BadRequest("projectId & taskId required");

            ProjectMemberModel member = null;

            if (!isAdmin())
            {
                member = await _memberService.GetProjectMemberAsync(projectId, userId);
                if (member == null || !isManager(member)) return Forbid();
            }

            var ok = await _taskService.DeleteTaskAsync(projectId, taskId);
            if (!ok) return NotFound();
            return NoContent();
        }

        [HttpGet("{taskId}/files")]
        public async Task<ActionResult<List<TaskFileModel>>> ListFiles(
            [FromRoute] string taskId,
            [FromQuery] string projectId,
            [FromQuery] string userId)
        {
            if (string.IsNullOrEmpty(taskId))
                return BadRequest("taskId is required");

            var currentUserId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            if (isAdmin())
            {
                var filesAdmin = await _taskFileService.ListAsync(taskId);
                return Ok(filesAdmin);
            }

            if (string.IsNullOrEmpty(projectId))
                return BadRequest("projectId required for non-admin");

            var member = await _memberService.GetProjectMemberAsync(projectId, userId);
            if (member == null) return Forbid();

            var task = await _taskService.GetTaskAsync(projectId, taskId);
            if (task == null) return NotFound("Task not found");

            if (isManager(member))
            {
                var filesManager = await _taskFileService.ListAsync(taskId);
                return Ok(filesManager);
            }

            if (!isAssignee(task, userId))
                return Forbid();

            var files = await _taskFileService.ListAsync(taskId);
            return Ok(files);
        }

        [HttpPost("{taskId}/files")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<TaskFileModel>> UploadFile(
             [FromRoute] string taskId,
             [FromQuery] string projectId,
             [FromQuery] string userId,
             IFormFile file)
        {
            if (string.IsNullOrEmpty(taskId) || string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(userId))
                return BadRequest("taskId, projectId, userId required");

            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            if (isAdmin())
            {
                var taskAdmin = await _taskService.GetTaskAsync(projectId, taskId);
                if (taskAdmin == null) return NotFound("Task not found");

                var savedAdmin = await _taskFileService.UploadAsync(
                    taskId,
                    projectId,
                    userId,
                    file.FileName,
                    file.ContentType ?? "application/octet-stream",
                    file.OpenReadStream());

                return Ok(savedAdmin);
            }

            var member = await _memberService.GetProjectMemberAsync(projectId, userId);
            if (member == null) return Forbid();

            var task = await _taskService.GetTaskAsync(projectId, taskId);
            if (task == null) return NotFound("Task not found");

            if (!isManager(member) && !isAssignee(task, userId))
                return Forbid();

            var saved = await _taskFileService.UploadAsync(
                taskId,
                projectId,
                userId,
                file.FileName,
                file.ContentType ?? "application/octet-stream",
                file.OpenReadStream());

            return Ok(saved);
        }


        [HttpGet("{taskId}/files/{fileId}/download")]
        public async Task<IActionResult> DownloadFile(
            [FromRoute] string taskId,
            [FromRoute] string fileId,
            [FromQuery] string projectId,
            [FromQuery] string userId)
        {
            if (string.IsNullOrEmpty(taskId) || string.IsNullOrEmpty(fileId))
                return BadRequest("taskId & fileId required");

            var file = await _taskFileService.GetFileAsync(fileId);
            if (file == null)
                return NotFound("File not found");

            if (file.TaskId != taskId)
                return NotFound("File does not belong to this task");

            // Check permissions
            if (!isAdmin())
            {
                if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(userId))
                    return BadRequest("projectId & userId required for non-admin");

                var member = await _memberService.GetProjectMemberAsync(projectId, userId);
                if (member == null)
                    return Forbid();

                var task = await _taskService.GetTaskAsync(projectId, taskId);
                if (task == null)
                    return NotFound("Task not found");

                if (!isManager(member) && !isAssignee(task, userId))
                    return Forbid();
            }

            try
            {
                var fileStream = await _taskFileService.DownloadAsync(fileId);
                
                // Đảm bảo content-type đúng và thêm headers cần thiết
                var contentType = file.ContentType ?? "application/octet-stream";
                
                Console.WriteLine($"[Download] File: {file.FileName}, ContentType: {contentType}, Size: {fileStream.Length}");
                
                return File(fileStream, contentType, file.FileName, enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Download error: {ex.Message}");
                return NotFound("File not found in storage");
            }
        }

        [HttpDelete("{taskId}/files/{fileId}")]
        public async Task<IActionResult> DeleteFile(
            [FromRoute] string taskId,
            [FromRoute] string fileId,
            [FromQuery] string projectId,
            [FromQuery] string userId)
        {
            if (string.IsNullOrEmpty(taskId) || string.IsNullOrEmpty(fileId))
            {
                return BadRequest("taskId & fileId required");
            }

            var file = await _taskFileService.GetFileAsync(fileId);
            if (file == null)
            {
                return NotFound("File not found");
            }

            if (file.TaskId != taskId)
            {
                return NotFound("File does not belong to this task");
            }

            if (isAdmin())
            {
                var okAdmin = await _taskFileService.DeleteAsync(fileId);
                if (!okAdmin)
                    return NotFound("Delete failed");

                return NoContent();
            }

            if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(userId))
            {
                return BadRequest("projectId & userId required for non-admin");
            }

            var task = await _taskService.GetTaskAsync(projectId, taskId);
            if (task == null)
            {
                return NotFound("Task not found");
            }

            var member = await _memberService.GetProjectMemberAsync(projectId, userId);
            if (member == null)
            {
                return Forbid();
            }

            if (!isManager(member) && !isAssignee(task, userId))
            {
                return Forbid();
            }

            var ok = await _taskFileService.DeleteAsync(fileId);
            if (!ok)
            {
                return NotFound("Delete failed");
            }

            return NoContent();
        }

    }
}