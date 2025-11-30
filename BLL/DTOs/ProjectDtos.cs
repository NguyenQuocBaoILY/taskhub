using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs
{
    internal class ProjectDtos
    {
    }

    public class CreateProjectRequest
    {
        // optional: FE có thể gửi hoặc để null, server tự generate
        public string ProjectId { get; set; }

        // Tên project (bắt buộc)
        [Required]
        public string Name { get; set; }

        public string Description { get; set; }

        public string ManagerUserId { get; set; }
        public string ManagerUsername { get; set; }

        // Role gán cho manager trong bảng ProjectMember (nếu có)
        public string ManagerRole { get; set; } = "PROJECT_OWNER";

        // Danh sách member khởi tạo
        public List<AddProjectMemberRequest> Members { get; set; } = new List<AddProjectMemberRequest>();

        // yyyy-MM-dd hoặc ISO string (tùy convention)
        public string StartDate { get; set; }
        public string EndDate { get; set; }
    }
}
