using Application.Interfaces;
using DeviceManagementSolution.Domain.Entities;
using Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Hubs;

namespace Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        private readonly DeviceDbContext _context;
        private readonly IHubContext<DeviceHub> _hubContext;
        public NotificationService(DeviceDbContext context, IHubContext<DeviceHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // Get notifications for a user (with read/unread info)
        public async Task<List<UserNotification>> GetNotificationsForUserAsync(string userId, string role)
        {
            IQueryable<UserNotification> query = _context.UserNotifications
                .Include(un => un.Notification)
                .OrderByDescending(un => un.Notification.CreatedAt);

            if (role != "Admin")
                query = query.Where(un => un.UserId == userId);

            return await query.ToListAsync();
        }

        // Mark single notification as read for the user
        public async Task<bool> MarkAsReadAsync(int userNotificationId)
        {
            var userNotification = await _context.UserNotifications.FindAsync(userNotificationId);
            if (userNotification == null) return false;

            userNotification.IsRead = true;
            userNotification.ReadAt = DateTime.UtcNow;

            return await _context.SaveChangesAsync() > 0;
        }

        // Create notification for a specific user
        public async Task CreateNotificationAsync(string userId, string message)
        {
            var notification = new Notification
            {
                Message = message,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            var userNotification = new UserNotification
            {
                UserId = userId,
                NotificationId = notification.Id,
                IsRead = false
            };

            _context.UserNotifications.Add(userNotification);
            await _context.SaveChangesAsync();
        }

        // Optional: Mark all notifications as read for a user
        public async Task<bool> MarkAllAsReadAsync(string userId)
        {
            var unreadNotifications = await _context.UserNotifications
                                                    .Where(un => un.UserId == userId && !un.IsRead)
                                                    .ToListAsync();

            unreadNotifications.ForEach(un => {
                un.IsRead = true;
                un.ReadAt = DateTime.UtcNow;
            });

            return await _context.SaveChangesAsync() > 0;
        }

        public async Task SendAverageToClients(string columnName, double average)
        {
            // Broadcast to all connected clients via SignalR
            await _hubContext.Clients.All.SendAsync("ReceiveAverage", new
            {
                Column = columnName,
                Average = average
            });
        }
    }
}
