using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NexusOXP.Authorization;
using NexusOXP.Data;
using NexusOXP.Models;
using NexusOXP.Services;
using NexusOXP.ViewModels;

namespace NexusOXP.Controllers
{
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager},{AppRoles.Member}")]
    public class TeamsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IActivityLogger _activityLogger;

        public TeamsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IActivityLogger activityLogger)
        {
            _context = context;
            _userManager = userManager;
            _activityLogger = activityLogger;
        }

        public async Task<IActionResult> Index(string? q = null, string? managerId = null)
        {
            var teamsQuery = _context.Teams
                .Include(team => team.Manager)
                .Include(team => team.Members)
                .Include(team => team.Projects)
                .AsNoTracking();

            if (!CanManageTeams())
            {
                var userId = _userManager.GetUserId(User);
                teamsQuery = teamsQuery.Where(team =>
                    team.ManagerId == userId ||
                    team.Members.Any(member => member.UserId == userId));
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var query = q.Trim();
                teamsQuery = teamsQuery.Where(team =>
                    team.Name.Contains(query) ||
                    (team.Description != null && team.Description.Contains(query)) ||
                    team.Manager.FirstName.Contains(query) ||
                    team.Manager.LastName.Contains(query));
            }

            if (!string.IsNullOrWhiteSpace(managerId))
            {
                teamsQuery = teamsQuery.Where(team => team.ManagerId == managerId);
            }

            var teams = await teamsQuery
                .OrderBy(team => team.Name)
                .ToListAsync();

            ViewBag.CanManageTeams = CanManageTeams();
            ViewBag.Query = q;
            ViewBag.ManagerId = managerId;
            ViewBag.ManagerOptions = await _context.Teams
                .Include(team => team.Manager)
                .AsNoTracking()
                .Select(team => team.Manager)
                .Distinct()
                .OrderBy(user => user.FirstName)
                .ThenBy(user => user.LastName)
                .Select(user => new SelectListItem($"{user.FirstName} {user.LastName}", user.Id))
                .ToListAsync();
            return View(teams);
        }

        public async Task<IActionResult> Details(int id)
        {
            var team = await _context.Teams
                .Include(item => item.Manager)
                .Include(item => item.Members)
                    .ThenInclude(member => member.User)
                .Include(item => item.Projects)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);

            if (team is null)
            {
                return NotFound();
            }

            if (!CanView(team))
            {
                return Forbid();
            }

            var memberForm = new TeamMemberFormViewModel
            {
                TeamId = team.Id
            };

            await PopulateMemberOptions(memberForm, team);
            ViewBag.MemberForm = memberForm;
            ViewBag.ProjectOptions = await ProjectOptions(team.Id);
            ViewBag.CanManageTeam = CanManage(team);

            return View(team);
        }

        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> Create()
        {
            var model = new TeamFormViewModel();
            await PopulateOptions(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> Create(TeamFormViewModel model)
        {
            await ValidateManager(model.ManagerId);

            if (!ModelState.IsValid)
            {
                await PopulateOptions(model);
                return View(model);
            }

            var team = new Team
            {
                Name = model.Name,
                Description = model.Description,
                ManagerId = model.ManagerId,
                Members =
                [
                    new TeamMember
                    {
                        UserId = model.ManagerId,
                        Role = TeamMemberRole.Manager
                    }
                ]
            };

            _context.Teams.Add(team);
            await _context.SaveChangesAsync();

            _activityLogger.Log(
                _userManager.GetUserId(User),
                "Create",
                "Team",
                team.Id,
                $"Created team \"{team.Name}\".",
                teamId: team.Id);

            if (model.ProjectId.HasValue)
            {
                await AssignProjectToTeam(team.Id, model.ProjectId.Value);
            }
            else
            {
                await _context.SaveChangesAsync();
            }

            TempData["StatusMessage"] = "Team created successfully.";
            return RedirectToAction(nameof(Details), new { id = team.Id });
        }

        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> Edit(int id)
        {
            var team = await _context.Teams
                .Include(item => item.Members)
                .Include(item => item.Projects)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (team is null)
            {
                return NotFound();
            }

            if (!CanManage(team))
            {
                return Forbid();
            }

            var model = new TeamFormViewModel
            {
                Id = team.Id,
                Name = team.Name,
                Description = team.Description,
                ManagerId = team.ManagerId,
                ProjectId = team.Projects.Select(project => project.Id).FirstOrDefault()
            };

            if (model.ProjectId == 0)
            {
                model.ProjectId = null;
            }

            await PopulateOptions(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> Edit(int id, TeamFormViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            await ValidateManager(model.ManagerId);

            if (!ModelState.IsValid)
            {
                await PopulateOptions(model);
                return View(model);
            }

            var team = await _context.Teams
                .Include(item => item.Members)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (team is null)
            {
                return NotFound();
            }

            if (!CanManage(team))
            {
                return Forbid();
            }

            team.Name = model.Name;
            team.Description = model.Description;
            team.ManagerId = model.ManagerId;

            var managerMember = team.Members.FirstOrDefault(member => member.UserId == model.ManagerId);
            if (managerMember is null)
            {
                team.Members.Add(new TeamMember
                {
                    UserId = model.ManagerId,
                    Role = TeamMemberRole.Manager
                });
            }
            else
            {
                managerMember.Role = TeamMemberRole.Manager;
            }

            _activityLogger.Log(
                _userManager.GetUserId(User),
                "Update",
                "Team",
                team.Id,
                $"Updated team \"{team.Name}\".",
                teamId: team.Id);

            await _context.SaveChangesAsync();

            if (model.ProjectId.HasValue)
            {
                await AssignProjectToTeam(team.Id, model.ProjectId.Value);
            }

            TempData["StatusMessage"] = "Team updated successfully.";
            return RedirectToAction(nameof(Details), new { id = team.Id });
        }

        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> Delete(int id)
        {
            var team = await _context.Teams
                .Include(item => item.Manager)
                .Include(item => item.Members)
                .Include(item => item.Projects)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);

            if (team is null)
            {
                return NotFound();
            }

            if (!CanManage(team))
            {
                return Forbid();
            }

            return View(team);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var team = await _context.Teams
                .Include(item => item.Projects)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (team is null)
            {
                return NotFound();
            }

            if (!CanManage(team))
            {
                return Forbid();
            }

            foreach (var project in team.Projects)
            {
                project.TeamId = null;
            }

            _context.Teams.Remove(team);
            _activityLogger.Log(
                _userManager.GetUserId(User),
                "Delete",
                "Team",
                team.Id,
                $"Deleted team \"{team.Name}\".",
                teamId: team.Id);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Team deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> AddMember(TeamMemberFormViewModel model)
        {
            var team = await _context.Teams
                .Include(item => item.Members)
                .FirstOrDefaultAsync(item => item.Id == model.TeamId);

            if (team is null)
            {
                return NotFound();
            }

            if (!CanManage(team))
            {
                return Forbid();
            }

            await ValidateMemberUser(model.UserId);

            if (!ModelState.IsValid)
            {
                TempData["StatusMessage"] = "Select a valid user before adding a member.";
                return RedirectToAction(nameof(Details), new { id = model.TeamId });
            }

            if (team.Members.Any(member => member.UserId == model.UserId))
            {
                TempData["StatusMessage"] = "That user is already on this team.";
                return RedirectToAction(nameof(Details), new { id = model.TeamId });
            }

            team.Members.Add(new TeamMember
            {
                UserId = model.UserId,
                Role = model.Role
            });

            _activityLogger.Log(
                _userManager.GetUserId(User),
                "AddMember",
                "Team",
                team.Id,
                $"Added a member to team \"{team.Name}\".",
                teamId: team.Id);

            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Team member added.";
            return RedirectToAction(nameof(Details), new { id = model.TeamId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> RemoveMember(int teamId, int memberId)
        {
            var team = await _context.Teams
                .Include(item => item.Members)
                .FirstOrDefaultAsync(item => item.Id == teamId);

            if (team is null)
            {
                return NotFound();
            }

            if (!CanManage(team))
            {
                return Forbid();
            }

            var member = team.Members.FirstOrDefault(item => item.Id == memberId);
            if (member is null)
            {
                return NotFound();
            }

            if (member.UserId == team.ManagerId)
            {
                TempData["StatusMessage"] = "Change the manager before removing this member.";
                return RedirectToAction(nameof(Details), new { id = teamId });
            }

            _context.TeamMembers.Remove(member);
            _activityLogger.Log(
                _userManager.GetUserId(User),
                "RemoveMember",
                "Team",
                team.Id,
                $"Removed a member from team \"{team.Name}\".",
                teamId: team.Id);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Team member removed.";
            return RedirectToAction(nameof(Details), new { id = teamId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> AssignProject(int teamId, int projectId)
        {
            var team = await _context.Teams
                .Include(item => item.Members)
                .FirstOrDefaultAsync(item => item.Id == teamId);

            if (team is null)
            {
                return NotFound();
            }

            if (!CanManage(team))
            {
                return Forbid();
            }

            await AssignProjectToTeam(teamId, projectId);

            TempData["StatusMessage"] = "Project assigned to team.";
            return RedirectToAction(nameof(Details), new { id = teamId });
        }

        private async Task AssignProjectToTeam(int teamId, int projectId)
        {
            var project = await _context.Projects.FindAsync(projectId);
            if (project is null)
            {
                return;
            }

            project.TeamId = teamId;
            _activityLogger.Log(
                _userManager.GetUserId(User),
                "AssignProject",
                "Team",
                teamId,
                $"Assigned project \"{project.Name}\" to a team.",
                projectId: project.Id,
                teamId: teamId);
            await _context.SaveChangesAsync();
        }

        private async Task ValidateManager(string managerId)
        {
            if (string.IsNullOrWhiteSpace(managerId) ||
                !await _context.Users.AnyAsync(user => user.Id == managerId && user.IsActive))
            {
                ModelState.AddModelError(nameof(TeamFormViewModel.ManagerId), "Select a valid active manager.");
            }
        }

        private async Task ValidateMemberUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId) ||
                !await _context.Users.AnyAsync(user => user.Id == userId && user.IsActive))
            {
                ModelState.AddModelError(nameof(TeamMemberFormViewModel.UserId), "Select a valid active user.");
            }
        }

        private async Task PopulateOptions(TeamFormViewModel model)
        {
            model.UserOptions = await UserOptions();
            model.ProjectOptions = await ProjectOptions(model.Id);
        }

        private async Task PopulateMemberOptions(TeamMemberFormViewModel model, Team team)
        {
            var assignedIds = team.Members.Select(member => member.UserId).ToHashSet();
            var users = await _context.Users
                .AsNoTracking()
                .Where(user => user.IsActive && !assignedIds.Contains(user.Id))
                .OrderBy(user => user.FirstName)
                .ThenBy(user => user.LastName)
                .ToListAsync();

            model.UserOptions = users
                .Select(user => new SelectListItem($"{user.FirstName} {user.LastName} ({user.Email})", user.Id))
                .ToList();
        }

        private async Task<IReadOnlyList<SelectListItem>> UserOptions()
        {
            var users = await _context.Users
                .AsNoTracking()
                .Where(user => user.IsActive)
                .OrderBy(user => user.FirstName)
                .ThenBy(user => user.LastName)
                .ToListAsync();

            return users
                .Select(user => new SelectListItem($"{user.FirstName} {user.LastName} ({user.Email})", user.Id))
                .ToList();
        }

        private async Task<IReadOnlyList<SelectListItem>> ProjectOptions(int teamId)
        {
            var projects = await _context.Projects
                .AsNoTracking()
                .Where(project => project.TeamId == null || project.TeamId == teamId)
                .OrderBy(project => project.Name)
                .ToListAsync();

            return projects
                .Select(project => new SelectListItem(project.Name, project.Id.ToString()))
                .ToList();
        }

        private bool CanView(Team team)
        {
            var userId = _userManager.GetUserId(User);

            return CanManage(team) ||
                team.ManagerId == userId ||
                team.Members.Any(member => member.UserId == userId);
        }

        private bool CanManage(Team team)
        {
            var userId = _userManager.GetUserId(User);

            return User.IsInRole(AppRoles.Admin) ||
                User.IsInRole(AppRoles.Manager) ||
                team.ManagerId == userId;
        }

        private bool CanManageTeams()
        {
            return User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager);
        }
    }
}
