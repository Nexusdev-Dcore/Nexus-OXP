using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusOXP.Authorization;
using NexusOXP.Data;
using NexusOXP.Models;
using NexusOXP.Services;
using NexusOXP.ViewModels;

namespace NexusOXP.Controllers.Api
{
    [ApiController]
    [Route("api/tasks")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager},{AppRoles.Member}")]
    public class TasksApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProjectAccessService _accessService;
        private readonly IActivityLogger _activityLogger;

        public TasksApiController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IProjectAccessService accessService,
            IActivityLogger activityLogger)
        {
            _context = context;
            _userManager = userManager;
            _accessService = accessService;
            _activityLogger = activityLogger;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<TaskDto>>> Get(int? projectId = null)
        {
            var userId = _userManager.GetUserId(User);
            var tasksQuery = _context.Tasks
                .Include(task => task.Project)
                    .ThenInclude(project => project.Members)
                .Include(task => task.AssignedUser)
                .Include(task => task.Attachments)
                .AsNoTracking();

            if (!_accessService.CanManageProjects(User))
            {
                tasksQuery = tasksQuery.Where(task =>
                    task.AssignedUserId == userId ||
                    task.CreatedById == userId ||
                    task.Project.OwnerId == userId ||
                    task.Project.Members.Any(member => member.UserId == userId));
            }

            if (projectId.HasValue)
            {
                tasksQuery = tasksQuery.Where(task => task.ProjectId == projectId.Value);
            }

            var tasks = await tasksQuery
                .OrderBy(task => task.Status)
                .ThenByDescending(task => task.Priority)
                .ToListAsync();

            return Ok(tasks.Select(ToDto).ToList());
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<TaskDto>> Get(int id)
        {
            var task = await _context.Tasks
                .Include(item => item.Project)
                    .ThenInclude(project => project.Members)
                .Include(item => item.AssignedUser)
                .Include(item => item.Attachments)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);

            if (task is null)
            {
                return NotFound();
            }

            return _accessService.CanView(task, User)
                ? Ok(ToDto(task))
                : Forbid();
        }

        [HttpPost]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<ActionResult<TaskDto>> Post(TaskCreateDto request)
        {
            var project = await _context.Projects
                .Include(item => item.Members)
                .FirstOrDefaultAsync(item => item.Id == request.ProjectId);

            if (project is null)
            {
                return ValidationProblem("Select a valid project.");
            }

            if (!_accessService.CanManage(project, User))
            {
                return Forbid();
            }

            if (!await ValidateAssignedUser(request.AssignedUserId))
            {
                return ValidationProblem("Select a valid active assignee.");
            }

            var userId = _userManager.GetUserId(User);
            if (userId is null)
            {
                return Unauthorized();
            }

            var task = new ProjectTask
            {
                Title = request.Title,
                Description = request.Description,
                ProjectId = request.ProjectId,
                AssignedUserId = string.IsNullOrWhiteSpace(request.AssignedUserId) ? null : request.AssignedUserId,
                Status = request.Status,
                Priority = request.Priority,
                DueDate = request.DueDate,
                CreatedById = userId
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            _activityLogger.Log(userId, "Create", "Task", task.Id, $"Created task \"{task.Title}\" via API.", projectId: task.ProjectId, taskId: task.Id);
            await _context.SaveChangesAsync();

            var result = await _context.Tasks
                .Include(item => item.Project)
                .Include(item => item.AssignedUser)
                .Include(item => item.Attachments)
                .AsNoTracking()
                .FirstAsync(item => item.Id == task.Id);

            return CreatedAtAction(nameof(Get), new { id = task.Id }, ToDto(result));
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> Put(int id, TaskUpdateDto request)
        {
            var task = await _context.Tasks
                .Include(item => item.Project)
                    .ThenInclude(project => project.Members)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (task is null)
            {
                return NotFound();
            }

            var newProject = await _context.Projects
                .Include(project => project.Members)
                .FirstOrDefaultAsync(project => project.Id == request.ProjectId);

            if (newProject is null)
            {
                return ValidationProblem("Select a valid project.");
            }

            if (!_accessService.CanManage(task.Project, User) || !_accessService.CanManage(newProject, User))
            {
                return Forbid();
            }

            if (!await ValidateAssignedUser(request.AssignedUserId))
            {
                return ValidationProblem("Select a valid active assignee.");
            }

            task.Title = request.Title;
            task.Description = request.Description;
            task.ProjectId = request.ProjectId;
            task.AssignedUserId = string.IsNullOrWhiteSpace(request.AssignedUserId) ? null : request.AssignedUserId;
            task.Status = request.Status;
            task.Priority = request.Priority;
            task.DueDate = request.DueDate;
            task.UpdatedAt = DateTime.UtcNow;

            _activityLogger.Log(_userManager.GetUserId(User), "Update", "Task", task.Id, $"Updated task \"{task.Title}\" via API.", projectId: task.ProjectId, taskId: task.Id);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task<bool> ValidateAssignedUser(string? assignedUserId)
        {
            return string.IsNullOrWhiteSpace(assignedUserId) ||
                await _context.Users.AnyAsync(user => user.Id == assignedUserId && user.IsActive);
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> Delete(int id)
        {
            var task = await _context.Tasks
                .Include(item => item.Project)
                    .ThenInclude(project => project.Members)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (task is null)
            {
                return NotFound();
            }

            if (!_accessService.CanManage(task.Project, User))
            {
                return Forbid();
            }

            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static TaskDto ToDto(ProjectTask task)
        {
            return new TaskDto(
                task.Id,
                task.Title,
                task.Description,
                task.ProjectId,
                task.Project.Name,
                task.AssignedUserId,
                task.AssignedUser is null ? null : $"{task.AssignedUser.FirstName} {task.AssignedUser.LastName}",
                task.Status,
                task.Priority,
                task.CreatedAt,
                task.UpdatedAt,
                task.DueDate,
                task.Attachments.Count);
        }
    }
}
