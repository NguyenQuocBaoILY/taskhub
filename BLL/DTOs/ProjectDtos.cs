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
        // Tên project (bắt buộc)
        [Required]
        public string NameProject { get; set; }

        public string DescriptionP { get; set; }

        // yyyy-MM-dd hoặc ISO string (tùy convention)
        public string StartDate { get; set; }
        public string EndDate { get; set; }
    }
}
