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
    public class TeamService : ITeamService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IActivityLogger _activityLogger;
        private readonly IProjectAccessService _accessService;

        public TeamService(
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

        public async Task<IReadOnlyList<Team>> GetTeamsAsync(ClaimsPrincipal user, TeamIndexQuery query)
        {
            var teamsQuery = VisibleTeams(user)
                .Include(team => team.Manager)
                .Include(team => team.Members)
                .Include(team => team.Projects)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                teamsQuery = teamsQuery.Where(team =>
                    team.Name.Contains(search) ||
                    (team.Description != null && team.Description.Contains(search)) ||
                    team.Manager.FirstName.Contains(search) ||
                    team.Manager.LastName.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(query.ManagerId))
            {
                teamsQuery = teamsQuery.Where(team => team.ManagerId == query.ManagerId);
            }

            return await teamsQuery
                .OrderBy(team => team.Name)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<SelectListItem>> GetManagerOptionsAsync()
        {
            return await _context.Teams
                .Include(team => team.Manager)
                .AsNoTracking()
                .Select(team => team.Manager)
                .Distinct()
                .OrderBy(user => user.FirstName)
                .ThenBy(user => user.LastName)
                .Select(user => new SelectListItem($"{user.FirstName} {user.LastName}", user.Id))
                .ToListAsync();
        }

        public async Task<Team?> GetDetailsAsync(int id)
        {
            return await _context.Teams
                .Include(item => item.Manager)
                .Include(item => item.Members)
                    .ThenInclude(member => member.User)
                .Include(item => item.Projects)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);
        }

        public async Task<Team?> GetForEditAsync(int id)
        {
            return await _context.Teams
                .Include(item => item.Members)
                .Include(item => item.Projects)
                .FirstOrDefaultAsync(item => item.Id == id);
        }

        public async Task<Team?> GetForDeleteAsync(int id)
        {
            return await _context.Teams
                .Include(item => item.Manager)
                .Include(item => item.Members)
                .Include(item => item.Projects)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);
        }

        public async Task<Team?> GetTrackedWithMembersAsync(int id)
        {
            return await _context.Teams
                .Include(item => item.Members)
                .FirstOrDefaultAsync(item => item.Id == id);
        }

        public async Task PopulateOptionsAsync(TeamFormViewModel model)
        {
            model.UserOptions = await UserOptionsAsync();
            model.ProjectOptions = await GetProjectOptionsAsync(model.Id);
        }

        public async Task PopulateMemberOptionsAsync(TeamMemberFormViewModel model, Team team)
        {
            var assignedIds = team.Members.Select(member => member.UserId).ToHashSet();
            var users = await _context.Users
                .AsNoTracking()
                .Where(user => user.IsActive && !assignedIds.Contains(user.Id))
                .OrderBy(user => user.FirstName)
                .ThenBy(user => user.LastName)
                .ToListAsync();

            model.UserOptions = users
                .Select(user => new SelectListItem($"{user.FirstName} {user.LastName} ({user.Email})", user.Id))
                .ToList();
        }

        public async Task<IReadOnlyList<SelectListItem>> GetProjectOptionsAsync(int teamId)
        {
            var projects = await _context.Projects
                .AsNoTracking()
                .Where(project => project.TeamId == null || project.TeamId == teamId)
                .OrderBy(project => project.Name)
                .ToListAsync();

            return projects
                .Select(project => new SelectListItem(project.Name, project.Id.ToString()))
                .ToList();
        }

        public async Task<bool> ActiveUserExistsAsync(string userId)
        {
            return !string.IsNullOrWhiteSpace(userId) &&
                await _context.Users.AnyAsync(user => user.Id == userId && user.IsActive);
        }

        public async Task<Team> CreateAsync(TeamFormViewModel model, string? actorId)
        {
            var team = new Team
            {
                Name = model.Name,
                Description = model.Description,
                ManagerId = model.ManagerId,
                Members =
                [
                    new TeamMember
                    {
                        UserId = model.ManagerId,
                        Role = TeamMemberRole.Manager
                    }
                ]
            };

            _context.Teams.Add(team);
            await _context.SaveChangesAsync();

            _activityLogger.Log(
                actorId,
                "Create",
                "Team",
                team.Id,
                $"Created team \"{team.Name}\".",
                teamId: team.Id);

            if (model.ProjectId.HasValue)
            {
                await AssignProjectAsync(team.Id, model.ProjectId.Value, actorId);
            }
            else
            {
                await _context.SaveChangesAsync();
            }

            return team;
        }

        public async Task UpdateAsync(Team team, TeamFormViewModel model, string? actorId)
        {
            team.Name = model.Name;
            team.Description = model.Description;
            team.ManagerId = model.ManagerId;

            var managerMember = team.Members.FirstOrDefault(member => member.UserId == model.ManagerId);
            if (managerMember is null)
            {
                team.Members.Add(new TeamMember
                {
                    UserId = model.ManagerId,
                    Role = TeamMemberRole.Manager
                });
            }
            else
            {
                managerMember.Role = TeamMemberRole.Manager;
            }

            _activityLogger.Log(
                actorId,
                "Update",
                "Team",
                team.Id,
                $"Updated team \"{team.Name}\".",
                teamId: team.Id);

            await _context.SaveChangesAsync();

            if (model.ProjectId.HasValue)
            {
                await AssignProjectAsync(team.Id, model.ProjectId.Value, actorId);
            }
        }

        public async Task DeleteAsync(Team team, string? actorId)
        {
            foreach (var project in team.Projects)
            {
                project.TeamId = null;
            }

            _context.Teams.Remove(team);
            _activityLogger.Log(
                actorId,
                "Delete",
                "Team",
                team.Id,
                $"Deleted team \"{team.Name}\".",
                teamId: team.Id);
            await _context.SaveChangesAsync();
        }

        public async Task<AddTeamMemberResult> AddMemberAsync(Team team, TeamMemberFormViewModel model, string? actorId)
        {
            if (team.Members.Any(member => member.UserId == model.UserId))
            {
                return AddTeamMemberResult.AlreadyMember;
            }

            team.Members.Add(new TeamMember
            {
                UserId = model.UserId,
                Role = model.Role
            });

            _activityLogger.Log(
                actorId,
                "AddMember",
                "Team",
                team.Id,
                $"Added a member to team \"{team.Name}\".",
                teamId: team.Id);

            await _context.SaveChangesAsync();
            return AddTeamMemberResult.Added;
        }

        public async Task<RemoveTeamMemberResult> RemoveMemberAsync(Team team, int memberId, string? actorId)
        {
            var member = team.Members.FirstOrDefault(item => item.Id == memberId);
            if (member is null)
            {
                return RemoveTeamMemberResult.NotFound;
            }

            if (member.UserId == team.ManagerId)
            {
                return RemoveTeamMemberResult.CannotRemoveManager;
            }

            _context.TeamMembers.Remove(member);
            _activityLogger.Log(
                actorId,
                "RemoveMember",
                "Team",
                team.Id,
                $"Removed a member from team \"{team.Name}\".",
                teamId: team.Id);
            await _context.SaveChangesAsync();

            return RemoveTeamMemberResult.Removed;
        }

        public async Task AssignProjectAsync(int teamId, int projectId, string? actorId)
        {
            var project = await _context.Projects.FindAsync(projectId);
            if (project is null)
            {
                return;
            }

            project.TeamId = teamId;
            _activityLogger.Log(
                actorId,
                "AssignProject",
                "Team",
                teamId,
                $"Assigned project \"{project.Name}\" to a team.",
                projectId: project.Id,
                teamId: teamId);
            await _context.SaveChangesAsync();
        }

        public bool CanView(Team team, ClaimsPrincipal user)
        {
            return _accessService.CanView(team, user);
        }

        public bool CanManage(Team team, ClaimsPrincipal user)
        {
            return _accessService.CanManage(team, user);
        }

        public bool CanManageTeams(ClaimsPrincipal user)
        {
            return user.IsInRole(AppRoles.Admin) || user.IsInRole(AppRoles.Manager);
        }

        public TeamFormViewModel ToFormModel(Team team)
        {
            var model = new TeamFormViewModel
            {
                Id = team.Id,
                Name = team.Name,
                Description = team.Description,
                ManagerId = team.ManagerId,
                ProjectId = team.Projects.Select(project => project.Id).FirstOrDefault()
            };

            if (model.ProjectId == 0)
            {
                model.ProjectId = null;
            }

            return model;
        }

        private IQueryable<Team> VisibleTeams(ClaimsPrincipal user)
        {
            var query = _context.Teams.AsQueryable();
            if (CanManageTeams(user))
            {
                return query;
            }

            var userId = _userManager.GetUserId(user);
            return query.Where(team =>
                team.ManagerId == userId ||
                team.Members.Any(member => member.UserId == userId));
        }

        private async Task<IReadOnlyList<SelectListItem>> UserOptionsAsync()
        {
            var users = await _context.Users
                .AsNoTracking()
                .Where(user => user.IsActive)
                .OrderBy(user => user.FirstName)
                .ThenBy(user => user.LastName)
                .ToListAsync();

            return users
                .Select(user => new SelectListItem($"{user.FirstName} {user.LastName} ({user.Email})", user.Id))
                .ToList();
        }
    }
}
