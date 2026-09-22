using NexusOXP.Models;

namespace NexusOXP.ViewModels
{
    public class SearchViewModel
    {
        public string Query { get; set; } = string.Empty;

        public IReadOnlyList<Project> Projects { get; set; } = [];

        public IReadOnlyList<ProjectTask> Tasks { get; set; } = [];

        public IReadOnlyList<Team> Teams { get; set; } = [];

        public IReadOnlyList<ApplicationUser> Users { get; set; } = [];

        public bool HasSearched => !string.IsNullOrWhiteSpace(Query);

        public int TotalResults => Projects.Count + Tasks.Count + Teams.Count + Users.Count;
    }
}
