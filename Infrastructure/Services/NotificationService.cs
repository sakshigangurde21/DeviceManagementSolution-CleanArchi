using Infrastructure.Data;
using DeviceManagementSolution.Domain.Entities;
using Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        private readonly DeviceDbContext _context;

        public NotificationService(DeviceDbContext context)
        {
            _context = context;
        }

        public async Task<List<Notification>> GetNotificationsForUserAsync(string userId, string role)
        {
            IQueryable<Notification> query = _context.Notifications;

            if (role != "Admin")
                query = query.Where(n => n.UserId == userId);

            return await query.OrderByDescending(n => n.CreatedAt).ToListAsync();
        }

        public async Task<bool> MarkAsReadAsync(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);
            if (notification == null) return false;

            notification.IsRead = true;
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task CreateNotificationAsync(string userId, string message)
        {
            var notification = new Notification
            {
                UserId = userId,
                Message = message,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }
    }
}
