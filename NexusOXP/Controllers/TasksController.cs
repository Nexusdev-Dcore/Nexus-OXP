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
    public class TasksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IActivityLogger _activityLogger;

        public TasksController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IActivityLogger activityLogger)
        {
            _context = context;
            _userManager = userManager;
            _activityLogger = activityLogger;
        }

        public async Task<IActionResult> Index(
            int? projectId = null,
            string? q = null,
            ProjectTaskStatus? status = null,
            ProjectPriority? priority = null,
            string? assignedUserId = null,
            string? due = null)
        {
            var tasksQuery = VisibleTasks()
                .Include(task => task.Project)
                .Include(task => task.AssignedUser)
                .AsNoTracking();

            if (projectId.HasValue)
            {
                tasksQuery = tasksQuery.Where(task => task.ProjectId == projectId.Value);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var query = q.Trim();
                tasksQuery = tasksQuery.Where(task =>
                    task.Title.Contains(query) ||
                    (task.Description != null && task.Description.Contains(query)) ||
                    task.Project.Name.Contains(query) ||
                    (task.AssignedUser != null &&
                        (task.AssignedUser.FirstName.Contains(query) ||
                         task.AssignedUser.LastName.Contains(query) ||
                         (task.AssignedUser.Email != null && task.AssignedUser.Email.Contains(query)))));
            }

            if (status.HasValue)
            {
                tasksQuery = tasksQuery.Where(task => task.Status == status.Value);
            }

            if (priority.HasValue)
            {
                tasksQuery = tasksQuery.Where(task => task.Priority == priority.Value);
            }

            if (!string.IsNullOrWhiteSpace(assignedUserId))
            {
                tasksQuery = assignedUserId == "unassigned"
                    ? tasksQuery.Where(task => task.AssignedUserId == null)
                    : tasksQuery.Where(task => task.AssignedUserId == assignedUserId);
            }

            var today = DateTime.UtcNow.Date;
            tasksQuery = due switch
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

            var tasks = await tasksQuery
                .OrderBy(task => task.Status)
                .ThenByDescending(task => task.Priority)
                .ThenBy(task => task.DueDate)
                .ToListAsync();

            ViewBag.ProjectId = projectId;
            ViewBag.Query = q;
            ViewBag.Status = status;
            ViewBag.Priority = priority;
            ViewBag.AssignedUserId = assignedUserId;
            ViewBag.Due = due;
            ViewBag.CanManageTasks = CanManageTasks();
            await PopulateTaskIndexFilters();

            return View(tasks);
        }

        public async Task<IActionResult> Board(int? projectId = null)
        {
            var tasksQuery = VisibleTasks()
                .Include(task => task.Project)
                .Include(task => task.AssignedUser)
                .AsNoTracking();

            Project? project = null;
            if (projectId.HasValue)
            {
                project = await _context.Projects
                    .Include(item => item.Members)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == projectId.Value);

                if (project is null)
                {
                    return NotFound();
                }

                if (!CanView(project))
                {
                    return Forbid();
                }

                tasksQuery = tasksQuery.Where(task => task.ProjectId == projectId.Value);
            }

            var tasks = await tasksQuery
                .OrderByDescending(task => task.Priority)
                .ThenBy(task => task.DueDate)
                .ToListAsync();

            ViewBag.ProjectId = projectId;
            ViewBag.ProjectName = project?.Name;
            ViewBag.CanManageTasks = CanManageTasks();

            return View(tasks);
        }

        public async Task<IActionResult> Details(int id)
        {
            var task = await _context.Tasks
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

            if (task is null)
            {
                return NotFound();
            }

            if (!CanView(task))
            {
                return Forbid();
            }

            ViewBag.CanManageTasks = CanManage(task.Project);
            ViewBag.CanUpdateStatus = CanUpdateStatus(task);
            ViewBag.CurrentUserId = _userManager.GetUserId(User);
            ViewBag.CommentForm = new CommentFormViewModel { TaskId = task.Id };

            return View(task);
        }

        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> Create(int? projectId = null)
        {
            if (projectId.HasValue)
            {
                var project = await _context.Projects
                    .Include(item => item.Members)
                    .FirstOrDefaultAsync(item => item.Id == projectId.Value);

                if (project is null)
                {
                    return NotFound();
                }

                if (!CanManage(project))
                {
                    return Forbid();
                }
            }

            var model = new TaskFormViewModel
            {
                ProjectId = projectId ?? 0
            };

            await PopulateOptions(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> Create(TaskFormViewModel model)
        {
            var project = await ValidateProject(model.ProjectId);
            await ValidateAssignedUser(model.AssignedUserId);
            ValidateDueDate(model);

            if (!ModelState.IsValid || project is null)
            {
                await PopulateOptions(model);
                return View(model);
            }

            var userId = _userManager.GetUserId(User);
            if (userId is null)
            {
                return Challenge();
            }

            var task = new ProjectTask
            {
                Title = model.Title,
                Description = model.Description,
                ProjectId = model.ProjectId,
                AssignedUserId = string.IsNullOrWhiteSpace(model.AssignedUserId) ? null : model.AssignedUserId,
                Status = model.Status,
                Priority = model.Priority,
                DueDate = model.DueDate,
                CreatedById = userId
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            AddTaskAssignmentNotification(task, userId);
            AddDeadlineNotification(task, userId);
            _activityLogger.Log(
                userId,
                "Create",
                "Task",
                task.Id,
                $"Created task \"{task.Title}\".",
                projectId: task.ProjectId,
                taskId: task.Id);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Task created successfully.";
            return RedirectToAction(nameof(Details), new { id = task.Id });
        }

        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> Edit(int id)
        {
            var task = await _context.Tasks
                .Include(item => item.Project)
                    .ThenInclude(project => project.Members)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (task is null)
            {
                return NotFound();
            }

            if (!CanManage(task.Project))
            {
                return Forbid();
            }

            var model = ToFormModel(task);
            await PopulateOptions(model);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> Edit(int id, TaskFormViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            var project = await ValidateProject(model.ProjectId);
            await ValidateAssignedUser(model.AssignedUserId);
            ValidateDueDate(model);

            if (!ModelState.IsValid || project is null)
            {
                await PopulateOptions(model);
                return View(model);
            }

            var task = await _context.Tasks
                .Include(item => item.Project)
                    .ThenInclude(projectItem => projectItem.Members)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (task is null)
            {
                return NotFound();
            }

            if (!CanManage(task.Project) || !CanManage(project))
            {
                return Forbid();
            }

            var oldAssignedUserId = task.AssignedUserId;
            var oldStatus = task.Status;
            var oldDueDate = task.DueDate;
            var currentUserId = _userManager.GetUserId(User);

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
                AddTaskAssignmentNotification(task, currentUserId);
            }

            if (task.Status == ProjectTaskStatus.Completed && oldStatus != ProjectTaskStatus.Completed)
            {
                AddTaskCompletedNotifications(task, currentUserId);
            }

            if (task.DueDate?.Date != oldDueDate?.Date)
            {
                AddDeadlineNotification(task, currentUserId);
            }

            _activityLogger.Log(
                currentUserId,
                "Update",
                "Task",
                task.Id,
                $"Updated task \"{task.Title}\".",
                projectId: task.ProjectId,
                taskId: task.Id);

            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Task updated successfully.";
            return RedirectToAction(nameof(Details), new { id = task.Id });
        }

        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> Delete(int id)
        {
            var task = await _context.Tasks
                .Include(item => item.Project)
                    .ThenInclude(project => project.Members)
                .Include(item => item.AssignedUser)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);

            if (task is null)
            {
                return NotFound();
            }

            if (!CanManage(task.Project))
            {
                return Forbid();
            }

            return View(task);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var task = await _context.Tasks
                .Include(item => item.Project)
                    .ThenInclude(project => project.Members)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (task is null)
            {
                return NotFound();
            }

            if (!CanManage(task.Project))
            {
                return Forbid();
            }

            _context.Tasks.Remove(task);
            _activityLogger.Log(
                _userManager.GetUserId(User),
                "Delete",
                "Task",
                task.Id,
                $"Deleted task \"{task.Title}\".",
                projectId: task.ProjectId,
                taskId: task.Id);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Task deleted successfully.";
            return RedirectToAction(nameof(Index), new { projectId = task.ProjectId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(int id, ProjectTaskStatus status, string? returnUrl = null)
        {
            var task = await _context.Tasks
                .Include(item => item.Project)
                    .ThenInclude(project => project.Members)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (task is null)
            {
                return NotFound();
            }

            if (!CanUpdateStatus(task))
            {
                return Forbid();
            }

            var oldStatus = task.Status;
            var currentUserId = _userManager.GetUserId(User);

            task.Status = status;
            task.UpdatedAt = DateTime.UtcNow;

            if (oldStatus != status)
            {
                _activityLogger.Log(
                    currentUserId,
                    status == ProjectTaskStatus.Completed ? "Complete" : "StatusChange",
                    "Task",
                    task.Id,
                    $"Changed task \"{task.Title}\" from {oldStatus} to {status}.",
                    projectId: task.ProjectId,
                    taskId: task.Id);
            }

            if (task.Status == ProjectTaskStatus.Completed && oldStatus != ProjectTaskStatus.Completed)
            {
                AddTaskCompletedNotifications(task, currentUserId);
            }

            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Task status updated.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Details), new { id = task.Id });
        }

        private IQueryable<ProjectTask> VisibleTasks()
        {
            var userId = _userManager.GetUserId(User);

            var query = _context.Tasks
                .Include(task => task.Project)
                    .ThenInclude(project => project.Members)
                .AsQueryable();

            if (CanManageTasks())
            {
                return query;
            }

            return query.Where(task =>
                task.AssignedUserId == userId ||
                task.CreatedById == userId ||
                task.Project.OwnerId == userId ||
                task.Project.Members.Any(member => member.UserId == userId));
        }

        private async Task<Project?> ValidateProject(int projectId)
        {
            var project = await _context.Projects
                .Include(item => item.Members)
                .FirstOrDefaultAsync(item => item.Id == projectId);

            if (project is null)
            {
                ModelState.AddModelError(nameof(TaskFormViewModel.ProjectId), "Select a valid project.");
                return null;
            }

            if (!CanManage(project))
            {
                ModelState.AddModelError(nameof(TaskFormViewModel.ProjectId), "You cannot manage this project.");
                return null;
            }

            return project;
        }

        private async Task ValidateAssignedUser(string? assignedUserId)
        {
            if (string.IsNullOrWhiteSpace(assignedUserId))
            {
                return;
            }

            var userExists = await _context.Users.AnyAsync(user => user.Id == assignedUserId && user.IsActive);
            if (!userExists)
            {
                ModelState.AddModelError(nameof(TaskFormViewModel.AssignedUserId), "Select a valid active user.");
            }
        }

        private static void ValidateDueDate(TaskFormViewModel model)
        {
            if (model.DueDate.HasValue && model.DueDate.Value.Date < DateTime.UtcNow.Date)
            {
                model.DueDate = model.DueDate.Value.Date;
            }
        }

        private async Task PopulateOptions(TaskFormViewModel model)
        {
            var projects = await _context.Projects
                .AsNoTracking()
                .OrderBy(project => project.Name)
                .ToListAsync();

            if (!CanManageTasks())
            {
                var userId = _userManager.GetUserId(User);
                projects = projects
                    .Where(project => project.OwnerId == userId)
                    .ToList();
            }

            var users = await _context.Users
                .AsNoTracking()
                .Where(user => user.IsActive)
                .OrderBy(user => user.FirstName)
                .ThenBy(user => user.LastName)
                .ToListAsync();

            model.ProjectOptions = projects
                .Select(project => new SelectListItem(project.Name, project.Id.ToString()))
                .ToList();

            model.UserOptions = users
                .Select(user => new SelectListItem($"{user.FirstName} {user.LastName} ({user.Email})", user.Id))
                .ToList();
        }

        private async Task PopulateTaskIndexFilters()
        {
            ViewBag.ProjectOptions = await _context.Projects
                .AsNoTracking()
                .OrderBy(project => project.Name)
                .Select(project => new SelectListItem(project.Name, project.Id.ToString()))
                .ToListAsync();

            ViewBag.UserOptions = await _context.Users
                .AsNoTracking()
                .Where(user => user.IsActive)
                .OrderBy(user => user.FirstName)
                .ThenBy(user => user.LastName)
                .Select(user => new SelectListItem($"{user.FirstName} {user.LastName} ({user.Email})", user.Id))
                .ToListAsync();
        }

        private bool CanView(ProjectTask task)
        {
            var userId = _userManager.GetUserId(User);

            return CanManage(task.Project) ||
                task.AssignedUserId == userId ||
                task.CreatedById == userId ||
                task.Project.OwnerId == userId ||
                task.Project.Members.Any(member => member.UserId == userId);
        }

        private bool CanView(Project project)
        {
            var userId = _userManager.GetUserId(User);

            return CanManage(project) ||
                project.OwnerId == userId ||
                project.Members.Any(member => member.UserId == userId);
        }

        private bool CanUpdateStatus(ProjectTask task)
        {
            var userId = _userManager.GetUserId(User);

            return CanManage(task.Project) ||
                task.AssignedUserId == userId ||
                task.CreatedById == userId;
        }

        private bool CanManage(Project project)
        {
            var userId = _userManager.GetUserId(User);

            return User.IsInRole(AppRoles.Admin) ||
                User.IsInRole(AppRoles.Manager) ||
                project.OwnerId == userId;
        }

        private bool CanManageTasks()
        {
            return User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager);
        }

        private void AddTaskAssignmentNotification(ProjectTask task, string? actorId)
        {
            if (string.IsNullOrWhiteSpace(task.AssignedUserId))
            {
                return;
            }

            AddNotification(
                task.AssignedUserId,
                "You were assigned a task",
                $"Task \"{task.Title}\" was assigned to you.",
                Url.Action(nameof(Details), "Tasks", new { id = task.Id }),
                NotificationType.TaskAssigned,
                actorId);
        }

        private void AddDeadlineNotification(ProjectTask task, string? actorId)
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
                Url.Action(nameof(Details), "Tasks", new { id = task.Id }),
                NotificationType.Deadline,
                actorId);
        }

        private void AddTaskCompletedNotifications(ProjectTask task, string? actorId)
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
                    Url.Action(nameof(Details), "Tasks", new { id = task.Id }),
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

        private static TaskFormViewModel ToFormModel(ProjectTask task)
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
    }
}
