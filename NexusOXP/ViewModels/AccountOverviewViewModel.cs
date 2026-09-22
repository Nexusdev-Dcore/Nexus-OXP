using System.ComponentModel.DataAnnotations;
using NexusOXP.Models;

namespace NexusOXP.ViewModels
{
    public class AccountOverviewViewModel
    {
        public ProfileDetailsViewModel Profile { get; set; } = new();

        public ChangePasswordViewModel Password { get; set; } = new();

        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string ProfileImage { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public bool IsActive { get; set; }

        public IReadOnlyList<string> Roles { get; set; } = [];

        public int MyProjectsCount { get; set; }

        public int MyTasksCount { get; set; }

        public int CompletedTasksCount { get; set; }

        public int ActivityCount { get; set; }

        public IReadOnlyList<Project> MyProjects { get; set; } = [];

        public IReadOnlyList<ProjectTask> MyTasks { get; set; } = [];

        public IReadOnlyList<ProjectTask> CompletedTasks { get; set; } = [];

        public IReadOnlyList<ActivityLog> RecentActivity { get; set; } = [];
    }

    public class ProfileDetailsViewModel
    {
        [Required]
        [StringLength(80)]
        [Display(Name = "First name")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(80)]
        [Display(Name = "Last name")]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Phone]
        [Display(Name = "Phone number")]
        public string? PhoneNumber { get; set; }

        [Url]
        [Display(Name = "Profile picture URL")]
        public string? ProfileImage { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
