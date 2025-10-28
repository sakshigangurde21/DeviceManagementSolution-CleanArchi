using Application.Interfaces;
using System.Collections.Concurrent;

namespace Infrastructure.Services
{
    public class QueueService : IQueueService
    {
        private readonly ConcurrentQueue<string> _queue = new();

        public void Enqueue(string columnName)
        {
            Console.WriteLine($"{columnName} added to queue");
            _queue.Enqueue(columnName);
        }

        public bool TryDequeue(out string columnName)
        {
            return _queue.TryDequeue(out columnName);
        }
    }
}
