namespace NexusOXP.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int UsersCount { get; set; }

        public int ActiveUsersCount { get; set; }

        public int ProjectsCount { get; set; }

        public int TeamsCount { get; set; }

        public int TasksCount { get; set; }

        public int AttachmentsCount { get; set; }

        public IReadOnlyList<AdminUserSummary> RecentUsers { get; set; } = [];

        public IReadOnlyList<AdminActivitySummary> RecentActivity { get; set; } = [];
    }

    public class AdminUserSummary
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class AdminActivitySummary
    {
        public string Action { get; set; } = string.Empty;

        public string EntityType { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}
