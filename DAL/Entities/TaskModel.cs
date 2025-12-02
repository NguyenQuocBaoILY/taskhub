using Amazon.DynamoDBv2.DataModel;
using System;
using System.Collections.Generic;

namespace DAL.Entities
{
    [DynamoDBTable("TaskHub-Core")]
    public class TaskModel
    {
        // PK = PROJECT#{ProjectId}
        [DynamoDBHashKey]
        public string PK { get; set; }

        // SK = TASK#{TaskId}
        [DynamoDBRangeKey]
        public string SK { get; set; }

        [DynamoDBProperty]
        public string EntityType { get; set; } = "TASK";

        [DynamoDBIgnore]
        public string ProjectId { get; set; }

        [DynamoDBIgnore]
        public string TaskId { get; set; }

        [DynamoDBProperty]
        public string Title { get; set; }

        [DynamoDBProperty]
        public string Description { get; set; }

        // Người được giao (assignee)
        [DynamoDBProperty]
        public string Assignee { get; set; }

        // Ngày được giao (assignedAt - ISO 8601)
        [DynamoDBProperty]
        public string AssignedAt { get; set; }

        // Ngày kết thúc
        [DynamoDBProperty]
        public string DueDate { get; set; }

        // Trạng thái (TODO | IN_PROGRESS | IN_REVIEW | DONE | CANCELLED)
        [DynamoDBProperty]
        public string Status { get; set; }

        [DynamoDBProperty]
        public string Priority { get; set; }

        [DynamoDBProperty]
        public string CreatedAt { get; set; }

        [DynamoDBProperty]
        public string UpdatedAt { get; set; }

        // GSI3 - UserAssignedTasks
        [DynamoDBGlobalSecondaryIndexHashKey("GSI3-UserAssignedTasks")]
        public string GSI3PK { get; set; }

        [DynamoDBGlobalSecondaryIndexRangeKey("GSI3-UserAssignedTasks")]
        public string GSI3SK { get; set; }

        // GSI4 - TasksByStatus
        [DynamoDBGlobalSecondaryIndexHashKey("GSI4-TasksByStatus")]
        public string GSI4PK { get; set; }

        [DynamoDBGlobalSecondaryIndexRangeKey("GSI4-TasksByStatus")]
        public string GSI4SK { get; set; }

        // GSI5 - ProjectTasks
        [DynamoDBGlobalSecondaryIndexHashKey("GSI5-ProjectTasks")]
        public string GSI5PK { get; set; }

        [DynamoDBGlobalSecondaryIndexRangeKey("GSI5-ProjectTasks")]
        public string GSI5SK { get; set; }

        // Helper: khởi tạo key
        public void InitializeKeys()
        {
            PK = $"PROJECT#{ProjectId}";
            SK = $"TASK#{TaskId}";
            GSI3PK = $"USER#{Assignee}";
            GSI3SK = $"TASK#{TaskId}";
            GSI4PK = $"PROJECT#{ProjectId}#STATUS#{Status}";
            GSI4SK = $"TASK#{TaskId}";
            GSI5PK = $"PROJECT#{ProjectId}";
            GSI5SK = $"TASK#{TaskId}";
        }
    }
}
