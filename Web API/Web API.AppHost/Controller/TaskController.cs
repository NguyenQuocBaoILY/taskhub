using BLL.Services;
using DAL.Entities;
using DAL.Interfaces;
using k8s.KubeConfigModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BLL.DTOs;
using System.Text.Json;

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

        private async Task<(bool isAdmin, ProjectMemberModel member)> GetContextAsync(string projectId, string userId)
        {
            var member = await _memberService.GetProjectMemberAsync(projectId, userId);

            var isAdmin = member?.IsAdmin == true;
            return (isAdmin, member);
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

            var member = await _memberService.GetProjectMemberAsync(projectId, userId);
            if (member == null) return Forbid();

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

            var member = await _memberService.GetProjectMemberAsync(projectId, userId);
            if (member == null) return Forbid();

            var task = await _taskService.GetTaskAsync(projectId, taskId);
            if (task == null) return NotFound();
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

            var(isAdmin, member) = await GetContextAsync(projectId, userId);

            if (!isAdmin)
            {
                if (member == null)
                    return Forbid();
                if(!string.Equals(member.Role, "manager", StringComparison.OrdinalIgnoreCase)) return Forbid();
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

            var member = await _memberService.GetProjectMemberAsync(projectId, userId);
            if (member == null) return Forbid();

            var isAdmin = member?.IsAdmin == true;
            var isManager = string.Equals(member.Role, "manager", StringComparison.OrdinalIgnoreCase);

            if (!isAdmin && !isManager)
                return Forbid();

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

            var member = await _memberService.GetProjectMemberAsync(projectId, userId);
            if (member == null) return Forbid();

            var task = await _taskService.GetTaskAsync(projectId, taskId);
            if (task == null) return NotFound();

            var isAdmin = member?.IsAdmin == true;
            var isManager = string.Equals(member.Role, "manager", StringComparison.OrdinalIgnoreCase);
            var isAssignee = string.Equals(task.Assignee, userId, StringComparison.OrdinalIgnoreCase);

            if (!isAdmin && !isManager && !isAssignee)
                return Forbid();


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

            var member = await _memberService.GetProjectMemberAsync(projectId, userId);

            if (member == null) return Forbid();

            var isAdmin = member?.IsAdmin == true;
            var isManager = string.Equals(member.Role, "manager", StringComparison.OrdinalIgnoreCase);

            if (!isAdmin && !isManager)
                return Forbid();

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
            if (string.IsNullOrEmpty(taskId) || string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(userId))
                return BadRequest("taskId, projectId, userId required");

            var member = await _memberService.GetProjectMemberAsync(projectId, userId);
            if (member == null) return Forbid();

            var files = await _taskFileService.ListAsync(taskId);
            return Ok(files);
        }

        [HttpPost("{taskId}/files")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<TaskFileModel>> UploadFile(
            [FromRoute] string taskId,
            [FromQuery] string projectId,
            [FromQuery] string userId,
            IFormFile file) // bỏ [FromForm] ở đây
        {
            if (string.IsNullOrEmpty(taskId) || string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(userId))
                return BadRequest("taskId, projectId, userId required");

            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            var member = await _memberService.GetProjectMemberAsync(projectId, userId);
            if (member == null) return Forbid();

            var task = await _taskService.GetTaskAsync(projectId, taskId);
            if (task == null) return NotFound("Task not found");

            bool isAdmin = member?.IsAdmin == true;
            bool isManager = string.Equals(member.Role, "manager", StringComparison.OrdinalIgnoreCase); 
            bool isAssignee = string.Equals(task.Assignee, userId, StringComparison.OrdinalIgnoreCase);

            if (!isAdmin && !isManager && !isAssignee)
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

        [HttpDelete("{taskId}/files/{fileId}")]
        public async Task<IActionResult> DeleteFile(
            [FromRoute] string taskId,
            [FromRoute] string fileId,
            [FromQuery] string projectId,
            [FromQuery] string userId)
        {
            if (string.IsNullOrEmpty(taskId) || string.IsNullOrEmpty(fileId) || string.IsNullOrEmpty(userId))
                return BadRequest("taskId & fileId and userId required");

            var member = await _memberService.GetProjectMemberAsync(projectId, userId);
            if (member == null) return Forbid();

            bool isAdmin = member?.IsAdmin == true;
            bool isManager = string.Equals(member.Role, "manager", StringComparison.OrdinalIgnoreCase);

            if (!isAdmin && !isManager)
                return Forbid();

            var file = await _taskFileService.GetFileAsync(fileId);
            if (file == null || file.TaskId != taskId)
                return NotFound();

            var ok = await _taskFileService.DeleteAsync(fileId);
            if (!ok) return NotFound();
            return NoContent();
        }
    }
}