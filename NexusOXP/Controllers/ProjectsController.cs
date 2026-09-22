using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NexusOXP.Authorization;
using NexusOXP.Data;
using NexusOXP.Models;
using NexusOXP.Services;
using NexusOXP.ViewModels;

namespace NexusOXP.Controllers
{
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager},{AppRoles.Member}")]
    public class ProjectsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IActivityLogger _activityLogger;

        public ProjectsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IActivityLogger activityLogger)
        {
            _context = context;
            _userManager = userManager;
            _activityLogger = activityLogger;
        }

        public async Task<IActionResult> Index(
            string? q = null,
            ProjectStatus? status = null,
            ProjectPriority? priority = null,
            string? ownerId = null,
            int? teamId = null,
            bool? overdue = null)
        {
            var userId = _userManager.GetUserId(User);
            var canManageAll = User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager);

            var projectsQuery = _context.Projects
                .Include(project => project.Owner)
                .Include(project => project.Team)
                .Include(project => project.Members)
                .AsNoTracking();

            if (!canManageAll)
            {
                projectsQuery = projectsQuery.Where(project =>
                    project.OwnerId == userId ||
                    project.Members.Any(member => member.UserId == userId));
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var query = q.Trim();
                projectsQuery = projectsQuery.Where(project =>
                    project.Name.Contains(query) ||
                    (project.Description != null && project.Description.Contains(query)) ||
                    project.Owner.FirstName.Contains(query) ||
                    project.Owner.LastName.Contains(query) ||
                    (project.Team != null && project.Team.Name.Contains(query)));
            }

            if (status.HasValue)
            {
                projectsQuery = projectsQuery.Where(project => project.Status == status.Value);
            }

            if (priority.HasValue)
            {
                projectsQuery = projectsQuery.Where(project => project.Priority == priority.Value);
            }

            if (!string.IsNullOrWhiteSpace(ownerId))
            {
                projectsQuery = projectsQuery.Where(project => project.OwnerId == ownerId);
            }

            if (teamId.HasValue)
            {
                projectsQuery = projectsQuery.Where(project => project.TeamId == teamId.Value);
            }

            if (overdue == true)
            {
                var today = DateTime.UtcNow.Date;
                projectsQuery = projectsQuery.Where(project =>
                    project.Deadline.HasValue &&
                    project.Deadline.Value.Date < today &&
                    project.Status != ProjectStatus.Completed &&
                    project.Status != ProjectStatus.Archived);
            }

            var projects = await projectsQuery
                .OrderByDescending(project => project.CreatedAt)
                .ToListAsync();

            await PopulateIndexFilters(q, status, priority, ownerId, teamId, overdue, canManageAll, userId);

            return View(projects);
        }

        public async Task<IActionResult> Details(int id)
        {
            var project = await _context.Projects
                .Include(item => item.Owner)
                .Include(item => item.Team)
                .Include(item => item.Members)
                    .ThenInclude(member => member.User)
                .Include(item => item.Tasks)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);

            if (project is null)
            {
                return NotFound();
            }

            if (!CanView(project))
            {
                return Forbid();
            }

            return View(project);
        }

        public async Task<IActionResult> Dashboard(int id)
        {
            var project = await _context.Projects
                .Include(item => item.Owner)
                .Include(item => item.Team)
                .Include(item => item.Members)
                    .ThenInclude(member => member.User)
                .Include(item => item.Tasks)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);

            if (project is null)
            {
                return NotFound();
            }

            if (!CanView(project))
            {
                return Forbid();
            }

            var model = new ProjectDashboardViewModel
            {
                Project = project,
                ProgressPercent = CalculateProgress(project),
                TotalTasks = project.Tasks.Count,
                CompletedTasks = project.Tasks.Count(task => task.Status == ProjectTaskStatus.Completed),
                InProgressTasks = project.Tasks.Count(task => task.Status == ProjectTaskStatus.InProgress),
                TodoTasks = project.Tasks.Count(task => task.Status == ProjectTaskStatus.Todo),
                RecentActivity =
                [
                    new ProjectActivityItem
                    {
                        Icon = "bi-folder-plus",
                        Title = "Project created",
                        Description = $"{project.Owner.FirstName} {project.Owner.LastName} created this project.",
                        OccurredAt = project.CreatedAt
                    },
                    new ProjectActivityItem
                    {
                        Icon = "bi-flag",
                        Title = $"Status set to {project.Status}",
                        Description = $"Priority is currently {project.Priority}.",
                        OccurredAt = project.CreatedAt
                    }
                ]
            };

            return View(model);
        }

        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public IActionResult Create()
        {
            return View(new ProjectFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> Create(ProjectFormViewModel model)
        {
            ValidateDates(model);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userId = _userManager.GetUserId(User);
            if (userId is null)
            {
                return Challenge();
            }

            var project = new Project
            {
                Name = model.Name,
                Description = model.Description,
                OwnerId = userId,
                StartDate = model.StartDate,
                Deadline = model.Deadline,
                Status = model.Status,
                Priority = model.Priority,
                Members =
                [
                    new ProjectMember
                    {
                        UserId = userId,
                        Role = ProjectMemberRole.Owner
                    }
                ]
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            _activityLogger.Log(
                userId,
                "Create",
                "Project",
                project.Id,
                $"Created project \"{project.Name}\".",
                projectId: project.Id);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Project created successfully.";
            return RedirectToAction(nameof(Details), new { id = project.Id });
        }

        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> Edit(int id)
        {
            var project = await _context.Projects.FindAsync(id);

            if (project is null)
            {
                return NotFound();
            }

            if (!CanManage(project))
            {
                return Forbid();
            }

            return View(ToFormModel(project));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> Edit(int id, ProjectFormViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            ValidateDates(model);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var project = await _context.Projects.FindAsync(id);

            if (project is null)
            {
                return NotFound();
            }

            if (!CanManage(project))
            {
                return Forbid();
            }

            project.Name = model.Name;
            project.Description = model.Description;
            project.StartDate = model.StartDate;
            project.Deadline = model.Deadline;
            project.Status = model.Status;
            project.Priority = model.Priority;

            _activityLogger.Log(
                _userManager.GetUserId(User),
                "Update",
                "Project",
                project.Id,
                $"Updated project \"{project.Name}\".",
                projectId: project.Id);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Project updated successfully.";
            return RedirectToAction(nameof(Details), new { id = project.Id });
        }

        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> Delete(int id)
        {
            var project = await _context.Projects
                .Include(item => item.Owner)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);

            if (project is null)
            {
                return NotFound();
            }

            if (!CanManage(project))
            {
                return Forbid();
            }

            return View(project);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var project = await _context.Projects.FindAsync(id);

            if (project is null)
            {
                return NotFound();
            }

            if (!CanManage(project))
            {
                return Forbid();
            }

            _context.Projects.Remove(project);
            _activityLogger.Log(
                _userManager.GetUserId(User),
                "Delete",
                "Project",
                project.Id,
                $"Deleted project \"{project.Name}\".",
                projectId: project.Id);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Project deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        private bool CanView(Project project)
        {
            var userId = _userManager.GetUserId(User);

            return User.IsInRole(AppRoles.Admin) ||
                User.IsInRole(AppRoles.Manager) ||
                project.OwnerId == userId ||
                project.Members.Any(member => member.UserId == userId);
        }

        private bool CanManage(Project project)
        {
            var userId = _userManager.GetUserId(User);

            return User.IsInRole(AppRoles.Admin) ||
                User.IsInRole(AppRoles.Manager) ||
                project.OwnerId == userId;
        }

        private static ProjectFormViewModel ToFormModel(Project project)
        {
            return new ProjectFormViewModel
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                StartDate = project.StartDate,
                Deadline = project.Deadline,
                Status = project.Status,
                Priority = project.Priority
            };
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

        private void ValidateDates(ProjectFormViewModel model)
        {
            if (ProjectPlanningValidator.HasInvalidDateRange(model.StartDate, model.Deadline))
            {
                ModelState.AddModelError(nameof(ProjectFormViewModel.Deadline), "Deadline cannot be before the start date.");
            }
        }

        private async Task PopulateIndexFilters(
            string? q,
            ProjectStatus? status,
            ProjectPriority? priority,
            string? ownerId,
            int? teamId,
            bool? overdue,
            bool canManageAll,
            string? userId)
        {
            var accessibleProjects = _context.Projects.AsNoTracking().AsQueryable();
            if (!canManageAll)
            {
                accessibleProjects = accessibleProjects.Where(project =>
                    project.OwnerId == userId ||
                    project.Members.Any(member => member.UserId == userId));
            }

            var ownerIds = accessibleProjects.Select(project => project.OwnerId);
            var teamIds = accessibleProjects
                .Where(project => project.TeamId.HasValue)
                .Select(project => project.TeamId!.Value);

            ViewBag.Query = q;
            ViewBag.Status = status;
            ViewBag.Priority = priority;
            ViewBag.OwnerId = ownerId;
            ViewBag.TeamId = teamId;
            ViewBag.Overdue = overdue == true;
            ViewBag.OwnerOptions = await _context.Users
                .AsNoTracking()
                .Where(user => ownerIds.Contains(user.Id))
                .OrderBy(user => user.FirstName)
                .ThenBy(user => user.LastName)
                .Select(user => new SelectListItem($"{user.FirstName} {user.LastName}", user.Id))
                .ToListAsync();
            ViewBag.TeamOptions = await _context.Teams
                .AsNoTracking()
                .Where(team => teamIds.Contains(team.Id))
                .OrderBy(team => team.Name)
                .Select(team => new SelectListItem(team.Name, team.Id.ToString()))
                .ToListAsync();
        }
    }
}
