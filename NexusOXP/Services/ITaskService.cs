using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Rendering;
using NexusOXP.Models;
using NexusOXP.ViewModels;

namespace NexusOXP.Services
{
    public interface ITaskService
    {
        Task<IReadOnlyList<ProjectTask>> GetTasksAsync(ClaimsPrincipal user, TaskIndexQuery query);

        Task<TaskIndexFilterOptions> GetTaskFilterOptionsAsync();

        Task<ProjectTask?> GetBoardProjectAsync(int projectId);

        Task<ProjectTask?> GetDetailsAsync(int id);

        Task<ProjectTask?> GetForEditAsync(int id);

        Task<ProjectTask?> GetForDeleteAsync(int id);

        Task<ProjectTask?> GetTrackedWithProjectAsync(int id);

        Task<Project?> GetManageableProjectAsync(int projectId, ClaimsPrincipal user);

        Task<bool> ActiveUserExistsAsync(string? userId);

        Task PopulateOptionsAsync(TaskFormViewModel model, ClaimsPrincipal user);

        Task<ProjectTask> CreateAsync(TaskFormViewModel model, Project project, string actorId, Func<int, string?> detailsUrl);

        Task UpdateAsync(TaskFormViewModel model, ProjectTask task, Project project, string? actorId, Func<int, string?> detailsUrl);

        Task DeleteAsync(ProjectTask task, string? actorId);

        Task ChangeStatusAsync(ProjectTask task, ProjectTaskStatus status, string? actorId, Func<int, string?> detailsUrl);

        bool CanView(Project project, ClaimsPrincipal user);

        bool CanView(ProjectTask task, ClaimsPrincipal user);

        bool CanManage(Project project, ClaimsPrincipal user);

        bool CanManageTasks(ClaimsPrincipal user);

        bool CanUpdateStatus(ProjectTask task, ClaimsPrincipal user);

        TaskFormViewModel ToFormModel(ProjectTask task);
    }

    public sealed record TaskIndexQuery(
        int? ProjectId,
        string? Search,
        ProjectTaskStatus? Status,
        ProjectPriority? Priority,
        string? AssignedUserId,
        string? Due);

    public sealed record TaskIndexFilterOptions(
        IReadOnlyList<SelectListItem> ProjectOptions,
        IReadOnlyList<SelectListItem> UserOptions);
}
