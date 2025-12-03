using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BLL.DTOs
{
    public class CreateProjectRequest
    {
        [Required]
        public string NameProject { get; set; }

        public string DescriptionP { get; set; }

        public string StartDate { get; set; }
        public string EndDate { get; set; }
    }
}
