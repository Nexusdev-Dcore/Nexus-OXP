using NexusOXP.Models;

namespace NexusOXP.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalProjects { get; set; }

        public int ActiveProjects { get; set; }

        public int TotalTasks { get; set; }

        public int ActiveTasks { get; set; }

        public int CompletedTasks { get; set; }

        public int OverdueTasks { get; set; }

        public int TeamsCount { get; set; }

        public int UnreadNotifications { get; set; }

        public IReadOnlyList<DashboardActivityItem> RecentActivity { get; set; } = [];

        public IReadOnlyList<DashboardProjectSummary> RecentProjects { get; set; } = [];

        public IReadOnlyList<DashboardTaskSummary> MyTasks { get; set; } = [];

        public IReadOnlyList<DashboardTaskSummary> UpcomingDeadlines { get; set; } = [];
    }

    public class DashboardActivityItem
    {
        public string Icon { get; set; } = "bi-info-circle";

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime OccurredAt { get; set; }

        public string? LinkUrl { get; set; }
    }

    public class DashboardProjectSummary
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public ProjectStatus Status { get; set; }

        public ProjectPriority Priority { get; set; }

        public int ProgressPercent { get; set; }

        public DateTime UpdatedAt { get; set; }
    }

    public class DashboardTaskSummary
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string ProjectName { get; set; } = string.Empty;

        public string AssignedName { get; set; } = string.Empty;

        public ProjectTaskStatus Status { get; set; }

        public ProjectPriority Priority { get; set; }

        public DateTime? DueDate { get; set; }

        public bool IsOverdue { get; set; }
    }
}
