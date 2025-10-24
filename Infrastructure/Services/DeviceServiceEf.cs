using Infrastructure.Data;
using DeviceManagementSolution.Domain.Entities;
using Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Infrastructure.Services
{
    public class DeviceServiceEf : IDeviceService
    {
        private readonly DeviceDbContext _context;

        public DeviceServiceEf(DeviceDbContext context)
        {
            _context = context;
        }

        // Get only active devices
        public List<Device> GetAllDevices()
        {
            return _context.Devices.Where(d => !d.IsDeleted).ToList();
        }

        public List<Device> GetAllDevicesIncludingDeleted()
        {
            return _context.Devices.IgnoreQueryFilters().ToList();
        }

        // Get only soft-deleted devices
        public List<Device> GetDeletedDevices()
        {
            return _context.Devices
                           .IgnoreQueryFilters()
                           .Where(d => d.IsDeleted)
                           .ToList();
        }

        public Device? GetDeviceById(int id)
        {
            return _context.Devices.IgnoreQueryFilters().FirstOrDefault(d => d.Id == id);
        }

        public bool CreateDevice(Device device)
        {
            _context.Devices.Add(device);
            return _context.SaveChanges() > 0;
        }

        public bool UpdateDevice(Device device)
        {
            var existing = _context.Devices.FirstOrDefault(d => d.Id == device.Id);
            if (existing == null) return false;

            existing.DeviceName = device.DeviceName;
            existing.Description = device.Description;
            return _context.SaveChanges() > 0;
        }

        // Soft delete
        public bool SoftDeleteDevice(int id)
        {
            var device = _context.Devices.FirstOrDefault(d => d.Id == id && !d.IsDeleted);
            if (device == null) return false;

            device.IsDeleted = true;
            _context.SaveChanges(); // make sure this is called

            return true; // no need to return _context.SaveChanges() > 0 here
        }

        // Restore one device
        public bool RestoreDevice(int id)
        {
            var device = _context.Devices
                                 .IgnoreQueryFilters()
                                 .FirstOrDefault(d => d.Id == id && d.IsDeleted);
            if (device == null) return false;

            device.IsDeleted = false;
            return _context.SaveChanges() > 0;
        }


        // Restore all deleted devices
        public bool RestoreAllDeletedDevices()
        {
            var deletedDevices = _context.Devices
                                         .IgnoreQueryFilters()
                                         .Where(d => d.IsDeleted)
                                         .ToList();
            if (!deletedDevices.Any()) return false;

            foreach (var device in deletedDevices)
                device.IsDeleted = false;

            return _context.SaveChanges() > 0;
        }


        // Permanent delete
        public bool PermanentDeleteDevice(int id)
        {
            var device = _context.Devices.FirstOrDefault(d => d.Id == id);
            if (device == null) return false;

            _context.Devices.Remove(device);
            return _context.SaveChanges() > 0;
        }

        public (List<Device> Devices, int TotalCount) GetDevicesPagination(int pageNumber, int pageSize, bool includeDeleted = false)
        {
            IQueryable<Device> query;

            if (includeDeleted)
                query = _context.Devices.IgnoreQueryFilters(); // include deleted
            else
                query = _context.Devices; // global filter automatically hides deleted

            int totalCount = query.Count();

            var devices = query
                .OrderBy(d => d.Id) // ensure consistent ordering
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (devices, totalCount);
        }
    }
    }
