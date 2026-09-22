namespace NexusOXP.Models
{
    public class Project
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string OwnerId { get; set; } = string.Empty;

        public ApplicationUser Owner { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? StartDate { get; set; }

        public DateTime? Deadline { get; set; }

        public ProjectStatus Status { get; set; } = ProjectStatus.Planning;

        public ProjectPriority Priority { get; set; } = ProjectPriority.Medium;

        public int? TeamId { get; set; }

        public Team? Team { get; set; }

        public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();

        public ICollection<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();
    }

    public enum ProjectStatus
    {
        Planning = 0,
        Active = 1,
        OnHold = 2,
        Completed = 3,
        Archived = 4
    }

    public enum ProjectPriority
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }
}
