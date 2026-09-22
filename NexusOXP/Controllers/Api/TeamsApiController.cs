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
    [Route("api/teams")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager},{AppRoles.Member}")]
    public class TeamsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProjectAccessService _accessService;

        public TeamsApiController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IProjectAccessService accessService)
        {
            _context = context;
            _userManager = userManager;
            _accessService = accessService;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<TeamDto>>> Get()
        {
            var userId = _userManager.GetUserId(User);
            var teamsQuery = _context.Teams
                .Include(team => team.Manager)
                .Include(team => team.Members)
                .Include(team => team.Projects)
                .AsNoTracking();

            if (!_accessService.CanManageTeams(User))
            {
                teamsQuery = teamsQuery.Where(team =>
                    team.ManagerId == userId ||
                    team.Members.Any(member => member.UserId == userId));
            }

            var teams = await teamsQuery
                .OrderBy(team => team.Name)
                .Select(team => new TeamDto(
                    team.Id,
                    team.Name,
                    team.Description,
                    team.ManagerId,
                    team.Manager.FirstName + " " + team.Manager.LastName,
                    team.Members.Count,
                    team.Projects.Count))
                .ToListAsync();

            return Ok(teams);
        }
    }
}
