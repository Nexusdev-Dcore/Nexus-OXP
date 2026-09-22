using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusOXP.Authorization;
using NexusOXP.Data;
using NexusOXP.Models;

namespace NexusOXP.Controllers
{
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager},{AppRoles.Member}")]
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            if (userId is null)
            {
                return Challenge();
            }

            var notifications = await _context.Notifications
                .AsNoTracking()
                .Where(notification => notification.UserId == userId)
                .OrderBy(notification => notification.IsRead)
                .ThenByDescending(notification => notification.CreatedAt)
                .Take(100)
                .ToListAsync();

            return View(notifications);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id, string? returnUrl = null)
        {
            var userId = _userManager.GetUserId(User);
            if (userId is null)
            {
                return Challenge();
            }

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);

            if (notification is null)
            {
                return NotFound();
            }

            MarkAsRead(notification);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var userId = _userManager.GetUserId(User);
            if (userId is null)
            {
                return Challenge();
            }

            var unreadNotifications = await _context.Notifications
                .Where(notification => notification.UserId == userId && !notification.IsRead)
                .ToListAsync();

            foreach (var notification in unreadNotifications)
            {
                MarkAsRead(notification);
            }

            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Notifications marked as read.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Open(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (userId is null)
            {
                return Challenge();
            }

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);

            if (notification is null)
            {
                return NotFound();
            }

            MarkAsRead(notification);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(notification.LinkUrl) && Url.IsLocalUrl(notification.LinkUrl))
            {
                return LocalRedirect(notification.LinkUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        private static void MarkAsRead(Notification notification)
        {
            if (notification.IsRead)
            {
                return;
            }

            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
        }
    }
}
