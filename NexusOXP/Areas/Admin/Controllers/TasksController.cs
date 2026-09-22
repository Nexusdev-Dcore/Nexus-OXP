using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusOXP.Authorization;
using NexusOXP.Data;

namespace NexusOXP.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = AppRoles.Admin)]
    public class TasksController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TasksController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var tasks = await _context.Tasks
                .Include(task => task.Project)
                .Include(task => task.AssignedUser)
                .Include(task => task.Attachments)
                .AsNoTracking()
                .OrderBy(task => task.Status)
                .ThenByDescending(task => task.Priority)
                .ToListAsync();

            return View(tasks);
        }
    }
}
