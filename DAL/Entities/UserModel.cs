using Amazon.DynamoDBv2.DataModel;
using System;
using System.Collections.Generic;

namespace DAL.Entities
{
    [DynamoDBTable("TaskHub-Core")]
    public class UserModel
    {
        [DynamoDBHashKey]
        public string PK { get; set; }

        [DynamoDBRangeKey]
        public string SK { get; set; }

        [DynamoDBProperty]
        public string EntityType { get; set; } = "USER";

        [DynamoDBProperty]
        public string UserId { get; set; }

        [DynamoDBProperty]
        public string Username { get; set; }

        [DynamoDBProperty]
        public string LowerUsername { get; set; }

        [DynamoDBProperty]
        public string Email { get; set; }

        [DynamoDBProperty]
        public string NormalizedEmail { get; set; }

        [DynamoDBProperty]
        public string PasswordHash { get; set; }

        [DynamoDBProperty]
        public string AuthProvider { get; set; } = "LOCAL";

        [DynamoDBProperty]
        public string Name { get; set; }

        [DynamoDBProperty]
        public string Avatar { get; set; }

        [DynamoDBProperty]
        public string Status { get; set; } = "PENDING_VERIFICATION";

        [DynamoDBProperty]
        public bool EmailVerified { get; set; } = false;

        [DynamoDBProperty]
        public string PhoneNumber { get; set; }

        [DynamoDBProperty]
        public bool PhoneVerified { get; set; } = false;

        [DynamoDBProperty]
        public string Timezone { get; set; }

        [DynamoDBProperty]
        public string Language { get; set; } = "en";

        [DynamoDBProperty]
        public bool MfaEnabled { get; set; } = false;

        [DynamoDBProperty]
        public int FailedLoginAttempts { get; set; } = 0;

        [DynamoDBProperty]
        public string LockedUntil { get; set; }

        [DynamoDBProperty]
        public string LastPasswordChangedAt { get; set; }

        [DynamoDBProperty]
        public string LastLoginIp { get; set; }

        [DynamoDBProperty]
        public string LastUserAgent { get; set; }

        [DynamoDBProperty]
        public string CreatedAt { get; set; }

        [DynamoDBProperty]
        public string UpdatedAt { get; set; }

        [DynamoDBProperty]
        public string LastActiveAt { get; set; }

        [DynamoDBProperty]
        public string PasswordResetToken { get; set; }

        [DynamoDBProperty]
        public string PasswordResetTokenExpiry { get; set; }

        [DynamoDBProperty]
        public bool IsAdmin { get; set; }

        // GSI1 - Email Lookup
        [DynamoDBGlobalSecondaryIndexHashKey("GSI1-EmailLookup")]
        public string GSI1PK { get; set; }

        [DynamoDBGlobalSecondaryIndexRangeKey("GSI1-EmailLookup")]
        public string GSI1SK { get; set; }

        // GSI6 - Username Lookup
        [DynamoDBGlobalSecondaryIndexHashKey("GSI6-UsernameLookup")]
        public string GSI6PK { get; set; }

        [DynamoDBGlobalSecondaryIndexRangeKey("GSI6-UsernameLookup")]
        public string GSI6SK { get; set; }
    }
}