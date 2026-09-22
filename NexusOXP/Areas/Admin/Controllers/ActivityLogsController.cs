using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusOXP.Authorization;
using NexusOXP.Data;

namespace NexusOXP.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = AppRoles.Admin)]
    public class ActivityLogsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ActivityLogsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var activity = await _context.ActivityLogs
                .Include(item => item.Actor)
                .AsNoTracking()
                .OrderByDescending(item => item.CreatedAt)
                .Take(250)
                .ToListAsync();

            return View(activity);
        }
    }
}
