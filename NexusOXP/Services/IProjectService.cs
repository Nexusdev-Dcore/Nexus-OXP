using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Rendering;
using NexusOXP.Models;
using NexusOXP.ViewModels;

namespace NexusOXP.Services
{
    public interface IProjectService
    {
        Task<IReadOnlyList<Project>> GetProjectsAsync(ClaimsPrincipal user, ProjectIndexQuery query);

        Task<ProjectIndexFilterOptions> GetProjectFilterOptionsAsync(ClaimsPrincipal user, ProjectIndexQuery query);

        Task<Project?> GetDetailsAsync(int id);

        Task<ProjectDashboardViewModel?> GetDashboardAsync(int id);

        Task<Project?> GetForEditAsync(int id);

        Task<Project?> GetForDeleteAsync(int id);

        Task<Project> CreateAsync(ProjectFormViewModel model, string ownerId);

        Task UpdateAsync(Project project, ProjectFormViewModel model, string? actorId);

        Task DeleteAsync(Project project, string? actorId);

        bool CanView(Project project, ClaimsPrincipal user);

        bool CanManage(Project project, ClaimsPrincipal user);

        ProjectFormViewModel ToFormModel(Project project);
    }

    public sealed record ProjectIndexQuery(
        string? Search,
        ProjectStatus? Status,
        ProjectPriority? Priority,
        string? OwnerId,
        int? TeamId,
        bool? Overdue);

    public sealed record ProjectIndexFilterOptions(
        IReadOnlyList<SelectListItem> OwnerOptions,
        IReadOnlyList<SelectListItem> TeamOptions);
}
