using DeviceManagementSolution.Domain.Entities;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface IRefreshTokenService
    {
        Task<RefreshToken?> GetByTokenAsync(string token);
        Task<RefreshToken> CreateAsync(int userId, string? ipAddress);
        Task RevokeAsync(RefreshToken token, string? revokedByIp, string? replacedByToken = null);
        Task RevokeByTokenAsync(string token, string? revokedByIp);

    }
}
