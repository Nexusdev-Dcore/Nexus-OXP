using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace NexusOXP.ViewModels
{
    public class TeamFormViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [Required]
        [Display(Name = "Manager")]
        public string ManagerId { get; set; } = string.Empty;

        [Display(Name = "Project")]
        public int? ProjectId { get; set; }

        public IReadOnlyList<SelectListItem> UserOptions { get; set; } = [];

        public IReadOnlyList<SelectListItem> ProjectOptions { get; set; } = [];
    }
}
