namespace NexusOXP.Services
{
    public static class ProjectPlanningValidator
    {
        public static bool HasInvalidDateRange(DateTime? startDate, DateTime? deadline)
        {
            return startDate.HasValue &&
                deadline.HasValue &&
                deadline.Value.Date < startDate.Value.Date;
        }

        public static bool IsPastDueDate(DateTime? dueDate, DateTime todayUtc)
        {
            return dueDate.HasValue && dueDate.Value.Date < todayUtc.Date;
        }
    }
}
