namespace NexusOXP.Models
{
    public class Team
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string ManagerId { get; set; } = string.Empty;

        public ApplicationUser Manager { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();

        public ICollection<Project> Projects { get; set; } = new List<Project>();
    }
}
