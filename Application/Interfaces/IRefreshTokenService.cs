using DeviceManagementSolution.Domain.Entities;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface IRefreshTokenService
    {
        Task<RefreshToken?> GetByTokenAsync(string token);
        Task<RefreshToken> CreateAsync(int userId);
        Task RevokeAsync(RefreshToken token);
        Task RevokeByTokenAsync(string token);
    }
}
