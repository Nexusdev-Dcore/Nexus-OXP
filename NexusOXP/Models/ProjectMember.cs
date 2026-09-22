namespace NexusOXP.Models
{
    public class ProjectMember
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }

        public Project Project { get; set; } = null!;

        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;

        public ProjectMemberRole Role { get; set; } = ProjectMemberRole.Member;

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }

    public enum ProjectMemberRole
    {
        Owner = 0,
        Manager = 1,
        Member = 2
    }
}
