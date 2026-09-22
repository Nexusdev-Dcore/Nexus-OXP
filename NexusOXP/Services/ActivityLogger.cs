using NexusOXP.Data;
using NexusOXP.Models;

namespace NexusOXP.Services
{
    public class ActivityLogger : IActivityLogger
    {
        private readonly ApplicationDbContext _context;

        public ActivityLogger(ApplicationDbContext context)
        {
            _context = context;
        }

        public void Log(
            string? actorId,
            string action,
            string entityType,
            int? entityId,
            string description,
            int? projectId = null,
            int? taskId = null,
            int? teamId = null)
        {
            _context.ActivityLogs.Add(new ActivityLog
            {
                ActorId = actorId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Description = description,
                ProjectId = projectId,
                TaskId = taskId,
                TeamId = teamId
            });
        }
    }
}
