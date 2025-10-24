using Infrastructure.Data;
using DeviceManagementSolution.Domain.Entities;
using Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly DeviceDbContext _context;

        public RefreshTokenService(DeviceDbContext context)
        {
            _context = context;
        }

        public async Task<RefreshToken?> GetByTokenAsync(string token)
        {
            return await _context.RefreshTokens
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Token == token);
        }

        public async Task<RefreshToken> CreateAsync(int userId)
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);

            var refreshToken = new RefreshToken
            {
                Token = Convert.ToBase64String(randomBytes),
                Expires = DateTime.UtcNow.AddDays(7),
                Created = DateTime.UtcNow,
                UserId = userId
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            return refreshToken;
        }
        public async Task RevokeAsync(RefreshToken token)
        {
            token.Revoked = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
        public async Task RevokeByTokenAsync(string token)
        {
            var existing = await GetByTokenAsync(token);
            if (existing == null) return;

            await RevokeAsync(existing);
        }
    }
}
