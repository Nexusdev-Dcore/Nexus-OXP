namespace NexusOXP.Models
{
    public class TaskAttachment
    {
        public int Id { get; set; }

        public int TaskId { get; set; }

        public ProjectTask Task { get; set; } = null!;

        public string UploadedById { get; set; } = string.Empty;

        public ApplicationUser UploadedBy { get; set; } = null!;

        public string FileName { get; set; } = string.Empty;

        public string StoredFileName { get; set; } = string.Empty;

        public string ContentType { get; set; } = "application/octet-stream";

        public long FileSize { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
