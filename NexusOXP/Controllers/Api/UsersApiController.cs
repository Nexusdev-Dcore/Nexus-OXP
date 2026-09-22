using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexusOXP.Authorization;
using NexusOXP.Data;
using NexusOXP.ViewModels;

namespace NexusOXP.Controllers.Api
{
    [ApiController]
    [Route("api/users")]
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public class UsersApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UsersApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<UserDto>>> Get()
        {
            var users = await _context.Users
                .AsNoTracking()
                .OrderBy(user => user.FirstName)
                .ThenBy(user => user.LastName)
                .Select(user => new UserDto(
                    user.Id,
                    user.FirstName + " " + user.LastName,
                    user.Email,
                    user.IsActive,
                    user.CreatedAt))
                .ToListAsync();

            return Ok(users);
        }
    }
}
