using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace DeviceManagementSolution.Domain.Entities
{
    public class Device
    {
        [BindNever]  // prevents Swagger/Model binding from showing/accepting it
        public int Id { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false;  // soft delete flag

        [BindNever] // this will store which user added the device
        public string UserId { get; set; } = string.Empty;
    }
}
