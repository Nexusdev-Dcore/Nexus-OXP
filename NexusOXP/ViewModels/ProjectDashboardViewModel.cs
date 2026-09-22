using NexusOXP.Models;

namespace NexusOXP.ViewModels
{
    public class ProjectDashboardViewModel
    {
        public Project Project { get; set; } = null!;

        public int ProgressPercent { get; set; }

        public int TotalTasks { get; set; }

        public int CompletedTasks { get; set; }

        public int InProgressTasks { get; set; }

        public int TodoTasks { get; set; }

        public IReadOnlyList<ProjectActivityItem> RecentActivity { get; set; } = [];
    }

    public class ProjectActivityItem
    {
        public string Icon { get; set; } = "bi-info-circle";

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime OccurredAt { get; set; }
    }
}
