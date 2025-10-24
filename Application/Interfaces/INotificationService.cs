using DeviceManagementSolution.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface INotificationService
    {
        // Get notifications for a user (returns per-user notification info)
        Task<List<UserNotification>> GetNotificationsForUserAsync(string userId, string role);

        // Mark a single user notification as read
        Task<bool> MarkAsReadAsync(int userNotificationId);

        // Create a notification for a specific user
        Task CreateNotificationAsync(string userId, string message);

        // Optional: mark all notifications as read for a user
        Task<bool> MarkAllAsReadAsync(string userId);

        Task SendAverageToClients(string columnName, double average);

    }
}
