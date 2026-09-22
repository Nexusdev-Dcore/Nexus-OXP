using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusOXP.Authorization;
using NexusOXP.Data;
using NexusOXP.ViewModels;

namespace NexusOXP.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = AppRoles.Admin)]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var model = new AdminDashboardViewModel
            {
                UsersCount = await _context.Users.CountAsync(),
                ActiveUsersCount = await _context.Users.CountAsync(user => user.IsActive),
                ProjectsCount = await _context.Projects.CountAsync(),
                TeamsCount = await _context.Teams.CountAsync(),
                TasksCount = await _context.Tasks.CountAsync(),
                AttachmentsCount = await _context.TaskAttachments.CountAsync(),
                RecentUsers = await _context.Users
                    .AsNoTracking()
                    .OrderByDescending(user => user.CreatedAt)
                    .Take(8)
                    .Select(user => new AdminUserSummary
                    {
                        Id = user.Id,
                        Name = user.FirstName + " " + user.LastName,
                        Email = user.Email ?? string.Empty,
                        IsActive = user.IsActive,
                        CreatedAt = user.CreatedAt
                    })
                    .ToListAsync(),
                RecentActivity = await _context.ActivityLogs
                    .AsNoTracking()
                    .OrderByDescending(activity => activity.CreatedAt)
                    .Take(10)
                    .Select(activity => new AdminActivitySummary
                    {
                        Action = activity.Action,
                        EntityType = activity.EntityType,
                        Description = activity.Description,
                        CreatedAt = activity.CreatedAt
                    })
                    .ToListAsync()
            };

            return View(model);
        }
    }
}
