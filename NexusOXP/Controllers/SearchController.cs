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
    public class SearchController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SearchController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? q = null)
        {
            var query = q?.Trim() ?? string.Empty;
            var model = new SearchViewModel { Query = query };

            if (string.IsNullOrWhiteSpace(query))
            {
                return View(model);
            }

            model.Projects = await VisibleProjects()
                .Include(project => project.Owner)
                .Include(project => project.Team)
                .AsNoTracking()
                .Where(project =>
                    project.Name.Contains(query) ||
                    (project.Description != null && project.Description.Contains(query)) ||
                    project.Owner.FirstName.Contains(query) ||
                    project.Owner.LastName.Contains(query) ||
                    (project.Team != null && project.Team.Name.Contains(query)))
                .OrderByDescending(project => project.CreatedAt)
                .Take(10)
                .ToListAsync();

            model.Tasks = await VisibleTasks()
                .Include(task => task.Project)
                .Include(task => task.AssignedUser)
                .AsNoTracking()
                .Where(task =>
                    task.Title.Contains(query) ||
                    (task.Description != null && task.Description.Contains(query)) ||
                    task.Project.Name.Contains(query) ||
                    (task.AssignedUser != null &&
                        (task.AssignedUser.FirstName.Contains(query) ||
                         task.AssignedUser.LastName.Contains(query) ||
                         (task.AssignedUser.Email != null && task.AssignedUser.Email.Contains(query)))))
                .OrderByDescending(task => task.UpdatedAt)
                .Take(10)
                .ToListAsync();

            model.Teams = await VisibleTeams()
                .Include(team => team.Manager)
                .Include(team => team.Members)
                .Include(team => team.Projects)
                .AsNoTracking()
                .Where(team =>
                    team.Name.Contains(query) ||
                    (team.Description != null && team.Description.Contains(query)) ||
                    team.Manager.FirstName.Contains(query) ||
                    team.Manager.LastName.Contains(query))
                .OrderBy(team => team.Name)
                .Take(10)
                .ToListAsync();

            model.Users = await VisibleUsers(query)
                .OrderBy(user => user.FirstName)
                .ThenBy(user => user.LastName)
                .Take(10)
                .ToListAsync();

            return View(model);
        }

        private IQueryable<Project> VisibleProjects()
        {
            var userId = _userManager.GetUserId(User);
            var projects = _context.Projects.AsQueryable();

            if (CanManageAll())
            {
                return projects;
            }

            return projects.Where(project =>
                project.OwnerId == userId ||
                project.Members.Any(member => member.UserId == userId));
        }

        private IQueryable<ProjectTask> VisibleTasks()
        {
            var userId = _userManager.GetUserId(User);
            var tasks = _context.Tasks.AsQueryable();

            if (CanManageAll())
            {
                return tasks;
            }

            return tasks.Where(task =>
                task.AssignedUserId == userId ||
                task.CreatedById == userId ||
                task.Project.OwnerId == userId ||
                task.Project.Members.Any(member => member.UserId == userId));
        }

        private IQueryable<Team> VisibleTeams()
        {
            var userId = _userManager.GetUserId(User);
            var teams = _context.Teams.AsQueryable();

            if (CanManageAll())
            {
                return teams;
            }

            return teams.Where(team =>
                team.ManagerId == userId ||
                team.Members.Any(member => member.UserId == userId));
        }

        private IQueryable<ApplicationUser> VisibleUsers(string query)
        {
            var users = _context.Users
                .AsNoTracking()
                .Where(user =>
                    user.IsActive &&
                    (user.FirstName.Contains(query) ||
                     user.LastName.Contains(query) ||
                     (user.Email != null && user.Email.Contains(query))));

            if (CanManageAll())
            {
                return users;
            }

            var userId = _userManager.GetUserId(User);
            var visibleUserIds = _context.ProjectMembers
                .Where(member =>
                    member.UserId == userId ||
                    member.Project.OwnerId == userId ||
                    member.Project.Members.Any(projectMember => projectMember.UserId == userId))
                .Select(member => member.UserId)
                .Concat(_context.TeamMembers
                    .Where(member =>
                        member.UserId == userId ||
                        member.Team.ManagerId == userId ||
                        member.Team.Members.Any(teamMember => teamMember.UserId == userId))
                    .Select(member => member.UserId));

            return users.Where(user => visibleUserIds.Contains(user.Id) || user.Id == userId);
        }

        private bool CanManageAll()
        {
            return User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager);
        }
    }
}
