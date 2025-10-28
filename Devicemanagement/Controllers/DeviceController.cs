using Application.DTO;
using Application.Interfaces;
using DeviceManagementSolution.Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Hubs;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims; 
using System.Threading.Tasks;

[Authorize] // Require authentication for all endpoints by default
[Route("api/[controller]")]
[ApiController]
[Produces("application/json")]
public class DeviceController : ControllerBase
{
    private readonly IDeviceService _deviceService;
    private readonly IHubContext<DeviceHub> _hubContext;
    private readonly ILogger<DeviceController> _logger;
    private readonly DeviceDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IQueueService _queueService;


    public DeviceController(IDeviceService deviceService, IHubContext<DeviceHub> hubContext, ILogger<DeviceController> logger, DeviceDbContext context, INotificationService notificationService, IQueueService queueService)
    {
        _deviceService = deviceService;
        _hubContext = hubContext;
        _logger = logger;
        _context = context;
        _notificationService = notificationService;
        _queueService = queueService;

    }


    // GET: api/Device
    [HttpGet]
    [Authorize(Roles = "Admin,User")]
    public IActionResult GetAll([FromQuery] bool deleted = false,
                                [FromQuery] string? searchDescription = null,
                                [FromQuery] string? searchUsername = null,
                                [FromQuery] string? createdByUserId = null,
                                [FromQuery] string? sortBy = null)
    {
        try
        {
            var userId = User.FindFirst("UserId")?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(role))
                return Unauthorized(new { message = "Invalid token: UserId or Role missing" });

            // Base query
            var devicesQuery = deleted
                ? _context.Devices.IgnoreQueryFilters().Where(d => d.IsDeleted)
                : _context.Devices.Where(d => !d.IsDeleted);
            // Filter deleted
            devicesQuery = deleted ? devicesQuery.Where(d => d.IsDeleted) : devicesQuery.Where(d => !d.IsDeleted);

            // Non-admin users see only their devices
            if (role != "Admin")
                devicesQuery = devicesQuery.Where(d => d.UserId == userId);

            // Filters
            if (!string.IsNullOrEmpty(searchDescription))
                devicesQuery = devicesQuery.Where(d => d.Description.Contains(searchDescription));

            if (!string.IsNullOrEmpty(createdByUserId))
                devicesQuery = devicesQuery.Where(d => d.UserId == createdByUserId);

            // Join with users to get username for filtering/sorting
            var result = from d in devicesQuery
                         join u in _context.Users on d.UserId equals u.Id.ToString() into userJoin
                         from user in userJoin.DefaultIfEmpty()
                         select new
                         {
                             d.Id,
                             d.DeviceName,
                             d.Description,
                             d.UserId,
                             CreatedBy = user != null ? user.Username : "Unknown",
                             d.IsDeleted
                         };

            // Filter by username
            if (!string.IsNullOrEmpty(searchUsername))
                result = result.Where(r => r.CreatedBy.Contains(searchUsername));

            // Sorting
            result = sortBy switch
            {
                "usernameAsc" => result.OrderBy(r => r.CreatedBy),
                "usernameDesc" => result.OrderByDescending(r => r.CreatedBy),
                _ => result.OrderBy(r => r.DeviceName)
            };

            return Ok(result.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while fetching devices");
            return StatusCode(500, new { message = "Unexpected error while fetching devices." });
        }
    }

    // GET: api/Device/{id}
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,User")]
    public IActionResult GetById(int id)
    {
        try
        {
            var device = _deviceService.GetDeviceById(id);
            if (device == null)
                return NotFound(new { message = $"Device with ID {id} not found." });

            return Ok(device);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while fetching device by ID");
            return StatusCode(500, new { message = "Unexpected error while fetching device." });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> Create([FromBody] CreateDeviceDto dto)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var trimmedName = dto.DeviceName?.Trim() ?? "";

            var allDevices = _deviceService.GetAllDevicesIncludingDeleted();
            if (allDevices.Any(d => d.DeviceName.Trim().ToLower() == trimmedName.ToLower()))
                return BadRequest(new { message = "Device name already exists" });

            var userId = User.FindFirst("UserId")?.Value ?? "";

            var device = new Device
            {
                DeviceName = trimmedName,
                Description = string.IsNullOrWhiteSpace(dto.Description) || dto.Description.Trim().ToLower() == "string"
                    ? "No description"
                    : dto.Description.Trim(),
                UserId = userId
            };

            bool success = _deviceService.CreateDevice(device);
            if (!success) return StatusCode(500, new { message = "Failed to create device" });

            await _hubContext.Clients.All.SendAsync("DeviceAdded", new
            {
                DeviceId = device.Id,
                DeviceName = device.DeviceName,
                AddedBy = User.Identity?.Name,
                UserId = device.UserId
            });


            // ✅ CREATE NOTIFICATION FOR ALL USERS
            var notification = new Notification
            {
                Message = $"{User.Identity?.Name} added device \"{device.DeviceName}\""
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // ✅ Get ALL users and create UserNotification for each
            var allUsers = await _context.Users.ToListAsync();
            foreach (var user in allUsers)
            {
                _context.UserNotifications.Add(new UserNotification
                {
                    UserId = user.Id.ToString(),
                    NotificationId = notification.Id,
                    IsRead = false
                });
            }
            await _context.SaveChangesAsync();

            // ✅ Send SignalR notification to ALL connected clients
            await _hubContext.Clients.All.SendAsync("NewNotification", new
            {
                id = notification.Id,
                message = notification.Message,
                createdAt = notification.CreatedAt,
                isRead = false
            });

            return CreatedAtAction(nameof(GetById), new { id = device.Id }, device);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating device");
            return StatusCode(500, new { message = "Unexpected error" });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDeviceDto dto)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var trimmedName = dto.DeviceName?.Trim() ?? "";

            var allDevices = _deviceService.GetAllDevices();
            if (allDevices.Any(d => d.DeviceName.Trim().ToLower() == trimmedName.ToLower() && d.Id != id))
                return BadRequest(new { message = "Device name already exists" });

            var userId = User.FindFirst("UserId")?.Value ?? "";

            var device = new Device
            {
                Id = id,
                DeviceName = trimmedName,
                Description = string.IsNullOrWhiteSpace(dto.Description) || dto.Description.Trim().ToLower() == "string"
                    ? "No description"
                    : dto.Description.Trim()
            };

            bool success = _deviceService.UpdateDevice(device);
            if (!success) return NotFound(new { message = $"Device {id} not found" });

            await _hubContext.Clients.All.SendAsync("DeviceUpdated", new
            {
                DeviceId = device.Id,
                DeviceName = device.DeviceName,
                UpdatedBy = User.Identity?.Name,
                UserId = device.UserId
            });

            // ✅ CREATE NOTIFICATION FOR ALL USERS
            var notification = new Notification
            {
                Message = $"{User.Identity?.Name} updated device \"{device.DeviceName}\""
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // ✅ Get ALL users
            var allUsers = await _context.Users.ToListAsync();
            foreach (var user in allUsers)
            {
                _context.UserNotifications.Add(new UserNotification
                {
                    UserId = user.Id.ToString(),
                    NotificationId = notification.Id,
                    IsRead = false
                });
            }
            await _context.SaveChangesAsync();

            // ✅ Send to ALL clients
            await _hubContext.Clients.All.SendAsync("NewNotification", new
            {
                id = notification.Id,
                message = notification.Message,
                createdAt = notification.CreatedAt,
                isRead = false
            });

            return Ok(new { message = $"Device {id} updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating device");
            return StatusCode(500, new { message = "Unexpected error" });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var device = await _context.Devices.FindAsync(id);
            if (device == null) return NotFound(new { message = $"Device {id} not found" });

            var deviceName = device.DeviceName; // Store name before deletion

            bool success = _deviceService.SoftDeleteDevice(id);
            if (!success) return NotFound(new { message = $"Device {id} not found" });

            await _hubContext.Clients.All.SendAsync("DeviceDeleted", new
            {
                DeviceId = id,
                Message = "Device deleted"
            });
            // ✅ CREATE NOTIFICATION FOR ALL USERS
            var notification = new Notification
            {
                Message = $"{User.Identity?.Name} deleted device \"{device.DeviceName}\""
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // ✅ Get ALL users
            var allUsers = await _context.Users.ToListAsync();
            foreach (var user in allUsers)
            {
                _context.UserNotifications.Add(new UserNotification
                {
                    UserId = user.Id.ToString(),
                    NotificationId = notification.Id,
                    IsRead = false
                });
            }
            await _context.SaveChangesAsync();

            // ✅ Send to ALL clients
            await _hubContext.Clients.All.SendAsync("NewNotification", new
            {
                id = notification.Id,
                message = notification.Message,
                createdAt = notification.CreatedAt,
                isRead = false
            });

            return Ok(new { message = $"Device {id} deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting device");
            return StatusCode(500, new { message = "Unexpected error" });
        }
    }


    // PUT: api/Device/restore/{id}  --> RESTORE or UNDO
    [HttpPut("restore/{id}")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> RestoreDevice(int id)
    {
        try
        {
            var device = await _context.Devices
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(d => d.Id == id);
            if (device == null)
                return NotFound(new { message = $"Device with ID {id} not found." });

            if (!device.IsDeleted)
                return BadRequest(new { message = "Device is already active." });

            device.IsDeleted = false;
            await _context.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("DeviceRestored", new
            {
                DeviceId = device.Id,
                Message = $"Device \"{device.DeviceName}\" restored successfully."
            });

            // CREATE NOTIFICATION FOR ALL USERS
            var notification = new Notification
            {
                Message = $"{User.Identity?.Name} restored device \"{device.DeviceName}\""
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            var allUsers = await _context.Users.ToListAsync();
            foreach (var user in allUsers)
            {
                _context.UserNotifications.Add(new UserNotification
                {
                    UserId = user.Id.ToString(),
                    NotificationId = notification.Id,
                    IsRead = false
                });
            }
            await _context.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("NewNotification", new
            {
                id = notification.Id,
                message = notification.Message,
                createdAt = notification.CreatedAt,
                isRead = false
            });
            return Ok(new { message = $"Device with ID {id} restored successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while restoring device");
            return StatusCode(500, new { message = "Unexpected error while restoring device." });
        }
    }

    // PUT: api/Device/restoreAll  --> RESTORE ALL
    [HttpPut("restoreAll")]
    [Authorize(Roles = "Admin")] 
    public async Task<IActionResult> RestoreAllDeletedDevices()
    {
        try
        {
            bool success = _deviceService.RestoreAllDeletedDevices();
            if (!success)
                return NotFound(new { message = "No deleted devices found to restore." });

            await _hubContext.Clients.All.SendAsync("ReceiveNotification", "All deleted devices restored successfully");

            return Ok(new { message = "All deleted devices restored successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while restoring all devices");
            return StatusCode(500, new { message = "Unexpected error while restoring devices." });
        }
    }

    // DELETE: api/Device/permanent/{id}  --> PERMANENT DELETE
    [HttpDelete("permanent/{id}")]
    [Authorize(Roles = "Admin")] 
    public async Task<IActionResult> DeletePermanent(int id)
    {
        try
        {
            bool success = _deviceService.PermanentDeleteDevice(id);
            if (!success)
                return NotFound(new { message = $"Device with ID {id} not found for permanent deletion." });

            await _hubContext.Clients.All.SendAsync("ReceiveNotification", "Device permanently deleted");

            return Ok(new { message = $"Device with ID {id} permanently deleted." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while permanently deleting device");
            return StatusCode(500, new { message = "Unexpected error while permanently deleting device." });
        }
    }

    // GET paged: api/Device/paged?pageNumber=1&pageSize=5
    [HttpGet("paged")]
    [Authorize(Roles = "Admin,User")]
    public IActionResult GetPaged([FromQuery] int pageNumber = 1,
                                  [FromQuery] int pageSize = 5,
                                  [FromQuery] bool includeDeleted = false,
                                  [FromQuery] string? searchDescription = null,
                                  [FromQuery] string? searchUsername = null,
                                  [FromQuery] string? createdByUserId = null,
                                  [FromQuery] string? sortBy = null)
    {
        try
        {
            var userId = User.FindFirst("UserId")?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(role))
                return Unauthorized(new { message = "Invalid token: UserId or Role missing" });

            var devicesQuery = _context.Devices.AsQueryable();

            devicesQuery = includeDeleted ? devicesQuery.Where(d => d.IsDeleted) : devicesQuery.Where(d => !d.IsDeleted);

            if (role != "Admin")
                devicesQuery = devicesQuery.Where(d => d.UserId == userId);

            if (!string.IsNullOrEmpty(searchDescription))
                devicesQuery = devicesQuery.Where(d => d.Description.Contains(searchDescription));

            if (!string.IsNullOrEmpty(createdByUserId))
                devicesQuery = devicesQuery.Where(d => d.UserId == createdByUserId);

            // Join with users to get usernames
            var result = from d in devicesQuery
                         join u in _context.Users on d.UserId equals u.Id.ToString() into userJoin
                         from user in userJoin.DefaultIfEmpty()
                         select new
                         {
                             d.Id,
                             d.DeviceName,
                             d.Description,
                             d.UserId,
                             CreatedBy = user != null ? user.Username : "Unknown",
                             d.IsDeleted
                         };

            if (!string.IsNullOrEmpty(searchUsername))
                result = result.Where(r => r.CreatedBy.Contains(searchUsername));

            result = sortBy switch
            {
                "usernameAsc" => result.OrderBy(r => r.CreatedBy),
                "usernameDesc" => result.OrderByDescending(r => r.CreatedBy),
                _ => result.OrderBy(r => r.DeviceName)
            };

            var totalCount = result.Count();

            var devices = result.Skip((pageNumber - 1) * pageSize)
                                .Take(pageSize)
                                .ToList();

            return Ok(new
            {
                Data = devices,
                TotalRecords = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                IncludeDeleted = includeDeleted
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while fetching paged devices");
            return StatusCode(500, new { message = "Unexpected error while fetching devices." });
        }
    }

    [HttpGet("notifications")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> GetAllNotifications()
    {
        var userId = User.FindFirst("UserId")?.Value;

        var notifications = await _context.UserNotifications
            .Include(un => un.Notification)
            .Where(un => un.UserId == userId)
            .OrderByDescending(un => un.Notification.CreatedAt)
            .Take(10)  //  Limit to latest 10
            .Select(un => new
            {
                id = un.Id,  // 
                message = un.Notification.Message,  // ✅ lowercase
                createdAt = un.Notification.CreatedAt,  // ✅ lowercase
                isRead = un.IsRead,  // ✅ lowercase
                readAt = un.ReadAt
            })
            .ToListAsync();

        return Ok(notifications);
    }


    [HttpGet("notifications/unread-count")]
    public IActionResult GetUnreadCount()
    {
        var userId = User.FindFirst("UserId")?.Value;
        var count = _context.UserNotifications.Count(un => un.UserId == userId && !un.IsRead);
        return Ok(new { count = count });
    }

    [HttpPut("notifications/markread/{userNotificationId}")]
    public async Task<IActionResult> MarkNotificationAsRead(int userNotificationId)
    {
        var success = await _notificationService.MarkAsReadAsync(userNotificationId);
        if (!success) return NotFound(new { message = "Notification not found" });
        return Ok(new { message = "Marked as read" });
    }

    [HttpPut("notifications/markallread")]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = User.FindFirst("UserId")?.Value;

        var unread = _context.UserNotifications
                             .Where(un => un.UserId == userId && !un.IsRead);

        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("notifications/paged")]
    public IActionResult GetNotificationsPaged(int pageNumber = 1, int pageSize = 20)
    {
        var userId = User.FindFirst("UserId")?.Value;

        var query = from un in _context.UserNotifications
                    join n in _context.Notifications on un.NotificationId equals n.Id
                    where un.UserId == userId
                    orderby n.CreatedAt descending
                    select new
                    {
                        un.Id,
                        n.Message,
                        n.CreatedAt,
                        un.IsRead,
                        un.ReadAt
                    };

        var totalCount = query.Count();
        var notifications = query.Skip((pageNumber - 1) * pageSize)
                                 .Take(pageSize)
                                 .ToList();

        return Ok(new { Data = notifications, TotalRecords = totalCount });
    }

    // POST: api/Device/calculate-average
    [HttpPost("calculate-average")]
    [Authorize(Roles = "Admin,User")]
    public IActionResult CalculateAverage([FromBody] ColumnRequest request)
    {
        if (string.IsNullOrEmpty(request.ColumnName))
            return BadRequest(new { message = "Column name is required" });

        _queueService.Enqueue(request.ColumnName);  // Sends the column name to the background service
        return Ok(new { message = $"{request.ColumnName} queued for calculation" });
    }

    // DTO for column name
    public class ColumnRequest
    {
        public string ColumnName { get; set; }
    }


}
