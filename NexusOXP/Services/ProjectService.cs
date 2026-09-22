using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NexusOXP.Authorization;
using NexusOXP.Data;
using NexusOXP.Models;
using NexusOXP.ViewModels;

namespace NexusOXP.Services
{
    public class ProjectService : IProjectService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IActivityLogger _activityLogger;
        private readonly IProjectAccessService _accessService;

        public ProjectService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IActivityLogger activityLogger,
            IProjectAccessService accessService)
        {
            _context = context;
            _userManager = userManager;
            _activityLogger = activityLogger;
            _accessService = accessService;
        }

        public async Task<IReadOnlyList<Project>> GetProjectsAsync(ClaimsPrincipal user, ProjectIndexQuery query)
        {
            var projectsQuery = VisibleProjects(user)
                .Include(project => project.Owner)
                .Include(project => project.Team)
                .Include(project => project.Members)
                .AsNoTracking();

            projectsQuery = ApplyFilters(projectsQuery, query);

            return await projectsQuery
                .OrderByDescending(project => project.CreatedAt)
                .ToListAsync();
        }

        public async Task<ProjectIndexFilterOptions> GetProjectFilterOptionsAsync(ClaimsPrincipal user, ProjectIndexQuery query)
        {
            var accessibleProjects = VisibleProjects(user).AsNoTracking();
            var ownerIds = accessibleProjects.Select(project => project.OwnerId);
            var teamIds = accessibleProjects
                .Where(project => project.TeamId.HasValue)
                .Select(project => project.TeamId!.Value);

            var ownerOptions = await _context.Users
                .AsNoTracking()
                .Where(item => ownerIds.Contains(item.Id))
                .OrderBy(item => item.FirstName)
                .ThenBy(item => item.LastName)
                .Select(item => new SelectListItem($"{item.FirstName} {item.LastName}", item.Id))
                .ToListAsync();

            var teamOptions = await _context.Teams
                .AsNoTracking()
                .Where(item => teamIds.Contains(item.Id))
                .OrderBy(item => item.Name)
                .Select(item => new SelectListItem(item.Name, item.Id.ToString()))
                .ToListAsync();

            return new ProjectIndexFilterOptions(ownerOptions, teamOptions);
        }

        public async Task<Project?> GetDetailsAsync(int id)
        {
            return await _context.Projects
                .Include(item => item.Owner)
                .Include(item => item.Team)
                .Include(item => item.Members)
                    .ThenInclude(member => member.User)
                .Include(item => item.Tasks)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);
        }

        public async Task<ProjectDashboardViewModel?> GetDashboardAsync(int id)
        {
            var project = await GetDetailsAsync(id);
            if (project is null)
            {
                return null;
            }

            return new ProjectDashboardViewModel
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
        }

        public async Task<Project?> GetForEditAsync(int id)
        {
            return await _context.Projects.FindAsync(id);
        }

        public async Task<Project?> GetForDeleteAsync(int id)
        {
            return await _context.Projects
                .Include(item => item.Owner)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);
        }

        public async Task<Project> CreateAsync(ProjectFormViewModel model, string ownerId)
        {
            var project = new Project
            {
                Name = model.Name,
                Description = model.Description,
                OwnerId = ownerId,
                StartDate = model.StartDate,
                Deadline = model.Deadline,
                Status = model.Status,
                Priority = model.Priority,
                Members =
                [
                    new ProjectMember
                    {
                        UserId = ownerId,
                        Role = ProjectMemberRole.Owner
                    }
                ]
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            _activityLogger.Log(
                ownerId,
                "Create",
                "Project",
                project.Id,
                $"Created project \"{project.Name}\".",
                projectId: project.Id);
            await _context.SaveChangesAsync();

            return project;
        }

        public async Task UpdateAsync(Project project, ProjectFormViewModel model, string? actorId)
        {
            project.Name = model.Name;
            project.Description = model.Description;
            project.StartDate = model.StartDate;
            project.Deadline = model.Deadline;
            project.Status = model.Status;
            project.Priority = model.Priority;

            _activityLogger.Log(
                actorId,
                "Update",
                "Project",
                project.Id,
                $"Updated project \"{project.Name}\".",
                projectId: project.Id);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Project project, string? actorId)
        {
            _context.Projects.Remove(project);
            _activityLogger.Log(
                actorId,
                "Delete",
                "Project",
                project.Id,
                $"Deleted project \"{project.Name}\".",
                projectId: project.Id);
            await _context.SaveChangesAsync();
        }

        public bool CanView(Project project, ClaimsPrincipal user)
        {
            return _accessService.CanView(project, user);
        }

        public bool CanManage(Project project, ClaimsPrincipal user)
        {
            return _accessService.CanManage(project, user);
        }

        public ProjectFormViewModel ToFormModel(Project project)
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

        private IQueryable<Project> VisibleProjects(ClaimsPrincipal user)
        {
            var userId = _userManager.GetUserId(user);
            var query = _context.Projects.AsQueryable();

            if (user.IsInRole(AppRoles.Admin) || user.IsInRole(AppRoles.Manager))
            {
                return query;
            }

            return query.Where(project =>
                project.OwnerId == userId ||
                project.Members.Any(member => member.UserId == userId));
        }

        private static IQueryable<Project> ApplyFilters(IQueryable<Project> projectsQuery, ProjectIndexQuery query)
        {
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                projectsQuery = projectsQuery.Where(project =>
                    project.Name.Contains(search) ||
                    (project.Description != null && project.Description.Contains(search)) ||
                    project.Owner.FirstName.Contains(search) ||
                    project.Owner.LastName.Contains(search) ||
                    (project.Team != null && project.Team.Name.Contains(search)));
            }

            if (query.Status.HasValue)
            {
                projectsQuery = projectsQuery.Where(project => project.Status == query.Status.Value);
            }

            if (query.Priority.HasValue)
            {
                projectsQuery = projectsQuery.Where(project => project.Priority == query.Priority.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.OwnerId))
            {
                projectsQuery = projectsQuery.Where(project => project.OwnerId == query.OwnerId);
            }

            if (query.TeamId.HasValue)
            {
                projectsQuery = projectsQuery.Where(project => project.TeamId == query.TeamId.Value);
            }

            if (query.Overdue == true)
            {
                var today = DateTime.UtcNow.Date;
                projectsQuery = projectsQuery.Where(project =>
                    project.Deadline.HasValue &&
                    project.Deadline.Value.Date < today &&
                    project.Status != ProjectStatus.Completed &&
                    project.Status != ProjectStatus.Archived);
            }

            return projectsQuery;
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
    }
}
