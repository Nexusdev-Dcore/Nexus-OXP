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
    public class TaskService : ITaskService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IActivityLogger _activityLogger;
        private readonly IProjectAccessService _accessService;

        public TaskService(
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

        public async Task<IReadOnlyList<ProjectTask>> GetTasksAsync(ClaimsPrincipal user, TaskIndexQuery query)
        {
            var tasksQuery = VisibleTasks(user)
                .Include(task => task.Project)
                .Include(task => task.AssignedUser)
                .AsNoTracking();

            tasksQuery = ApplyFilters(tasksQuery, query);

            return await tasksQuery
                .OrderBy(task => task.Status)
                .ThenByDescending(task => task.Priority)
                .ThenBy(task => task.DueDate)
                .ToListAsync();
        }

        public async Task<TaskIndexFilterOptions> GetTaskFilterOptionsAsync()
        {
            var projectOptions = await _context.Projects
                .AsNoTracking()
                .OrderBy(project => project.Name)
                .Select(project => new SelectListItem(project.Name, project.Id.ToString()))
                .ToListAsync();

            var userOptions = await _context.Users
                .AsNoTracking()
                .Where(user => user.IsActive)
                .OrderBy(user => user.FirstName)
                .ThenBy(user => user.LastName)
                .Select(user => new SelectListItem($"{user.FirstName} {user.LastName} ({user.Email})", user.Id))
                .ToListAsync();

            return new TaskIndexFilterOptions(projectOptions, userOptions);
        }

        public async Task<ProjectTask?> GetBoardProjectAsync(int projectId)
        {
            var project = await _context.Projects
                .Include(item => item.Members)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == projectId);

            if (project is null)
            {
                return null;
            }

            return new ProjectTask { Project = project, ProjectId = project.Id };
        }

        public async Task<ProjectTask?> GetDetailsAsync(int id)
        {
            return await _context.Tasks
                .Include(item => item.Project)
                    .ThenInclude(project => project.Members)
                .Include(item => item.AssignedUser)
                .Include(item => item.CreatedBy)
                .Include(item => item.Comments)
                    .ThenInclude(comment => comment.Author)
                .Include(item => item.Attachments)
                    .ThenInclude(attachment => attachment.UploadedBy)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);
        }

        public async Task<ProjectTask?> GetForEditAsync(int id)
        {
            return await _context.Tasks
                .Include(item => item.Project)
                    .ThenInclude(project => project.Members)
                .FirstOrDefaultAsync(item => item.Id == id);
        }

        public async Task<ProjectTask?> GetForDeleteAsync(int id)
        {
            return await _context.Tasks
                .Include(item => item.Project)
                    .ThenInclude(project => project.Members)
                .Include(item => item.AssignedUser)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);
        }

        public async Task<ProjectTask?> GetTrackedWithProjectAsync(int id)
        {
            return await _context.Tasks
                .Include(item => item.Project)
                    .ThenInclude(project => project.Members)
                .FirstOrDefaultAsync(item => item.Id == id);
        }

        public async Task<Project?> GetManageableProjectAsync(int projectId, ClaimsPrincipal user)
        {
            var project = await _context.Projects
                .Include(item => item.Members)
                .FirstOrDefaultAsync(item => item.Id == projectId);

            return project is not null && CanManage(project, user) ? project : null;
        }

        public async Task<bool> ActiveUserExistsAsync(string? userId)
        {
            return string.IsNullOrWhiteSpace(userId) ||
                await _context.Users.AnyAsync(user => user.Id == userId && user.IsActive);
        }

        public async Task PopulateOptionsAsync(TaskFormViewModel model, ClaimsPrincipal user)
        {
            var projects = await _context.Projects
                .AsNoTracking()
                .OrderBy(project => project.Name)
                .ToListAsync();

            if (!CanManageTasks(user))
            {
                var userId = _userManager.GetUserId(user);
                projects = projects
                    .Where(project => project.OwnerId == userId)
                    .ToList();
            }

            var users = await _context.Users
                .AsNoTracking()
                .Where(item => item.IsActive)
                .OrderBy(item => item.FirstName)
                .ThenBy(item => item.LastName)
                .ToListAsync();

            model.ProjectOptions = projects
                .Select(project => new SelectListItem(project.Name, project.Id.ToString()))
                .ToList();

            model.UserOptions = users
                .Select(item => new SelectListItem($"{item.FirstName} {item.LastName} ({item.Email})", item.Id))
                .ToList();
        }

        public async Task<ProjectTask> CreateAsync(TaskFormViewModel model, Project project, string actorId, Func<int, string?> detailsUrl)
        {
            var task = new ProjectTask
            {
                Title = model.Title,
                Description = model.Description,
                ProjectId = model.ProjectId,
                AssignedUserId = string.IsNullOrWhiteSpace(model.AssignedUserId) ? null : model.AssignedUserId,
                Status = model.Status,
                Priority = model.Priority,
                DueDate = model.DueDate,
                CreatedById = actorId,
                Project = project
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            AddTaskAssignmentNotification(task, actorId, detailsUrl);
            AddDeadlineNotification(task, actorId, detailsUrl);
            _activityLogger.Log(
                actorId,
                "Create",
                "Task",
                task.Id,
                $"Created task \"{task.Title}\".",
                projectId: task.ProjectId,
                taskId: task.Id);
            await _context.SaveChangesAsync();

            return task;
        }

        public async Task UpdateAsync(TaskFormViewModel model, ProjectTask task, Project project, string? actorId, Func<int, string?> detailsUrl)
        {
            var oldAssignedUserId = task.AssignedUserId;
            var oldStatus = task.Status;
            var oldDueDate = task.DueDate;

            task.Title = model.Title;
            task.Description = model.Description;
            task.ProjectId = model.ProjectId;
            task.Project = project;
            task.AssignedUserId = string.IsNullOrWhiteSpace(model.AssignedUserId) ? null : model.AssignedUserId;
            task.Status = model.Status;
            task.Priority = model.Priority;
            task.DueDate = model.DueDate;
            task.UpdatedAt = DateTime.UtcNow;

            if (task.AssignedUserId != oldAssignedUserId)
            {
                AddTaskAssignmentNotification(task, actorId, detailsUrl);
            }

            if (task.Status == ProjectTaskStatus.Completed && oldStatus != ProjectTaskStatus.Completed)
            {
                AddTaskCompletedNotifications(task, actorId, detailsUrl);
            }

            if (task.DueDate?.Date != oldDueDate?.Date)
            {
                AddDeadlineNotification(task, actorId, detailsUrl);
            }

            _activityLogger.Log(
                actorId,
                "Update",
                "Task",
                task.Id,
                $"Updated task \"{task.Title}\".",
                projectId: task.ProjectId,
                taskId: task.Id);

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(ProjectTask task, string? actorId)
        {
            _context.Tasks.Remove(task);
            _activityLogger.Log(
                actorId,
                "Delete",
                "Task",
                task.Id,
                $"Deleted task \"{task.Title}\".",
                projectId: task.ProjectId,
                taskId: task.Id);
            await _context.SaveChangesAsync();
        }

        public async Task ChangeStatusAsync(ProjectTask task, ProjectTaskStatus status, string? actorId, Func<int, string?> detailsUrl)
        {
            var oldStatus = task.Status;
            task.Status = status;
            task.UpdatedAt = DateTime.UtcNow;

            if (oldStatus != status)
            {
                _activityLogger.Log(
                    actorId,
                    status == ProjectTaskStatus.Completed ? "Complete" : "StatusChange",
                    "Task",
                    task.Id,
                    $"Changed task \"{task.Title}\" from {oldStatus} to {status}.",
                    projectId: task.ProjectId,
                    taskId: task.Id);
            }

            if (task.Status == ProjectTaskStatus.Completed && oldStatus != ProjectTaskStatus.Completed)
            {
                AddTaskCompletedNotifications(task, actorId, detailsUrl);
            }

            await _context.SaveChangesAsync();
        }

        public bool CanView(Project project, ClaimsPrincipal user)
        {
            return _accessService.CanView(project, user);
        }

        public bool CanView(ProjectTask task, ClaimsPrincipal user)
        {
            return _accessService.CanView(task, user);
        }

        public bool CanManage(Project project, ClaimsPrincipal user)
        {
            return _accessService.CanManage(project, user);
        }

        public bool CanManageTasks(ClaimsPrincipal user)
        {
            return user.IsInRole(AppRoles.Admin) || user.IsInRole(AppRoles.Manager);
        }

        public bool CanUpdateStatus(ProjectTask task, ClaimsPrincipal user)
        {
            return _accessService.CanUpdateStatus(task, user);
        }

        public TaskFormViewModel ToFormModel(ProjectTask task)
        {
            return new TaskFormViewModel
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                ProjectId = task.ProjectId,
                AssignedUserId = task.AssignedUserId,
                Status = task.Status,
                Priority = task.Priority,
                DueDate = task.DueDate
            };
        }

        private IQueryable<ProjectTask> VisibleTasks(ClaimsPrincipal user)
        {
            var userId = _userManager.GetUserId(user);
            var query = _context.Tasks
                .Include(task => task.Project)
                    .ThenInclude(project => project.Members)
                .AsQueryable();

            if (CanManageTasks(user))
            {
                return query;
            }

            return query.Where(task =>
                task.AssignedUserId == userId ||
                task.CreatedById == userId ||
                task.Project.OwnerId == userId ||
                task.Project.Members.Any(member => member.UserId == userId));
        }

        private static IQueryable<ProjectTask> ApplyFilters(IQueryable<ProjectTask> tasksQuery, TaskIndexQuery query)
        {
            if (query.ProjectId.HasValue)
            {
                tasksQuery = tasksQuery.Where(task => task.ProjectId == query.ProjectId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                tasksQuery = tasksQuery.Where(task =>
                    task.Title.Contains(search) ||
                    (task.Description != null && task.Description.Contains(search)) ||
                    task.Project.Name.Contains(search) ||
                    (task.AssignedUser != null &&
                        (task.AssignedUser.FirstName.Contains(search) ||
                         task.AssignedUser.LastName.Contains(search) ||
                         (task.AssignedUser.Email != null && task.AssignedUser.Email.Contains(search)))));
            }

            if (query.Status.HasValue)
            {
                tasksQuery = tasksQuery.Where(task => task.Status == query.Status.Value);
            }

            if (query.Priority.HasValue)
            {
                tasksQuery = tasksQuery.Where(task => task.Priority == query.Priority.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.AssignedUserId))
            {
                tasksQuery = query.AssignedUserId == "unassigned"
                    ? tasksQuery.Where(task => task.AssignedUserId == null)
                    : tasksQuery.Where(task => task.AssignedUserId == query.AssignedUserId);
            }

            var today = DateTime.UtcNow.Date;
            return query.Due switch
            {
                "overdue" => tasksQuery.Where(task =>
                    task.DueDate.HasValue &&
                    task.DueDate.Value.Date < today &&
                    task.Status != ProjectTaskStatus.Completed),
                "today" => tasksQuery.Where(task => task.DueDate.HasValue && task.DueDate.Value.Date == today),
                "week" => tasksQuery.Where(task =>
                    task.DueDate.HasValue &&
                    task.DueDate.Value.Date >= today &&
                    task.DueDate.Value.Date <= today.AddDays(7)),
                "none" => tasksQuery.Where(task => !task.DueDate.HasValue),
                _ => tasksQuery
            };
        }

        private void AddTaskAssignmentNotification(ProjectTask task, string? actorId, Func<int, string?> detailsUrl)
        {
            if (string.IsNullOrWhiteSpace(task.AssignedUserId))
            {
                return;
            }

            AddNotification(
                task.AssignedUserId,
                "You were assigned a task",
                $"Task \"{task.Title}\" was assigned to you.",
                detailsUrl(task.Id),
                NotificationType.TaskAssigned,
                actorId);
        }

        private void AddDeadlineNotification(ProjectTask task, string? actorId, Func<int, string?> detailsUrl)
        {
            if (task.DueDate?.Date != DateTime.UtcNow.Date.AddDays(1) ||
                string.IsNullOrWhiteSpace(task.AssignedUserId))
            {
                return;
            }

            AddNotification(
                task.AssignedUserId,
                "Task deadline is tomorrow",
                $"Task \"{task.Title}\" is due tomorrow.",
                detailsUrl(task.Id),
                NotificationType.Deadline,
                actorId);
        }

        private void AddTaskCompletedNotifications(ProjectTask task, string? actorId, Func<int, string?> detailsUrl)
        {
            var recipientIds = new[]
            {
                task.AssignedUserId,
                task.CreatedById,
                task.Project.OwnerId
            }
            .Concat(task.Project.Members.Select(member => member.UserId))
            .Where(userId => !string.IsNullOrWhiteSpace(userId))
            .Distinct();

            foreach (var recipientId in recipientIds)
            {
                AddNotification(
                    recipientId!,
                    "Task completed",
                    $"Task \"{task.Title}\" was completed.",
                    detailsUrl(task.Id),
                    NotificationType.TaskCompleted,
                    actorId);
            }
        }

        private void AddNotification(
            string userId,
            string title,
            string message,
            string? linkUrl,
            NotificationType type,
            string? actorId)
        {
            if (string.IsNullOrWhiteSpace(userId) || userId == actorId)
            {
                return;
            }

            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                LinkUrl = linkUrl,
                Type = type
            });
        }
    }
}
