using BLL.DTOs;
using DAL.Entities;
using DAL.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services
{
    public interface INotificationService
    {
        Task CreateTaskAssignedNotificationAsync(TaskModel task, string assignedByUserId, string projectName = null);
        Task CreateTaskStatusChangedNotificationAsync(TaskModel task, string oldStatus, string projectName = null);
        Task CreateTaskDueDateReminderAsync(TaskModel task, string projectName = null);
        Task CreateTaskOverdueNotificationAsync(TaskModel task, string projectName = null);
        Task<List<NotificationDto>> GetUserNotificationsAsync(string userId, int limit = 50, bool unreadOnly = false);
        Task<bool> MarkAsReadAsync(string userId, string notificationId);
        Task<bool> MarkAllAsReadAsync(string userId);
        Task<int> GetUnreadCountAsync(string userId);
        Task<bool> DeleteNotificationAsync(string userId, string notificationId);
    }

    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IProjectRepository _projectRepository;

        public NotificationService(
            INotificationRepository notificationRepository,
            IUserRepository userRepository,
            IProjectRepository projectRepository)
        {
            _notificationRepository = notificationRepository;
            _userRepository = userRepository;
            _projectRepository = projectRepository;
        }

        public async Task CreateTaskAssignedNotificationAsync(TaskModel task, string assignedByUserId, string projectName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(task.Assignee)) return;

                var assignedBy = await _userRepository.GetUserByIdAsync(assignedByUserId);
                var assignedByName = assignedBy?.Username ?? assignedBy?.Name ?? "Manager";

                if (string.IsNullOrEmpty(projectName))
                {
                    var project = await _projectRepository.GetProjectByIdAsync(task.ProjectId);
                    projectName = project?.Name ?? "Dự án";
                }

                var notification = new NotificationModel
                {
                    UserId = task.Assignee,
                    Type = "TASK_ASSIGNED",
                    Title = "Task mới được giao",
                    Message = $"{assignedByName} đã giao cho bạn task: {task.Title}",
                    RelatedEntityType = "TASK",
                    RelatedEntityId = task.TaskId,
                    ProjectId = task.ProjectId,
                    TaskId = task.TaskId,
                    ActionUrl = $"/projects/{task.ProjectId}/tasks/{task.TaskId}",
                    Icon = "📋",
                    Priority = task.Priority ?? "MEDIUM",
                    CreatedBy = assignedByUserId
                };

                var created = await _notificationRepository.CreateNotificationAsync(notification);
                Console.WriteLine($"✅ Task assigned notification created for user {task.Assignee}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error creating task assigned notification: {ex.Message}");
            }
        }

        public async Task CreateTaskStatusChangedNotificationAsync(TaskModel task, string oldStatus, string projectName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(task.Assignee)) return;

                if (string.IsNullOrEmpty(projectName))
                {
                    var project = await _projectRepository.GetProjectByIdAsync(task.ProjectId);
                    projectName = project?.Name ?? "Dự án";
                }

                var notification = new NotificationModel
                {
                    UserId = task.Assignee,
                    Type = "TASK_STATUS_CHANGED",
                    Title = "Cập nhật trạng thái task",
                    Message = $"Task '{task.Title}' đã chuyển từ {GetStatusText(oldStatus)} sang {GetStatusText(task.Status)}",
                    RelatedEntityType = "TASK",
                    RelatedEntityId = task.TaskId,
                    ProjectId = task.ProjectId,
                    TaskId = task.TaskId,
                    ActionUrl = $"/projects/{task.ProjectId}/tasks/{task.TaskId}",
                    Icon = "🔄",
                    Priority = "MEDIUM"
                };

                var created = await _notificationRepository.CreateNotificationAsync(notification);
                Console.WriteLine($"✅ Task status changed notification created for user {task.Assignee}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error creating task status changed notification: {ex.Message}");
            }
        }

        public async Task CreateTaskDueDateReminderAsync(TaskModel task, string projectName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(task.Assignee) || string.IsNullOrEmpty(task.DueDate)) return;

                if (string.IsNullOrEmpty(projectName))
                {
                    var project = await _projectRepository.GetProjectByIdAsync(task.ProjectId);
                    projectName = project?.Name ?? "Dự án";
                }

                var dueDate = DateTime.Parse(task.DueDate);
                var hoursLeft = (dueDate - DateTime.UtcNow).TotalHours;

                var notification = new NotificationModel
                {
                    UserId = task.Assignee,
                    Type = "TASK_DUE_SOON",
                    Title = "Task sắp đến hạn",
                    Message = $"Task '{task.Title}' sẽ đến hạn trong {(hoursLeft > 24 ? Math.Ceiling(hoursLeft / 24) + " ngày" : Math.Ceiling(hoursLeft) + " giờ")}",
                    RelatedEntityType = "TASK",
                    RelatedEntityId = task.TaskId,
                    ProjectId = task.ProjectId,
                    TaskId = task.TaskId,
                    ActionUrl = $"/projects/{task.ProjectId}/tasks/{task.TaskId}",
                    Icon = "⏰",
                    Priority = "HIGH"
                };

                var created = await _notificationRepository.CreateNotificationAsync(notification);
                Console.WriteLine($"✅ Task due date reminder created for user {task.Assignee}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error creating task due date reminder: {ex.Message}");
            }
        }

        public async Task CreateTaskOverdueNotificationAsync(TaskModel task, string projectName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(task.Assignee) || string.IsNullOrEmpty(task.DueDate)) return;

                if (string.IsNullOrEmpty(projectName))
                {
                    var project = await _projectRepository.GetProjectByIdAsync(task.ProjectId);
                    projectName = project?.Name ?? "Dự án";
                }

                var notification = new NotificationModel
                {
                    UserId = task.Assignee,
                    Type = "TASK_OVERDUE",
                    Title = "Task đã quá hạn",
                    Message = $"Task '{task.Title}' đã quá hạn. Vui lòng cập nhật tiến độ ngay!",
                    RelatedEntityType = "TASK",
                    RelatedEntityId = task.TaskId,
                    ProjectId = task.ProjectId,
                    TaskId = task.TaskId,
                    ActionUrl = $"/projects/{task.ProjectId}/tasks/{task.TaskId}",
                    Icon = "🚨",
                    Priority = "HIGH"
                };

                var created = await _notificationRepository.CreateNotificationAsync(notification);
                Console.WriteLine($"✅ Task overdue notification created for user {task.Assignee}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error creating task overdue notification: {ex.Message}");
            }
        }

        public async Task<List<NotificationDto>> GetUserNotificationsAsync(string userId, int limit = 50, bool unreadOnly = false)
        {
            var notifications = await _notificationRepository.GetUserNotificationsAsync(userId, limit, unreadOnly);
            return notifications.Select(MapToDto).ToList();
        }

        public async Task<bool> MarkAsReadAsync(string userId, string notificationId)
        {
            return await _notificationRepository.MarkAsReadAsync(userId, notificationId);
        }

        public async Task<bool> MarkAllAsReadAsync(string userId)
        {
            return await _notificationRepository.MarkAllAsReadAsync(userId);
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            return await _notificationRepository.GetUnreadCountAsync(userId);
        }

        public async Task<bool> DeleteNotificationAsync(string userId, string notificationId)
        {
            return await _notificationRepository.DeleteNotificationAsync(userId, notificationId);
        }

        private NotificationDto MapToDto(NotificationModel notification)
        {
            return new NotificationDto
            {
                Id = notification.NotificationId,
                Type = notification.Type,
                Title = notification.Title,
                Message = notification.Message,
                Icon = notification.Icon,
                Priority = notification.Priority,
                IsRead = notification.IsRead,
                CreatedAt = notification.CreatedAt,
                ReadAt = notification.ReadAt,
                ActionUrl = notification.ActionUrl,
                RelatedEntityType = notification.RelatedEntityType,
                RelatedEntityId = notification.RelatedEntityId,
                ProjectId = notification.ProjectId,
                TaskId = notification.TaskId
            };
        }

        private string GetStatusText(string status)
        {
            return status switch
            {
                "TODO" => "Chờ thực hiện",
                "IN_PROGRESS" => "Đang thực hiện",
                "IN_REVIEW" => "Đang review",
                "DONE" => "Hoàn thành",
                "CANCELLED" => "Đã hủy",
                _ => status
            };
        }
    }
}