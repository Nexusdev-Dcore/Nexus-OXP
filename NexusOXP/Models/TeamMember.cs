namespace NexusOXP.Models
{
    public class TeamMember
    {
        public int Id { get; set; }

        public int TeamId { get; set; }

        public Team Team { get; set; } = null!;

        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;

        public TeamMemberRole Role { get; set; } = TeamMemberRole.Member;

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }

    public enum TeamMemberRole
    {
        Manager = 0,
        Member = 1
    }
}
