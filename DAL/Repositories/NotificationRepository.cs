using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using DAL.Entities;
using DAL.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DAL.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly IDynamoDBContext _context;

        public NotificationRepository(IDynamoDBContext context)
        {
            _context = context;
        }

        public async Task<NotificationModel> CreateNotificationAsync(NotificationModel notification)
        {
            notification.InitializeKeys();
            await _context.SaveAsync(notification);
            return notification;
        }

        public async Task<List<NotificationModel>> GetUserNotificationsAsync(string userId, int limit = 50, bool unreadOnly = false)
        {
            if (string.IsNullOrEmpty(userId)) return new List<NotificationModel>();

            try
            {
                // Dùng Scan đơn giản để tránh lỗi DynamoDBOperationConfig
                var conditions = new List<ScanCondition>
                {
                    new ScanCondition("GSI1PK", ScanOperator.Equal, $"USER#{userId}#NOTIFICATION")
                };

                var search = _context.ScanAsync<NotificationModel>(conditions);
                var allNotifications = await search.GetRemainingAsync();
                
                var notifications = allNotifications
                    .Select(MapFromDb)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToList();

                if (unreadOnly)
                {
                    notifications = notifications.Where(n => !n.IsRead).ToList();
                }

                return notifications.Take(limit).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting notifications: {ex.Message}");
                return new List<NotificationModel>();
            }
        }

        public async Task<bool> MarkAsReadAsync(string userId, string notificationId)
        {
            try
            {
                // Tìm notification với PK và SK pattern
                var conditions = new List<ScanCondition>
                {
                    new ScanCondition("PK", ScanOperator.Equal, $"USER#{userId}"),
                    new ScanCondition("SK", ScanOperator.BeginsWith, $"NOTIFICATION#{notificationId}")
                };

                var search = _context.ScanAsync<NotificationModel>(conditions);
                var notifications = await search.GetRemainingAsync();
                var notification = notifications.FirstOrDefault();

                if (notification == null) return false;

                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow.ToString("o");
                notification.UpdatedAt = DateTime.UtcNow.ToString("o");

                await _context.SaveAsync(notification);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error marking notification as read: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> MarkAllAsReadAsync(string userId)
        {
            try
            {
                var notifications = await GetUserNotificationsAsync(userId, 100, true); // Get unread
                
                foreach (var notification in notifications)
                {
                    var success = await MarkAsReadAsync(userId, notification.NotificationId);
                    if (!success)
                    {
                        Console.WriteLine($"Failed to mark notification {notification.NotificationId} as read");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error marking all notifications as read: {ex.Message}");
                return false;
            }
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            try
            {
                var unreadNotifications = await GetUserNotificationsAsync(userId, 1000, true);
                return unreadNotifications.Count;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<bool> DeleteNotificationAsync(string userId, string notificationId)
        {
            try
            {
                var conditions = new List<ScanCondition>
                {
                    new ScanCondition("PK", ScanOperator.Equal, $"USER#{userId}"),
                    new ScanCondition("SK", ScanOperator.BeginsWith, $"NOTIFICATION#{notificationId}")
                };

                var search = _context.ScanAsync<NotificationModel>(conditions);
                var notifications = await search.GetRemainingAsync();
                var notification = notifications.FirstOrDefault();

                if (notification == null) return false;

                await _context.DeleteAsync<NotificationModel>(notification.PK, notification.SK);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting notification: {ex.Message}");
                return false;
            }
        }

        public async Task<NotificationModel> GetNotificationAsync(string userId, string notificationId)
        {
            try
            {
                var conditions = new List<ScanCondition>
                {
                    new ScanCondition("PK", ScanOperator.Equal, $"USER#{userId}"),
                    new ScanCondition("SK", ScanOperator.BeginsWith, $"NOTIFICATION#{notificationId}")
                };

                var search = _context.ScanAsync<NotificationModel>(conditions);
                var notifications = await search.GetRemainingAsync();
                var notification = notifications.FirstOrDefault();

                return notification != null ? MapFromDb(notification) : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting notification: {ex.Message}");
                return null;
            }
        }

        private NotificationModel MapFromDb(NotificationModel notification)
        {
            if (notification?.PK != null && notification.PK.StartsWith("USER#"))
            {
                notification.UserId = notification.PK.Substring("USER#".Length);
            }

            if (notification?.SK != null && notification.SK.StartsWith("NOTIFICATION#"))
            {
                var skPart = notification.SK.Substring("NOTIFICATION#".Length);
                var parts = skPart.Split('#');
                if (parts.Length > 0)
                {
                    notification.NotificationId = parts[0];
                }
            }

            return notification;
        }
    }
}