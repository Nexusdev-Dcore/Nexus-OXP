using System.Security.Claims;
using NexusOXP.Models;

namespace NexusOXP.Services
{
    public interface IProjectAccessService
    {
        bool CanManageProjects(ClaimsPrincipal user);

        bool CanManageTeams(ClaimsPrincipal user);

        bool CanView(Project project, ClaimsPrincipal user);

        bool CanManage(Project project, ClaimsPrincipal user);

        bool CanView(ProjectTask task, ClaimsPrincipal user);

        bool CanUpdateStatus(ProjectTask task, ClaimsPrincipal user);

        bool CanView(Team team, ClaimsPrincipal user);

        bool CanManage(Team team, ClaimsPrincipal user);
    }
}
