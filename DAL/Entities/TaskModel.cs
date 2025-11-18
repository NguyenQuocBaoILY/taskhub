using Amazon.DynamoDBv2.DataModel;
using System;
using System.Collections.Generic;

namespace DAL.Entities
{
    [DynamoDBTable("TaskHub-Core")]
    public class TaskModel
    {
        [DynamoDBHashKey]  // Partition Key
        public string PK { get; set; }

        [DynamoDBRangeKey] // Sort Key
        public string SK { get; set; }

        [DynamoDBProperty]
        public string TaskId { get; set; }

        [DynamoDBProperty]
        public string ProjectId { get; set; }

        [DynamoDBProperty]
        public string Title { get; set; }

        [DynamoDBProperty]
        public string Description { get; set; }

        [DynamoDBProperty]
        public string Status { get; set; } // TODO, IN_PROGRESS, DONE...

        [DynamoDBProperty]
        public string Type { get; set; }   // STORY, BUG, TASK, ...

        [DynamoDBProperty]
        public string Priority { get; set; } // LOWEST → HIGHEST

        [DynamoDBProperty]
        public string Assignee { get; set; }

        [DynamoDBProperty]
        public string Reporter { get; set; }

        [DynamoDBProperty]
        public string DueDate { get; set; }

        [DynamoDBProperty]
        public string StartDate { get; set; }

        [DynamoDBProperty]
        public string EndDate { get; set; }

        [DynamoDBProperty]
        public int? StoryPoints { get; set; }

        [DynamoDBProperty]
        public List<string> Labels { get; set; }

        [DynamoDBProperty]
        public string CreatedAt { get; set; }

        [DynamoDBProperty]
        public string UpdatedAt { get; set; }

        [DynamoDBGlobalSecondaryIndexHashKey("GSI5PK")]
        public string GSI5PK { get; set; }

        [DynamoDBGlobalSecondaryIndexRangeKey("GSI5SK")]
        public string GSI5SK { get; set; }
    }
}
