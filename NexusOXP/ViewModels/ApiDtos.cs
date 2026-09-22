using System.ComponentModel.DataAnnotations;
using NexusOXP.Models;

namespace NexusOXP.ViewModels
{
    public record ProjectDto(
        int Id,
        string Name,
        string? Description,
        string OwnerId,
        string OwnerName,
        DateTime CreatedAt,
        DateTime? StartDate,
        DateTime? Deadline,
        ProjectStatus Status,
        ProjectPriority Priority,
        int? TeamId,
        string? TeamName,
        int TaskCount);

    public class ProjectCreateDto
    {
        [Required]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? Deadline { get; set; }

        public ProjectStatus Status { get; set; } = ProjectStatus.Planning;

        public ProjectPriority Priority { get; set; } = ProjectPriority.Medium;
    }

    public class ProjectUpdateDto : ProjectCreateDto
    {
    }

    public record TaskDto(
        int Id,
        string Title,
        string? Description,
        int ProjectId,
        string ProjectName,
        string? AssignedUserId,
        string? AssignedUserName,
        ProjectTaskStatus Status,
        ProjectPriority Priority,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        DateTime? DueDate,
        int AttachmentCount);

    public class TaskCreateDto
    {
        [Required]
        [StringLength(180)]
        public string Title { get; set; } = string.Empty;

        [StringLength(3000)]
        public string? Description { get; set; }

        [Range(1, int.MaxValue)]
        public int ProjectId { get; set; }

        public string? AssignedUserId { get; set; }

        public ProjectTaskStatus Status { get; set; } = ProjectTaskStatus.Todo;

        public ProjectPriority Priority { get; set; } = ProjectPriority.Medium;

        public DateTime? DueDate { get; set; }
    }

    public class TaskUpdateDto : TaskCreateDto
    {
    }

    public record UserDto(
        string Id,
        string Name,
        string? Email,
        bool IsActive,
        DateTime CreatedAt);

    public record TeamDto(
        int Id,
        string Name,
        string? Description,
        string ManagerId,
        string ManagerName,
        int MemberCount,
        int ProjectCount);
}
