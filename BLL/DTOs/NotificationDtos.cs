using System;

namespace BLL.DTOs
{
    public class NotificationDto
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Icon { get; set; }
        public string Priority { get; set; }
        public bool IsRead { get; set; }
        public string CreatedAt { get; set; }
        public string ReadAt { get; set; }
        public string ActionUrl { get; set; }
        public string RelatedEntityType { get; set; }
        public string RelatedEntityId { get; set; }
        public string ProjectId { get; set; }
        public string TaskId { get; set; }
    }

    public class NotificationCountDto
    {
        public int UnreadCount { get; set; }
        public int TotalCount { get; set; }
    }
}