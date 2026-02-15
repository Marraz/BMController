using System.ComponentModel.DataAnnotations;

namespace BMController.Models
{
    public class MoveTask
    {
        public int Id { get; set; }
        [Required]
        public string IP { get; set; }
        [Required]
        public string Source { get; set; }
        [Required]
        public string Destination { get; set; }

        public bool isCompleted { get; set; }

        public bool isCancelled { get; set; }

        public bool isFailed { get; set; }

        public double progress { get; set; }
    }
}
