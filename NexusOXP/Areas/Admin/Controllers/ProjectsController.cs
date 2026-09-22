using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusOXP.Authorization;
using NexusOXP.Data;

namespace NexusOXP.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = AppRoles.Admin)]
    public class ProjectsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProjectsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var projects = await _context.Projects
                .Include(project => project.Owner)
                .Include(project => project.Team)
                .Include(project => project.Tasks)
                .AsNoTracking()
                .OrderByDescending(project => project.CreatedAt)
                .ToListAsync();

            return View(projects);
        }
    }
}
