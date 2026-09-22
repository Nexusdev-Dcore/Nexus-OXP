using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusOXP.Authorization;
using NexusOXP.Data;
using NexusOXP.Models;

namespace NexusOXP.Controllers
{
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager},{AppRoles.Member}")]
    public class ActivityLogsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ActivityLogsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? entityType = null, int? projectId = null)
        {
            var activitiesQuery = VisibleActivities()
                .Include(activity => activity.Actor)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(entityType))
            {
                activitiesQuery = activitiesQuery.Where(activity => activity.EntityType == entityType);
            }

            if (projectId.HasValue)
            {
                activitiesQuery = activitiesQuery.Where(activity => activity.ProjectId == projectId.Value);
            }

            var activities = await activitiesQuery
                .OrderByDescending(activity => activity.CreatedAt)
                .Take(150)
                .ToListAsync();

            ViewBag.EntityType = entityType;
            ViewBag.ProjectId = projectId;

            return View(activities);
        }

        private IQueryable<ActivityLog> VisibleActivities()
        {
            if (User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager))
            {
                return _context.ActivityLogs;
            }

            var userId = _userManager.GetUserId(User);

            var projectIds = _context.Projects
                .Where(project =>
                    project.OwnerId == userId ||
                    project.Members.Any(member => member.UserId == userId))
                .Select(project => project.Id);

            var taskIds = _context.Tasks
                .Where(task =>
                    task.AssignedUserId == userId ||
                    task.CreatedById == userId ||
                    task.Project.OwnerId == userId ||
                    task.Project.Members.Any(member => member.UserId == userId))
                .Select(task => task.Id);

            var teamIds = _context.Teams
                .Where(team =>
                    team.ManagerId == userId ||
                    team.Members.Any(member => member.UserId == userId))
                .Select(team => team.Id);

            return _context.ActivityLogs.Where(activity =>
                activity.ActorId == userId ||
                activity.ProjectId.HasValue && projectIds.Contains(activity.ProjectId.Value) ||
                activity.TaskId.HasValue && taskIds.Contains(activity.TaskId.Value) ||
                activity.TeamId.HasValue && teamIds.Contains(activity.TeamId.Value));
        }
    }
}
