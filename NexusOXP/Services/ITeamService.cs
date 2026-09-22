using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Rendering;
using NexusOXP.Models;
using NexusOXP.ViewModels;

namespace NexusOXP.Services
{
    public interface ITeamService
    {
        Task<IReadOnlyList<Team>> GetTeamsAsync(ClaimsPrincipal user, TeamIndexQuery query);

        Task<IReadOnlyList<SelectListItem>> GetManagerOptionsAsync();

        Task<Team?> GetDetailsAsync(int id);

        Task<Team?> GetForEditAsync(int id);

        Task<Team?> GetForDeleteAsync(int id);

        Task<Team?> GetTrackedWithMembersAsync(int id);

        Task PopulateOptionsAsync(TeamFormViewModel model);

        Task PopulateMemberOptionsAsync(TeamMemberFormViewModel model, Team team);

        Task<IReadOnlyList<SelectListItem>> GetProjectOptionsAsync(int teamId);

        Task<bool> ActiveUserExistsAsync(string userId);

        Task<Team> CreateAsync(TeamFormViewModel model, string? actorId);

        Task UpdateAsync(Team team, TeamFormViewModel model, string? actorId);

        Task DeleteAsync(Team team, string? actorId);

        Task<AddTeamMemberResult> AddMemberAsync(Team team, TeamMemberFormViewModel model, string? actorId);

        Task<RemoveTeamMemberResult> RemoveMemberAsync(Team team, int memberId, string? actorId);

        Task AssignProjectAsync(int teamId, int projectId, string? actorId);

        bool CanView(Team team, ClaimsPrincipal user);

        bool CanManage(Team team, ClaimsPrincipal user);

        bool CanManageTeams(ClaimsPrincipal user);

        TeamFormViewModel ToFormModel(Team team);
    }

    public sealed record TeamIndexQuery(string? Search, string? ManagerId);

    public enum AddTeamMemberResult
    {
        Added,
        AlreadyMember
    }

    public enum RemoveTeamMemberResult
    {
        Removed,
        NotFound,
        CannotRemoveManager
    }
}
