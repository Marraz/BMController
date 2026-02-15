using System.ComponentModel.DataAnnotations;
namespace BMController.Models
{
    public class MoveTaskInput
    {
        public int Id { get; set; }
        [Required]
        public string IP { get; set; }
        [Required]
        public string Source { get; set; }
        [Required]
        public string Destination { get; set; }
    }
}
