using Amazon.DynamoDBv2.DataModel;
using System;

namespace DAL.Entities
{
    [DynamoDBTable("TaskHub-Activity")]
    public class NotificationModel
    {
        // PK = USER#{UserId}
        [DynamoDBHashKey]
        public string PK { get; set; }

        // SK = NOTIFICATION#{NotificationId}#{CreatedAt}
        [DynamoDBRangeKey]
        public string SK { get; set; }

        [DynamoDBProperty("entityType")]
        public string EntityType { get; set; } = "NOTIFICATION";

        [DynamoDBIgnore]
        public string UserId { get; set; }

        [DynamoDBIgnore]
        public string NotificationId { get; set; }

        // Từ schema: action field
        [DynamoDBProperty("action")]
        public string Type { get; set; } // TASK_ASSIGNED, TASK_STATUS_CHANGED, TASK_DUE_SOON, etc.

        [DynamoDBProperty("message")]
        public string Title { get; set; }

        [DynamoDBProperty("content")]
        public string Message { get; set; }

        [DynamoDBProperty("relatedEntityType")]
        public string RelatedEntityType { get; set; } // TASK, PROJECT

        [DynamoDBProperty("relatedEntityId")]
        public string RelatedEntityId { get; set; } // TaskId, ProjectId

        [DynamoDBProperty("projectId")]
        public string ProjectId { get; set; }

        [DynamoDBProperty("taskId")]
        public string TaskId { get; set; }

        [DynamoDBProperty("isRead")]
        public bool IsRead { get; set; } = false;

        [DynamoDBProperty("createdAt")]
        public string CreatedAt { get; set; }

        [DynamoDBProperty("updatedAt")]
        public string UpdatedAt { get; set; }

        [DynamoDBProperty("createdBy")]
        public string CreatedBy { get; set; }

        [DynamoDBProperty("actionUrl")]
        public string ActionUrl { get; set; } // URL để navigate khi click notification

        // Custom properties (không có trong schema, có thể thêm)
        [DynamoDBProperty("icon")]
        public string Icon { get; set; } // Icon class hoặc emoji

        [DynamoDBProperty("priority")]
        public string Priority { get; set; } // LOW, MEDIUM, HIGH

        [DynamoDBProperty("readAt")]
        public string ReadAt { get; set; }

        // GSI1 - UserNotifications (từ schema TaskHub-Activity)
        [DynamoDBGlobalSecondaryIndexHashKey("GSI1-UserNotifications")]
        public string GSI1PK { get; set; }

        [DynamoDBGlobalSecondaryIndexRangeKey("GSI1-UserNotifications")]
        public string GSI1SK { get; set; }

        // TTL
        [DynamoDBProperty("expiresAt")]
        public long? ExpiresAt { get; set; }

        public void InitializeKeys()
        {
            if (string.IsNullOrEmpty(NotificationId))
                NotificationId = Guid.NewGuid().ToString();

            var timestamp = DateTime.UtcNow.ToString("o");
            CreatedAt = timestamp;
            UpdatedAt = timestamp;

            PK = $"USER#{UserId}";
            SK = $"NOTIFICATION#{NotificationId}#{CreatedAt}";
            
            // GSI1 cho user notifications (newest first)
            GSI1PK = $"USER#{UserId}#NOTIFICATION";
            GSI1SK = $"{CreatedAt}#{NotificationId}";

            // Set TTL 30 days from now
            ExpiresAt = ((DateTimeOffset)DateTime.UtcNow.AddDays(30)).ToUnixTimeSeconds();
        }
    }
}