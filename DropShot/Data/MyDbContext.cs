using DropShot.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace DropShot.Data
{
    public class MyDbContext : DbContext
    {
        public MyDbContext(DbContextOptions<MyDbContext> options)
            : base(options)
        {
        }

        public DbSet<Score> Score { get; set; }
        public DbSet<AppSetting> AppSettings { get; set; }

        // Add this property to fix CS1061
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
