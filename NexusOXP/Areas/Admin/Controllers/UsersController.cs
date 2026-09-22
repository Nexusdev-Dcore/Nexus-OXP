using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusOXP.Authorization;
using NexusOXP.Data;
using NexusOXP.Models;

namespace NexusOXP.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = AppRoles.Admin)]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public UsersController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? q = null)
        {
            var usersQuery = _context.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var query = q.Trim();
                usersQuery = usersQuery.Where(user =>
                    user.FirstName.Contains(query) ||
                    user.LastName.Contains(query) ||
                    (user.Email != null && user.Email.Contains(query)));
            }

            var users = await usersQuery
                .OrderBy(user => user.FirstName)
                .ThenBy(user => user.LastName)
                .ToListAsync();

            ViewBag.Query = q;
            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);
            if (user.Id == currentUserId)
            {
                TempData["StatusMessage"] = "You cannot deactivate your own account.";
                return RedirectToAction(nameof(Index));
            }

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);

            TempData["StatusMessage"] = user.IsActive
                ? "User activated."
                : "User deactivated.";

            return RedirectToAction(nameof(Index));
        }
    }
}
