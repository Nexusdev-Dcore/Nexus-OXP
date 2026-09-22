namespace NexusOXP.Services
{
    public interface IActivityLogger
    {
        void Log(
            string? actorId,
            string action,
            string entityType,
            int? entityId,
            string description,
            int? projectId = null,
            int? taskId = null,
            int? teamId = null);
    }
}
