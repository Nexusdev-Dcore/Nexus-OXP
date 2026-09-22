using Microsoft.AspNetCore.Identity;

namespace NexusOXP.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string? ProfileImage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        // Password reset support
        public string? PasswordResetCode { get; set; }

        public DateTimeOffset? PasswordResetCodeExpiresAt { get; set; }
    }
}
