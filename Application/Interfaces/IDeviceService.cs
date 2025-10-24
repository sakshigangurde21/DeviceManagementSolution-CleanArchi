using DeviceManagementSolution.Domain.Entities;
using System.Collections.Generic;

namespace Application.Interfaces
{
    public interface IDeviceService
    {
        List<Device> GetAllDevices(); // Only active devices
        List<Device> GetDeletedDevices(); // Only deleted devices

        Device? GetDeviceById(int id);
        bool CreateDevice(Device device);
        bool UpdateDevice(Device device);

        // Soft delete
        bool SoftDeleteDevice(int id);

        // Restore
        bool RestoreDevice(int id);
        bool RestoreAllDeletedDevices();

        // Optional hard delete if needed
        bool PermanentDeleteDevice(int id);

        // Pagination (optional)
        (List<Device> Devices, int TotalCount) GetDevicesPagination(int pageNumber, int pageSize, bool includeDeleted = false);

        List<Device> GetAllDevicesIncludingDeleted();

    }
}
