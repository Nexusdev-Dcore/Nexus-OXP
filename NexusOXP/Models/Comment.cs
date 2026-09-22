namespace NexusOXP.Models
{
    public class Comment
    {
        public int Id { get; set; }

        public int TaskId { get; set; }

        public ProjectTask Task { get; set; } = null!;

        public string AuthorId { get; set; } = string.Empty;

        public ApplicationUser Author { get; set; } = null!;

        public string Content { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
