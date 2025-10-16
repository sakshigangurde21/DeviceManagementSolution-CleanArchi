using System;

namespace DeviceManagementSolution.Domain.Entities
{
    public class RefreshToken
    {
        public int Id { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime Expires { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Revoked { get; set; } // optional, still useful to track revocation

        // Foreign Key → User
        public int UserId { get; set; }
        public User? User { get; set; }

        public bool IsActive => Revoked == null && !IsExpired;
        public bool IsExpired => DateTime.UtcNow >= Expires;
    }
}
