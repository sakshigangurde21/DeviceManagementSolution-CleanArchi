using DeviceManagementSolution.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceManagementSolution.Domain.Entities
{
    public class UserNotification
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int NotificationId { get; set; }
        public Notification Notification { get; set; } // EF navigation
        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }
    }
}
