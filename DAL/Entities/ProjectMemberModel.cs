using Amazon.DynamoDBv2.DataModel;
using System.Collections.Generic;

namespace DAL.Entities
{
    [DynamoDBTable("TaskHub-Core")]
    public class ProjectMemberModel
    {
        // PK = PROJECT#{projectId}
        [DynamoDBHashKey]
        public string PK { get; set; }

        // SK = MEMBER#{userId}
        [DynamoDBRangeKey]
        public string SK { get; set; }

        [DynamoDBProperty]
        public string EntityType { get; set; } = "PROJECT_MEMBER";

        [DynamoDBProperty]
        public string ProjectId { get; set; }

        [DynamoDBProperty]
        public string UserId { get; set; }

        [DynamoDBProperty]
        public string Role { get; set; }

        [DynamoDBProperty]
        public bool IsAdmin { get; set; }

        // granular permissions saved as string set
        [DynamoDBProperty]
        public List<string> Permissions { get; set; }

        [DynamoDBProperty]
        public string MemberStatus { get; set; } = "ACTIVE";

        [DynamoDBProperty]
        public string JoinedAt { get; set; }

        [DynamoDBProperty]
        public string AddedBy { get; set; }

        [DynamoDBProperty]
        public string RemovedBy { get; set; }

        [DynamoDBProperty]
        public string RemovedAt { get; set; }

        [DynamoDBProperty]
        public string LastAccessedAt { get; set; }

        [DynamoDBProperty]
        public string CreatedAt { get; set; }

        [DynamoDBProperty]
        public string UpdatedAt { get; set; }

        // GSI7 - User -> Projects
        [DynamoDBGlobalSecondaryIndexHashKey("GSI7-UserProjects")]
        public string GSI7PK { get; set; } // USER#{userId}#PROJECT

        [DynamoDBGlobalSecondaryIndexRangeKey("GSI7-UserProjects")]
        public string GSI7SK { get; set; } // PROJECT#{projectId}#{role}#{joinedAt}

        // GSI8 - Project -> Members
        [DynamoDBGlobalSecondaryIndexHashKey("GSI8-ProjectMembers")]
        public string GSI8PK { get; set; } // PROJECT#{projectId}#MEMBER

        [DynamoDBGlobalSecondaryIndexRangeKey("GSI8-ProjectMembers")]
        public string GSI8SK { get; set; } // {role}#{memberStatus}#{joinedAt}

        // Not persisted to DynamoDB — chỉ dùng để trả cho FE
        [DynamoDBIgnore]
        public string Username { get; set; }
    }
}