using Application.Interfaces;
using System.Collections.Concurrent;

namespace Infrastructure.Services
{
    public class QueueService : IQueueService
    {
        private readonly ConcurrentQueue<string> _queue = new();

        // Hardcoded to only work with "Temperature"
        public void Enqueue()
        {
            string columnName = "Temperature"; // always this column
            Console.WriteLine($"{columnName} added to queue");
            _queue.Enqueue(columnName);
        }

        public bool TryDequeue(out string columnName)
        {
            return _queue.TryDequeue(out columnName);
        }
    }
}
