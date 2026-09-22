using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusOXP.Authorization;
using NexusOXP.Data;
using NexusOXP.Models;
using NexusOXP.Services;
using NexusOXP.ViewModels;

namespace NexusOXP.Controllers
{
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager},{AppRoles.Member}")]
    public class CommentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IActivityLogger _activityLogger;

        public CommentsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IActivityLogger activityLogger)
        {
            _context = context;
            _userManager = userManager;
            _activityLogger = activityLogger;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CommentFormViewModel model)
        {
            var task = await _context.Tasks
                .Include(item => item.Project)
                    .ThenInclude(project => project.Members)
                .FirstOrDefaultAsync(item => item.Id == model.TaskId);

            if (task is null)
            {
                return NotFound();
            }

            if (!CanView(task))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                TempData["StatusMessage"] = "Write a comment before posting.";
                return RedirectToAction("Details", "Tasks", new { id = model.TaskId });
            }

            var userId = _userManager.GetUserId(User);
            if (userId is null)
            {
                return Challenge();
            }

            var comment = new Comment
            {
                TaskId = model.TaskId,
                AuthorId = userId,
                Content = model.Content.Trim()
            };

            _context.Comments.Add(comment);
            AddCommentNotifications(task, userId, model.Content.Trim());
            await _context.SaveChangesAsync();

            _activityLogger.Log(
                userId,
                "Create",
                "Comment",
                comment.Id,
                $"Commented on task \"{task.Title}\".",
                projectId: task.ProjectId,
                taskId: task.Id);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Comment posted.";
            return RedirectToAction("Details", "Tasks", new { id = model.TaskId });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var comment = await _context.Comments
                .Include(item => item.Task)
                    .ThenInclude(task => task.Project)
                        .ThenInclude(project => project.Members)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);

            if (comment is null)
            {
                return NotFound();
            }

            if (!CanManageComment(comment))
            {
                return Forbid();
            }

            return View(new CommentFormViewModel
            {
                Id = comment.Id,
                TaskId = comment.TaskId,
                Content = comment.Content
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CommentFormViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            var comment = await _context.Comments
                .Include(item => item.Task)
                    .ThenInclude(task => task.Project)
                        .ThenInclude(project => project.Members)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (comment is null)
            {
                return NotFound();
            }

            if (!CanManageComment(comment))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            comment.Content = model.Content.Trim();
            comment.UpdatedAt = DateTime.UtcNow;

            _activityLogger.Log(
                _userManager.GetUserId(User),
                "Update",
                "Comment",
                comment.Id,
                $"Updated a comment on task \"{comment.Task.Title}\".",
                projectId: comment.Task.ProjectId,
                taskId: comment.TaskId);

            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Comment updated.";
            return RedirectToAction("Details", "Tasks", new { id = comment.TaskId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var comment = await _context.Comments
                .Include(item => item.Task)
                    .ThenInclude(task => task.Project)
                        .ThenInclude(project => project.Members)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (comment is null)
            {
                return NotFound();
            }

            if (!CanManageComment(comment))
            {
                return Forbid();
            }

            var taskId = comment.TaskId;
            var taskTitle = comment.Task.Title;
            var projectId = comment.Task.ProjectId;
            _context.Comments.Remove(comment);
            _activityLogger.Log(
                _userManager.GetUserId(User),
                "Delete",
                "Comment",
                comment.Id,
                $"Deleted a comment on task \"{taskTitle}\".",
                projectId: projectId,
                taskId: taskId);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Comment deleted.";
            return RedirectToAction("Details", "Tasks", new { id = taskId });
        }

        private bool CanView(ProjectTask task)
        {
            var userId = _userManager.GetUserId(User);

            return CanModerate() ||
                task.AssignedUserId == userId ||
                task.CreatedById == userId ||
                task.Project.OwnerId == userId ||
                task.Project.Members.Any(member => member.UserId == userId);
        }

        private bool CanManageComment(Comment comment)
        {
            var userId = _userManager.GetUserId(User);

            return CanModerate() || comment.AuthorId == userId;
        }

        private bool CanModerate()
        {
            return User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager);
        }

        private void AddCommentNotifications(ProjectTask task, string authorId, string content)
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
                if (recipientId == authorId)
                {
                    continue;
                }

                _context.Notifications.Add(new Notification
                {
                    UserId = recipientId!,
                    Title = "New comment on your task",
                    Message = $"A new comment was added to \"{task.Title}\": {Preview(content)}",
                    LinkUrl = Url.Action("Details", "Tasks", new { id = task.Id }),
                    Type = NotificationType.TaskComment
                });
            }
        }

        private static string Preview(string content)
        {
            const int maxLength = 120;
            var normalized = content.ReplaceLineEndings(" ").Trim();
            return normalized.Length <= maxLength
                ? normalized
                : $"{normalized[..maxLength]}...";
        }
    }
}
