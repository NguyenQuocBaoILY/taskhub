using DAL.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DAL.Interfaces
{
    public interface INotificationRepository
    {
        Task<NotificationModel> CreateNotificationAsync(NotificationModel notification);
        Task<List<NotificationModel>> GetUserNotificationsAsync(string userId, int limit = 50, bool unreadOnly = false);
        Task<bool> MarkAsReadAsync(string userId, string notificationId);
        Task<bool> MarkAllAsReadAsync(string userId);
        Task<int> GetUnreadCountAsync(string userId);
        Task<bool> DeleteNotificationAsync(string userId, string notificationId);
        Task<NotificationModel> GetNotificationAsync(string userId, string notificationId);
    }
}