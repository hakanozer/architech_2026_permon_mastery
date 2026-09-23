using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestApi.Models
{
    public class Note
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string Content { get; set; } = string.Empty;
        
        [Required]
        public int UserId { get; set; }
        
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;
    }
}