using Amazon.DynamoDBv2.DataModel;
using System;

namespace DAL.Entities
{
    [DynamoDBTable("TaskHub-Auth")]
    public class AuthSessionModel
    {
        [DynamoDBHashKey]
        public string PK { get; set; }

        [DynamoDBRangeKey]
        public string SK { get; set; }

        [DynamoDBProperty]
        public string EntityType { get; set; } = "SESSION";

        [DynamoDBProperty]
        public string UserId { get; set; }

        [DynamoDBProperty]
        public string SessionId { get; set; }

        [DynamoDBProperty]
        public string RefreshTokenHash { get; set; }

        [DynamoDBProperty]
        public string UserAgent { get; set; }

        [DynamoDBProperty]
        public string IpAddress { get; set; }

        [DynamoDBProperty]
        public string LastLoginAt { get; set; }

        [DynamoDBProperty]
        public string CreatedAt { get; set; }

        [DynamoDBProperty]
        public long ExpiresAt { get; set; }

        // GSI1 - User Sessions
        [DynamoDBGlobalSecondaryIndexHashKey("GSI1-UserSessions")]
        public string GSI1PK { get; set; }

        [DynamoDBGlobalSecondaryIndexRangeKey("GSI1-UserSessions")]
        public string GSI1SK { get; set; }

        // Thêm properties cho Cognito
        [DynamoDBProperty]
        public string CognitoAccessToken { get; set; }

        [DynamoDBProperty]
        public string CognitoRefreshToken { get; set; }

        [DynamoDBProperty]
        public string AuthProvider { get; set; } // "LOCAL", "GOOGLE", "COGNITO"
    }
}