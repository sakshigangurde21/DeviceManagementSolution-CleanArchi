using DeviceManagementSolution.Domain.Entities;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface IUserService
    {
        Task<User?> GetByUsernameAsync(string username);
        Task<User?> GetByIdAsync(int id);
        Task<bool> CreateUserAsync(User user);
    }
}
