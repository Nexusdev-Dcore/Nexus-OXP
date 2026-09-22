using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusOXP.Authorization;
using NexusOXP.Data;
using NexusOXP.Models;
using NexusOXP.Services;

namespace NexusOXP.Controllers
{
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager},{AppRoles.Member}")]
    public class AttachmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProjectAccessService _accessService;
        private readonly IFileStorageService _fileStorage;
        private readonly IActivityLogger _activityLogger;

        public AttachmentsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IProjectAccessService accessService,
            IFileStorageService fileStorage,
            IActivityLogger activityLogger)
        {
            _context = context;
            _userManager = userManager;
            _accessService = accessService;
            _fileStorage = fileStorage;
            _activityLogger = activityLogger;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(int taskId, IFormFile? file)
        {
            var task = await _context.Tasks
                .Include(item => item.Project)
                    .ThenInclude(project => project.Members)
                .FirstOrDefaultAsync(item => item.Id == taskId);

            if (task is null)
            {
                return NotFound();
            }

            if (!_accessService.CanView(task, User))
            {
                return Forbid();
            }

            if (file is null)
            {
                TempData["StatusMessage"] = "Choose a file before uploading.";
                return RedirectToAction("Details", "Tasks", new { id = taskId });
            }

            StoredFileResult storedFile;
            try
            {
                storedFile = await _fileStorage.SaveTaskAttachmentAsync(taskId, file);
            }
            catch (InvalidOperationException ex)
            {
                TempData["StatusMessage"] = ex.Message;
                return RedirectToAction("Details", "Tasks", new { id = taskId });
            }

            var userId = _userManager.GetUserId(User);
            if (userId is null)
            {
                return Challenge();
            }

            var attachment = new TaskAttachment
            {
                TaskId = taskId,
                UploadedById = userId,
                FileName = storedFile.FileName,
                StoredFileName = storedFile.StoredFileName,
                ContentType = storedFile.ContentType,
                FileSize = storedFile.FileSize
            };

            _context.TaskAttachments.Add(attachment);
            _activityLogger.Log(
                userId,
                "Upload",
                "TaskAttachment",
                taskId,
                $"Uploaded \"{attachment.FileName}\" to task \"{task.Title}\".",
                projectId: task.ProjectId,
                taskId: task.Id);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Attachment uploaded.";
            return RedirectToAction("Details", "Tasks", new { id = taskId });
        }

        public async Task<IActionResult> Download(int id)
        {
            var attachment = await _context.TaskAttachments
                .Include(item => item.Task)
                    .ThenInclude(task => task.Project)
                        .ThenInclude(project => project.Members)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);

            if (attachment is null)
            {
                return NotFound();
            }

            if (!_accessService.CanView(attachment.Task, User))
            {
                return Forbid();
            }

            var path = _fileStorage.GetTaskAttachmentPath(attachment.TaskId, attachment.StoredFileName);
            if (!System.IO.File.Exists(path))
            {
                return NotFound();
            }

            return PhysicalFile(path, attachment.ContentType, attachment.FileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var attachment = await _context.TaskAttachments
                .Include(item => item.Task)
                    .ThenInclude(task => task.Project)
                        .ThenInclude(project => project.Members)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (attachment is null)
            {
                return NotFound();
            }

            if (!_accessService.CanManage(attachment.Task.Project, User) &&
                attachment.UploadedById != _userManager.GetUserId(User))
            {
                return Forbid();
            }

            var taskId = attachment.TaskId;
            var fileName = attachment.FileName;

            await _fileStorage.DeleteTaskAttachmentAsync(attachment.TaskId, attachment.StoredFileName);
            _context.TaskAttachments.Remove(attachment);
            _activityLogger.Log(
                _userManager.GetUserId(User),
                "Delete",
                "TaskAttachment",
                attachment.Id,
                $"Deleted attachment \"{fileName}\".",
                projectId: attachment.Task.ProjectId,
                taskId: attachment.TaskId);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Attachment deleted.";
            return RedirectToAction("Details", "Tasks", new { id = taskId });
        }
    }
}
