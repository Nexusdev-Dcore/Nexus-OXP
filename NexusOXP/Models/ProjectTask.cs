namespace NexusOXP.Models
{
    public class ProjectTask
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public int ProjectId { get; set; }

        public Project Project { get; set; } = null!;

        public string? AssignedUserId { get; set; }

        public ApplicationUser? AssignedUser { get; set; }

        public ProjectTaskStatus Status { get; set; } = ProjectTaskStatus.Todo;

        public ProjectPriority Priority { get; set; } = ProjectPriority.Medium;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? DueDate { get; set; }

        public string CreatedById { get; set; } = string.Empty;

        public ApplicationUser CreatedBy { get; set; } = null!;

        public ICollection<Comment> Comments { get; set; } = new List<Comment>();

        public ICollection<TaskAttachment> Attachments { get; set; } = new List<TaskAttachment>();
    }

    public enum ProjectTaskStatus
    {
        Todo = 0,
        InProgress = 1,
        Review = 2,
        Completed = 3
    }
}
