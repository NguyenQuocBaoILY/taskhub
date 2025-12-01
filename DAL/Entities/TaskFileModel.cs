using Amazon.DynamoDBv2.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    [DynamoDBTable("TaskHub-Files")]
    public class TaskFileModel
    {
        // PK = FILE#{fileId}
        [DynamoDBHashKey]
        public string PK { get; set; }

        // SK = TASK#{taskId}
        [DynamoDBRangeKey]
        public string SK { get; set; }

        [DynamoDBProperty("entityType")]
        public string EntityType { get; set; } = "FILE";

        [DynamoDBIgnore]
        public string FileId { get; set; }

        [DynamoDBIgnore]
        public string TaskId { get; set; }

        [DynamoDBProperty]
        public string ProjectId { get; set; }

        [DynamoDBProperty]
        public string FileName { get; set; }

        [DynamoDBProperty]
        public string StoreName { get; set; }

        [DynamoDBProperty]
        public string Url { get; set; }

        [DynamoDBProperty]
        public string ContentType { get; set; }

        [DynamoDBProperty]
        public long Size { get; set; }

        [DynamoDBProperty]
        public string UploadedBy { get; set; }

        [DynamoDBProperty]
        public string UploadedAt { get; set; }

        [DynamoDBGlobalSecondaryIndexHashKey("GSI1-TaskFiles")]
        public string GSI1PK { get; set; }

        [DynamoDBGlobalSecondaryIndexRangeKey("GSI1-TaskFiles")]
        public string GSI1SK { get; set; }

        public void InitKeys()
        {
            PK = $"FILE#{FileId}";
            SK = $"TASK#{TaskId}";
            GSI1PK = $"TASK#{TaskId}#FILE";
            GSI1SK = $"{UploadedAt}#{FileId}";
        }
    }
}
