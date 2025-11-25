using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs
{
    public class AddProjectMemberRequest
    {
        [Required]
        public string UserId { get; set; }

        [Required]
        public string Role { get; set; }
    }

    public class UpdateProjectMemberRequest
    {
        public string Role { get; set; }
        public string MemberStatus { get; set; }
    }

    public class ProjectMemberResponse
    {
        public string ProjectId { get; set; }
        public string UserId { get; set; }
        public string Role { get; set; }
        public List<string> Permissions { get; set; }
        public string MemberStatus { get; set; }
        public string JoinedAt { get; set; }
    }

    public class UserProjectResponse
    {
        public string ProjectId { get; set; }
        public string Role { get; set; }
        public List<string> Permissions { get; set; }
        public string JoinedAt { get; set; }
    }
}