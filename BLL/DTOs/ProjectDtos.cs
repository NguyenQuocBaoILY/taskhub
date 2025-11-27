using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs
{
    internal class ProjectDtos
    {
    }

    public class CreateProjectRequest
    {
        // optional: FE can provide or leave null to generate server-side
        public string ProjectId { get; set; }

        [Required]
        public string OrgId { get; set; }

        [Required]
        public string Name { get; set; }

        public string Key { get; set; }
        public string Description { get; set; }

        // PRIVATE / PUBLIC / INTERNAL
        public string Visibility { get; set; } = "PRIVATE";

        // either ManagerUserId (preferred) or ManagerUsername (controller will resolve)
        public string ManagerUserId { get; set; }
        public string ManagerUsername { get; set; }

        // role to assign to manager if provided
        public string ManagerRole { get; set; } = "PROJECT_OWNER";

        // list of initial members (use existing AddProjectMemberRequest DTO with UserId + Role)
        public List<AddProjectMemberRequest> Members { get; set; } = new List<AddProjectMemberRequest>();

        // optional metadata
        public string Priority { get; set; }
        public string Status { get; set; } = "ACTIVE";
        public string StartDate { get; set; }
        public string EndDate { get; set; }
    }
}
