using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;

namespace DAL.Entities
{
    [DynamoDBTable("TaskHub-Core")]
    public class ProjectModel
    {
        [DynamoDBHashKey]
        public string PK { get; set; }

        // SK = PROJECT#<ProjectId>
        [DynamoDBRangeKey]
        public string SK { get; set; }

        // ID duy nhất của Project
        [DynamoDBProperty]
        public string ProjectId { get; set; }

        [DynamoDBProperty]
        public string Name { get; set; }

        [DynamoDBProperty]
        public string Description { get; set; }

        // USER#ABC
        [DynamoDBProperty]
        public string OwnerUserId { get; set; }

        [DynamoDBProperty]
        public int TaskCount { get; set; }

        [DynamoDBProperty]
        public int MemberCount { get; set; }

        [DynamoDBProperty]
        public string StartDate { get; set; }

        [DynamoDBProperty]
        public string EndDate { get; set; }

        [DynamoDBProperty]
        public string CreatedAt { get; set; }

        [DynamoDBProperty]
        public string UpdatedAt { get; set; }

        // Query Project theo Owner (My Projects)
        [DynamoDBGlobalSecondaryIndexHashKey("GSI4PK")]
        public string GSI4PK { get; set; }

        [DynamoDBGlobalSecondaryIndexRangeKey("GSI4SK")]
        public string GSI4SK { get; set; }
    }
}
