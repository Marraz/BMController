using BMController.Models;
using FluentFTP;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace BMController.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MoveTasksController : ControllerBase
    {
        private readonly MoveTaskContext _context;
        private Queue<int> tasksInProgress;
        private Task? processingTask;
        private double progress;
        private CancellationToken currentCancelation;
        private int currentTaskId;

        public MoveTasksController(MoveTaskContext context)
        {
            _context = context;
            tasksInProgress = new Queue<int>();
        }

        // GET: api/MoveTasks
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MoveTask>>> GetMoveTasks()
        {
            return await _context.MoveTasks.ToListAsync();
        }

        // GET: api/MoveTasks/5
        [HttpGet("{id}")]
        public async Task<ActionResult<MoveTask>> GetMoveTask(int id)
        {
            var moveTask = await _context.MoveTasks.FindAsync(id);

            if (moveTask == null)
            {
                return NotFound();
            }

            if (currentTaskId == id)
            {
                moveTask.progress = this.progress;
                moveTask.isCancelled |= currentCancelation.IsCancellationRequested;
            }

            return moveTask;
        }

        // PUT: api/MoveTasks/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutMoveTask(int id, MoveTaskInput moveTask)
        {
            if (id != moveTask.Id)
            {
                return BadRequest();
            }

            var newMoveTask = new MoveTask
            {
                Id = id,
                IP = moveTask.IP,
                Source = moveTask.Source,
                Destination = moveTask.Destination,
                isCompleted = false,
                isCancelled = false,
                isFailed = false,
                progress = 0
            };

            _context.Entry(newMoveTask).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MoveTaskExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return Ok();
        }

        // POST: api/MoveTasks
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<MoveTask>> PostMoveTask(MoveTaskInput moveTask)
        {
            if (_context.MoveTasks.Any(e => e.Id == moveTask.Id))
            {
                return Conflict("A task with the same ID already exists.");
            }

            var newMoveTask = new MoveTask
            {
                Id = moveTask.Id,
                IP = moveTask.IP,
                Source = moveTask.Source,
                Destination = moveTask.Destination,
                isCompleted = false,
                isCancelled = false,
                isFailed = false,
                progress = 0
            };

            _context.MoveTasks.Add(newMoveTask);
            await _context.SaveChangesAsync();

            lock (this.tasksInProgress)
            {
                this.tasksInProgress.Enqueue(moveTask.Id);
                if (this.processingTask == null || this.processingTask.IsCompleted)
                {
                    this.processingTask = ProcessPendingTasksAsync();
                }
            }

            return CreatedAtAction(nameof(GetMoveTask), new { id = moveTask.Id }, moveTask);
        }

        // DELETE: api/MoveTasks/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMoveTask(int id)
        {
            var moveTask = await _context.MoveTasks.FindAsync(id);
            if (moveTask == null)
            {
                return NotFound();
            }

            _context.MoveTasks.Remove(moveTask);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool MoveTaskExists(int id)
        {
            return _context.MoveTasks.Any(e => e.Id == id);
        }

        private async Task ProcessPendingTasksAsync()
        {
            IServiceScopeFactory serviceScopeFactory = HttpContext.RequestServices.GetService<IServiceScopeFactory>();
            while (true)
            {
                lock (this.tasksInProgress)
                {
                    if (this.tasksInProgress.Count == 0)
                    {
                        break;
                    }
                    currentTaskId = this.tasksInProgress.Dequeue();
                }

                using (var currentContext = serviceScopeFactory?.CreateAsyncScope().ServiceProvider.GetRequiredService<MoveTaskContext>())
                {
                    var moveTask = await currentContext.MoveTasks.FindAsync(currentTaskId);
                    if (moveTask != null)
                    {
                        // Basic parse (accepts IPv4 and IPv6)
                        if (!IPAddress.TryParse(moveTask.IP, out var address))
                        {
                            moveTask.isFailed = true;
                            _context.Entry(moveTask).State = EntityState.Modified;
                            await _context.SaveChangesAsync();
                            continue;
                        }
                        Console.WriteLine($"Processing move task {moveTask.Id} for IP {moveTask.IP} from {moveTask.Source} to {moveTask.Destination}");
                        currentCancelation = new CancellationTokenSource().Token;
                        var client = new AsyncFtpClient(moveTask.IP);

                        await client.AutoConnect(currentCancelation);

                        IProgress<FtpProgress> progress = new Progress<FtpProgress>(p =>
                        {
                            this.progress = p.Progress;
                        });

                        var status = await client.DownloadFile(moveTask.Destination, moveTask.Source, FtpLocalExists.Overwrite, FtpVerify.Retry, progress, currentCancelation);
                        
                        switch(status)
                        {
                            case FtpStatus.Success:
                                Console.WriteLine($"Move task {moveTask.Id} completed successfully.");
                                break;
                            case FtpStatus.Failed:
                                Console.WriteLine($"Move task {moveTask.Id} failed.");
                                moveTask.isFailed = true;
                                break;
                            case FtpStatus.Skipped:
                                Console.WriteLine($"Move task {moveTask.Id} was skipped.");
                                moveTask.isFailed = true;
                                break;
                        }

                        moveTask.isCompleted = true;
                        moveTask.progress = this.progress;

                        await client.Disconnect();

                        currentContext.Entry(moveTask).State = EntityState.Modified;
                        await currentContext.SaveChangesAsync();
                        Console.WriteLine($"Move task {moveTask.Id} completed and saved to database.");

                    }
                }
            }
        }
    }
}
