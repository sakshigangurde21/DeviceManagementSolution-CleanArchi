using System;

namespace DeviceManagementSolution.Domain.Entities
{
    public class Notification
    {
        public int Id { get; set; }
        public string Message { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // NEW: Track whether the notification has been read
        public bool IsRead { get; set; } = false;
    }
}
