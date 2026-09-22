using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using NexusOXP.Models;

namespace NexusOXP.ViewModels
{
    public class TeamMemberFormViewModel
    {
        public int TeamId { get; set; }

        [Required]
        [Display(Name = "User")]
        public string UserId { get; set; } = string.Empty;

        public TeamMemberRole Role { get; set; } = TeamMemberRole.Member;

        public IReadOnlyList<SelectListItem> UserOptions { get; set; } = [];
    }
}
