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
    [Route("api/projects")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager},{AppRoles.Member}")]
    public class ProjectsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProjectAccessService _accessService;
        private readonly IActivityLogger _activityLogger;

        public ProjectsApiController(
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
        public async Task<ActionResult<IReadOnlyList<ProjectDto>>> Get()
        {
            var userId = _userManager.GetUserId(User);
            var projectsQuery = _context.Projects
                .Include(project => project.Owner)
                .Include(project => project.Team)
                .Include(project => project.Members)
                .Include(project => project.Tasks)
                .AsNoTracking();

            if (!_accessService.CanManageProjects(User))
            {
                projectsQuery = projectsQuery.Where(project =>
                    project.OwnerId == userId ||
                    project.Members.Any(member => member.UserId == userId));
            }

            var projects = await projectsQuery
                .OrderByDescending(project => project.CreatedAt)
                .ToListAsync();

            return Ok(projects.Select(ToDto).ToList());
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ProjectDto>> Get(int id)
        {
            var project = await _context.Projects
                .Include(item => item.Owner)
                .Include(item => item.Team)
                .Include(item => item.Members)
                .Include(item => item.Tasks)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);

            if (project is null)
            {
                return NotFound();
            }

            return _accessService.CanView(project, User)
                ? Ok(ToDto(project))
                : Forbid();
        }

        [HttpPost]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<ActionResult<ProjectDto>> Post(ProjectCreateDto request)
        {
            ValidateProjectDates(request);

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var userId = _userManager.GetUserId(User);
            if (userId is null)
            {
                return Unauthorized();
            }

            var project = new Project
            {
                Name = request.Name,
                Description = request.Description,
                OwnerId = userId,
                StartDate = request.StartDate,
                Deadline = request.Deadline,
                Status = request.Status,
                Priority = request.Priority,
                Members =
                [
                    new ProjectMember
                    {
                        UserId = userId,
                        Role = ProjectMemberRole.Owner
                    }
                ]
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            _activityLogger.Log(userId, "Create", "Project", project.Id, $"Created project \"{project.Name}\" via API.", projectId: project.Id);
            await _context.SaveChangesAsync();

            var result = await _context.Projects
                .Include(item => item.Owner)
                .Include(item => item.Team)
                .Include(item => item.Tasks)
                .AsNoTracking()
                .FirstAsync(item => item.Id == project.Id);

            return CreatedAtAction(nameof(Get), new { id = project.Id }, ToDto(result));
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> Put(int id, ProjectUpdateDto request)
        {
            ValidateProjectDates(request);

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var project = await _context.Projects
                .Include(item => item.Members)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (project is null)
            {
                return NotFound();
            }

            if (!_accessService.CanManage(project, User))
            {
                return Forbid();
            }

            project.Name = request.Name;
            project.Description = request.Description;
            project.StartDate = request.StartDate;
            project.Deadline = request.Deadline;
            project.Status = request.Status;
            project.Priority = request.Priority;

            _activityLogger.Log(_userManager.GetUserId(User), "Update", "Project", project.Id, $"Updated project \"{project.Name}\" via API.", projectId: project.Id);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private void ValidateProjectDates(ProjectCreateDto request)
        {
            if (ProjectPlanningValidator.HasInvalidDateRange(request.StartDate, request.Deadline))
            {
                ModelState.AddModelError(nameof(request.Deadline), "Deadline cannot be before the start date.");
            }
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> Delete(int id)
        {
            var project = await _context.Projects
                .Include(item => item.Members)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (project is null)
            {
                return NotFound();
            }

            if (!_accessService.CanManage(project, User))
            {
                return Forbid();
            }

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private static ProjectDto ToDto(Project project)
        {
            return new ProjectDto(
                project.Id,
                project.Name,
                project.Description,
                project.OwnerId,
                $"{project.Owner.FirstName} {project.Owner.LastName}",
                project.CreatedAt,
                project.StartDate,
                project.Deadline,
                project.Status,
                project.Priority,
                project.TeamId,
                project.Team?.Name,
                project.Tasks.Count);
        }
    }
}
