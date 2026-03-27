using DropShot.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Data
{
    public class MyDbContext(DbContextOptions<MyDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<Competition> Competition { get; set; }
        public DbSet<Score> Score { get; set; }
        public DbSet<SavedMatch> SavedMatch { get; set; }
        public DbSet<AppSetting> AppSettings { get; set; }
        public DbSet<Player> Players { get; set; }
    }

    public interface ISettingsService
    {
        Task<Dictionary<string, string>> GetSettingsAsync();
    }

    public class SettingsService : ISettingsService
    {
        private readonly MyDbContext _context;

        public SettingsService(MyDbContext context)
        {
            _context = context;
        }

        public async Task<Dictionary<string, string>> GetSettingsAsync()
        {
            return await _context.AppSettings
                .ToDictionaryAsync(s => s.Setting, s => s.Value);
        }
    }
}
