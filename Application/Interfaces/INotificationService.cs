using DeviceManagementSolution.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface INotificationService
    {
        Task<List<Notification>> GetNotificationsForUserAsync(string userId, string role);
        Task<bool> MarkAsReadAsync(int id);
        Task CreateNotificationAsync(string userId, string message);

    }
}
