using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using NexusOXP.Authorization;
using NexusOXP.Models;

namespace NexusOXP.Services
{
    public class ProjectAccessService : IProjectAccessService
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ProjectAccessService(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public bool CanManageProjects(ClaimsPrincipal user)
        {
            return user.IsInRole(AppRoles.Admin) || user.IsInRole(AppRoles.Manager);
        }

        public bool CanManageTeams(ClaimsPrincipal user)
        {
            return user.IsInRole(AppRoles.Admin) || user.IsInRole(AppRoles.Manager);
        }

        public bool CanView(Project project, ClaimsPrincipal user)
        {
            var userId = _userManager.GetUserId(user);

            return CanManage(project, user) ||
                project.OwnerId == userId ||
                project.Members.Any(member => member.UserId == userId);
        }

        public bool CanManage(Project project, ClaimsPrincipal user)
        {
            var userId = _userManager.GetUserId(user);

            return user.IsInRole(AppRoles.Admin) ||
                user.IsInRole(AppRoles.Manager) ||
                project.OwnerId == userId;
        }

        public bool CanView(ProjectTask task, ClaimsPrincipal user)
        {
            var userId = _userManager.GetUserId(user);

            return CanManage(task.Project, user) ||
                task.AssignedUserId == userId ||
                task.CreatedById == userId ||
                task.Project.OwnerId == userId ||
                task.Project.Members.Any(member => member.UserId == userId);
        }

        public bool CanUpdateStatus(ProjectTask task, ClaimsPrincipal user)
        {
            var userId = _userManager.GetUserId(user);

            return CanManage(task.Project, user) ||
                task.AssignedUserId == userId ||
                task.CreatedById == userId;
        }

        public bool CanView(Team team, ClaimsPrincipal user)
        {
            var userId = _userManager.GetUserId(user);

            return CanManage(team, user) ||
                team.ManagerId == userId ||
                team.Members.Any(member => member.UserId == userId);
        }

        public bool CanManage(Team team, ClaimsPrincipal user)
        {
            var userId = _userManager.GetUserId(user);

            return user.IsInRole(AppRoles.Admin) ||
                user.IsInRole(AppRoles.Manager) ||
                team.ManagerId == userId;
        }
    }
}
