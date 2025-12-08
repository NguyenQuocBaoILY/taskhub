using BLL.DTOs;
using BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Web_API.AppHost.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] int limit = 50, [FromQuery] bool unreadOnly = false)
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var notifications = await _notificationService.GetUserNotificationsAsync(userId, limit, unreadOnly);
            return Ok(notifications);
        }

        [HttpGet("count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
            var totalNotifications = await _notificationService.GetUserNotificationsAsync(userId, 1000);
            
            return Ok(new NotificationCountDto 
            { 
                UnreadCount = unreadCount, 
                TotalCount = totalNotifications.Count 
            });
        }

        [HttpPatch("{notificationId}/read")]
        public async Task<IActionResult> MarkAsRead(string notificationId)
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var success = await _notificationService.MarkAsReadAsync(userId, notificationId);
            if (!success)
                return NotFound();

            return NoContent();
        }

        [HttpPatch("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var success = await _notificationService.MarkAllAsReadAsync(userId);
            if (!success)
                return BadRequest();

            return NoContent();
        }

        [HttpDelete("{notificationId}")]
        public async Task<IActionResult> DeleteNotification(string notificationId)
        {
            var userId = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var success = await _notificationService.DeleteNotificationAsync(userId, notificationId);
            if (!success)
                return NotFound();

            return NoContent();
        }
    }
}