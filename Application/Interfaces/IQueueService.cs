namespace Application.Interfaces
{
    public interface IQueueService
    {
        void Enqueue(string columnName);
        bool TryDequeue(out string columnName);
    }
}
