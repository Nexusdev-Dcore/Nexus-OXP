using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using NexusOXP.Models;

namespace NexusOXP.ViewModels
{
    public class TaskFormViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(180)]
        public string Title { get; set; } = string.Empty;

        [StringLength(3000)]
        public string? Description { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Select a project.")]
        [Display(Name = "Project")]
        public int ProjectId { get; set; }

        [Display(Name = "Assigned User")]
        public string? AssignedUserId { get; set; }

        public ProjectTaskStatus Status { get; set; } = ProjectTaskStatus.Todo;

        public ProjectPriority Priority { get; set; } = ProjectPriority.Medium;

        [DataType(DataType.Date)]
        [Display(Name = "Due Date")]
        public DateTime? DueDate { get; set; }

        public IReadOnlyList<SelectListItem> ProjectOptions { get; set; } = [];

        public IReadOnlyList<SelectListItem> UserOptions { get; set; } = [];
    }
}
