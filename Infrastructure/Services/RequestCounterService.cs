namespace Infrastructure.Services
{
    using System.Collections.Concurrent;

    // This is just a plain class (a service)
    public class RequestCounterService
    {
        // Dictionary to hold request counts
        public ConcurrentDictionary<string, int> RequestCounts { get; } = new();
    }
}
