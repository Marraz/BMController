using Microsoft.EntityFrameworkCore;
namespace BMController.Models
{
    public class MoveTaskContext : DbContext
    {
        public MoveTaskContext(DbContextOptions<MoveTaskContext> options) : base(options)
        {
        }
        public DbSet<MoveTask> MoveTasks { get; set; } = null!;
    }
}
