using System.ComponentModel.DataAnnotations;

namespace NexusOXP.ViewModels
{
    public class CommentFormViewModel
    {
        public int Id { get; set; }

        public int TaskId { get; set; }

        [Required]
        [StringLength(2000)]
        [Display(Name = "Comment")]
        public string Content { get; set; } = string.Empty;
    }
}
