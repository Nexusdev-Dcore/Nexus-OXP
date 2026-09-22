namespace NexusOXP.Models
{
    public class ActivityLog
    {
        public int Id { get; set; }

        public string? ActorId { get; set; }

        public ApplicationUser? Actor { get; set; }

        public string Action { get; set; } = string.Empty;

        public string EntityType { get; set; } = string.Empty;

        public int? EntityId { get; set; }

        public int? ProjectId { get; set; }

        public int? TaskId { get; set; }

        public int? TeamId { get; set; }

        public string Description { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
