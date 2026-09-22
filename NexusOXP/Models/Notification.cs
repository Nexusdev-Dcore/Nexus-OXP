namespace NexusOXP.Models
{
    public class Notification
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string? LinkUrl { get; set; }

        public NotificationType Type { get; set; } = NotificationType.General;

        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReadAt { get; set; }
    }

    public enum NotificationType
    {
        General = 0,
        TaskAssigned = 1,
        TaskComment = 2,
        Deadline = 3,
        TaskCompleted = 4
    }
}
