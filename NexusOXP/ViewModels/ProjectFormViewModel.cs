using System.ComponentModel.DataAnnotations;
using NexusOXP.Models;

namespace NexusOXP.ViewModels
{
    public class ProjectFormViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? Deadline { get; set; }

        public ProjectStatus Status { get; set; } = ProjectStatus.Planning;

        public ProjectPriority Priority { get; set; } = ProjectPriority.Medium;
    }
}
