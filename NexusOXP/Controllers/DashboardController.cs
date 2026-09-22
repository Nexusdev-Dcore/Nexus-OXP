using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusOXP.Authorization;
using NexusOXP.Data;
using NexusOXP.Models;
using NexusOXP.ViewModels;

namespace NexusOXP.Controllers
{
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager},{AppRoles.Member}")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DashboardController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            if (userId is null)
            {
                return Challenge();
            }

            var canManageAll = User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager);

            var projectsQuery = _context.Projects
                .Include(project => project.Tasks)
                .AsNoTracking();

            var tasksQuery = _context.Tasks
                .Include(task => task.Project)
                .AsNoTracking();

            var teamsQuery = _context.Teams
                .Include(team => team.Members)
                .AsNoTracking();

            var activityQuery = _context.ActivityLogs
                .Include(activity => activity.Actor)
                .AsNoTracking();

            if (!canManageAll)
            {
                projectsQuery = projectsQuery.Where(project =>
                    project.OwnerId == userId ||
                    project.Members.Any(member => member.UserId == userId));

                tasksQuery = tasksQuery.Where(task =>
                    task.AssignedUserId == userId ||
                    task.CreatedById == userId ||
                    task.Project.OwnerId == userId ||
                    task.Project.Members.Any(member => member.UserId == userId));

                teamsQuery = teamsQuery.Where(team =>
                    team.ManagerId == userId ||
                    team.Members.Any(member => member.UserId == userId));

                activityQuery = activityQuery.Where(activity =>
                    activity.ActorId == userId ||
                    activity.ProjectId.HasValue && projectsQuery.Select(project => project.Id).Contains(activity.ProjectId.Value) ||
                    activity.TaskId.HasValue && tasksQuery.Select(task => task.Id).Contains(activity.TaskId.Value) ||
                    activity.TeamId.HasValue && teamsQuery.Select(team => team.Id).Contains(activity.TeamId.Value));
            }

            var today = DateTime.UtcNow.Date;

            var recentProjects = await projectsQuery
                .OrderByDescending(project => project.CreatedAt)
                .Take(5)
                .ToListAsync();

            var recentTasks = await tasksQuery
                .Include(task => task.AssignedUser)
                .OrderByDescending(task => task.UpdatedAt)
                .Take(5)
                .ToListAsync();

            var myTasks = await tasksQuery
                .Include(task => task.AssignedUser)
                .Where(task =>
                    task.Status != ProjectTaskStatus.Completed &&
                    (task.AssignedUserId == userId || task.CreatedById == userId))
                .OrderBy(task => task.DueDate ?? DateTime.MaxValue)
                .ThenByDescending(task => task.Priority)
                .Take(6)
                .ToListAsync();

            var upcomingDeadlines = await tasksQuery
                .Include(task => task.AssignedUser)
                .Where(task =>
                    task.Status != ProjectTaskStatus.Completed &&
                    task.DueDate.HasValue &&
                    task.DueDate.Value.Date >= today)
                .OrderBy(task => task.DueDate)
                .ThenByDescending(task => task.Priority)
                .Take(6)
                .ToListAsync();

            var recentActivities = await activityQuery
                .OrderByDescending(activity => activity.CreatedAt)
                .Take(8)
                .ToListAsync();

            var recentNotifications = await _context.Notifications
                .AsNoTracking()
                .Where(notification => notification.UserId == userId)
                .OrderByDescending(notification => notification.CreatedAt)
                .Take(5)
                .ToListAsync();

            var model = new DashboardViewModel
            {
                TotalProjects = await projectsQuery.CountAsync(),
                ActiveProjects = await projectsQuery.CountAsync(project => project.Status == ProjectStatus.Active),
                TotalTasks = await tasksQuery.CountAsync(),
                ActiveTasks = await tasksQuery.CountAsync(task => task.Status != ProjectTaskStatus.Completed),
                CompletedTasks = await tasksQuery.CountAsync(task => task.Status == ProjectTaskStatus.Completed),
                OverdueTasks = await tasksQuery.CountAsync(task =>
                    task.DueDate.HasValue &&
                    task.DueDate.Value.Date < today &&
                    task.Status != ProjectTaskStatus.Completed),
                TeamsCount = await teamsQuery.CountAsync(),
                UnreadNotifications = await _context.Notifications.CountAsync(notification => notification.UserId == userId && !notification.IsRead),
                RecentProjects = recentProjects
                    .Select(project => new DashboardProjectSummary
                    {
                        Id = project.Id,
                        Name = project.Name,
                        Description = string.IsNullOrWhiteSpace(project.Description) ? "No description" : project.Description,
                        Status = project.Status,
                        Priority = project.Priority,
                        ProgressPercent = CalculateProgress(project),
                        UpdatedAt = project.Tasks.Count == 0
                            ? project.CreatedAt
                            : project.Tasks.Max(task => task.UpdatedAt)
                    })
                    .ToList(),
                MyTasks = myTasks.Select(ToTaskSummary).ToList(),
                UpcomingDeadlines = upcomingDeadlines.Select(ToTaskSummary).ToList(),
                RecentActivity = recentActivities
                    .Select(activity => new DashboardActivityItem
                    {
                        Icon = ActivityIcon(activity.EntityType, activity.Action),
                        Title = activity.Action,
                        Description = activity.Description,
                        OccurredAt = activity.CreatedAt,
                        LinkUrl = ActivityLink(activity)
                    })
                    .Concat(recentNotifications
                    .Select(notification => new DashboardActivityItem
                    {
                        Icon = NotificationIcon(notification.Type),
                        Title = notification.Title,
                        Description = notification.Message,
                        OccurredAt = notification.CreatedAt,
                        LinkUrl = notification.LinkUrl
                    }))
                    .Concat(recentTasks.Select(task => new DashboardActivityItem
                    {
                        Icon = task.Status == ProjectTaskStatus.Completed ? "bi-check-circle" : "bi-list-task",
                        Title = task.Title,
                        Description = $"{task.Project.Name} is {task.Status}.",
                        OccurredAt = task.UpdatedAt,
                        LinkUrl = Url.Action("Details", "Tasks", new { id = task.Id })
                    }))
                    .OrderByDescending(activity => activity.OccurredAt)
                    .Take(6)
                    .ToList()
            };

            return View(model);
        }

        private static int CalculateProgress(Project project)
        {
            if (project.Tasks.Count > 0)
            {
                var completedTasks = project.Tasks.Count(task => task.Status == ProjectTaskStatus.Completed);
                return (int)Math.Round(completedTasks * 100.0 / project.Tasks.Count);
            }

            return project.Status switch
            {
                ProjectStatus.Planning => 10,
                ProjectStatus.Active => 50,
                ProjectStatus.OnHold => 35,
                ProjectStatus.Completed => 100,
                ProjectStatus.Archived => 100,
                _ => 0
            };
        }

        private static string NotificationIcon(NotificationType type)
        {
            return type switch
            {
                NotificationType.TaskAssigned => "bi-person-check",
                NotificationType.TaskComment => "bi-chat-left-text",
                NotificationType.Deadline => "bi-calendar-event",
                NotificationType.TaskCompleted => "bi-check-circle",
                _ => "bi-bell"
            };
        }

        private static string ActivityIcon(string entityType, string action)
        {
            if (action.Contains("Delete", StringComparison.OrdinalIgnoreCase))
            {
                return "bi-trash";
            }

            if (action.Contains("Complete", StringComparison.OrdinalIgnoreCase))
            {
                return "bi-check-circle";
            }

            return entityType switch
            {
                "Project" => "bi-folder2-open",
                "Task" => "bi-list-task",
                "Team" => "bi-people",
                "Comment" => "bi-chat-left-text",
                _ => "bi-activity"
            };
        }

        private string? ActivityLink(ActivityLog activity)
        {
            return activity.EntityType switch
            {
                "Project" when activity.ProjectId.HasValue => Url.Action("Dashboard", "Projects", new { id = activity.ProjectId.Value }),
                "Task" when activity.TaskId.HasValue => Url.Action("Details", "Tasks", new { id = activity.TaskId.Value }),
                "Comment" when activity.TaskId.HasValue => Url.Action("Details", "Tasks", new { id = activity.TaskId.Value }),
                "Team" when activity.TeamId.HasValue => Url.Action("Details", "Teams", new { id = activity.TeamId.Value }),
                _ => null
            };
        }

        private static DashboardTaskSummary ToTaskSummary(ProjectTask task)
        {
            return new DashboardTaskSummary
            {
                Id = task.Id,
                Title = task.Title,
                ProjectName = task.Project.Name,
                AssignedName = task.AssignedUser is null
                    ? "Unassigned"
                    : $"{task.AssignedUser.FirstName} {task.AssignedUser.LastName}",
                Status = task.Status,
                Priority = task.Priority,
                DueDate = task.DueDate,
                IsOverdue = task.DueDate.HasValue &&
                    task.DueDate.Value.Date < DateTime.UtcNow.Date &&
                    task.Status != ProjectTaskStatus.Completed
            };
        }
    }
}
