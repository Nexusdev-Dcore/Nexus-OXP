using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NexusOXP.Models;

namespace NexusOXP.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Project> Projects => Set<Project>();

        public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

        public DbSet<ProjectTask> Tasks => Set<ProjectTask>();

        public DbSet<Team> Teams => Set<Team>();

        public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

        public DbSet<Comment> Comments => Set<Comment>();

        public DbSet<Notification> Notifications => Set<Notification>();

        public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

        public DbSet<TaskAttachment> TaskAttachments => Set<TaskAttachment>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Project>(entity =>
            {
                entity.Property(project => project.Name)
                    .HasMaxLength(150)
                    .IsRequired();

                entity.Property(project => project.Description)
                    .HasMaxLength(2000);

                entity.HasOne(project => project.Owner)
                    .WithMany()
                    .HasForeignKey(project => project.OwnerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(project => project.Team)
                    .WithMany(team => team.Projects)
                    .HasForeignKey(project => project.TeamId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<ProjectMember>(entity =>
            {
                entity.HasIndex(member => new { member.ProjectId, member.UserId })
                    .IsUnique();

                entity.HasOne(member => member.Project)
                    .WithMany(project => project.Members)
                    .HasForeignKey(member => member.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(member => member.User)
                    .WithMany()
                    .HasForeignKey(member => member.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ProjectTask>(entity =>
            {
                entity.ToTable("Tasks");

                entity.Property(task => task.Title)
                    .HasMaxLength(180)
                    .IsRequired();

                entity.Property(task => task.Description)
                    .HasMaxLength(3000);

                entity.HasOne(task => task.Project)
                    .WithMany(project => project.Tasks)
                    .HasForeignKey(task => task.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(task => task.AssignedUser)
                    .WithMany()
                    .HasForeignKey(task => task.AssignedUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(task => task.CreatedBy)
                    .WithMany()
                    .HasForeignKey(task => task.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<TaskAttachment>(entity =>
            {
                entity.Property(attachment => attachment.FileName)
                    .HasMaxLength(260)
                    .IsRequired();

                entity.Property(attachment => attachment.StoredFileName)
                    .HasMaxLength(260)
                    .IsRequired();

                entity.Property(attachment => attachment.ContentType)
                    .HasMaxLength(120)
                    .IsRequired();

                entity.HasIndex(attachment => attachment.TaskId);

                entity.HasOne(attachment => attachment.Task)
                    .WithMany(task => task.Attachments)
                    .HasForeignKey(attachment => attachment.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(attachment => attachment.UploadedBy)
                    .WithMany()
                    .HasForeignKey(attachment => attachment.UploadedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Comment>(entity =>
            {
                entity.Property(comment => comment.Content)
                    .HasMaxLength(2000)
                    .IsRequired();

                entity.HasOne(comment => comment.Task)
                    .WithMany(task => task.Comments)
                    .HasForeignKey(comment => comment.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(comment => comment.Author)
                    .WithMany()
                    .HasForeignKey(comment => comment.AuthorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Notification>(entity =>
            {
                entity.Property(notification => notification.Title)
                    .HasMaxLength(160)
                    .IsRequired();

                entity.Property(notification => notification.Message)
                    .HasMaxLength(500)
                    .IsRequired();

                entity.Property(notification => notification.LinkUrl)
                    .HasMaxLength(300);

                entity.HasOne(notification => notification.User)
                    .WithMany()
                    .HasForeignKey(notification => notification.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ActivityLog>(entity =>
            {
                entity.Property(activity => activity.Action)
                    .HasMaxLength(80)
                    .IsRequired();

                entity.Property(activity => activity.EntityType)
                    .HasMaxLength(80)
                    .IsRequired();

                entity.Property(activity => activity.Description)
                    .HasMaxLength(500)
                    .IsRequired();

                entity.HasIndex(activity => activity.CreatedAt);

                entity.HasIndex(activity => activity.ProjectId);

                entity.HasIndex(activity => activity.TaskId);

                entity.HasIndex(activity => activity.TeamId);

                entity.HasOne(activity => activity.Actor)
                    .WithMany()
                    .HasForeignKey(activity => activity.ActorId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<Team>(entity =>
            {
                entity.Property(team => team.Name)
                    .HasMaxLength(150)
                    .IsRequired();

                entity.Property(team => team.Description)
                    .HasMaxLength(2000);

                entity.HasOne(team => team.Manager)
                    .WithMany()
                    .HasForeignKey(team => team.ManagerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<TeamMember>(entity =>
            {
                entity.HasIndex(member => new { member.TeamId, member.UserId })
                    .IsUnique();

                entity.HasOne(member => member.Team)
                    .WithMany(team => team.Members)
                    .HasForeignKey(member => member.TeamId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(member => member.User)
                    .WithMany()
                    .HasForeignKey(member => member.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
