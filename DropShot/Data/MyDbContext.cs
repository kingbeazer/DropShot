using System.Collections.Generic;
using System.Xml.Linq;
using DropShot.Models;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Data
{
    public class MyDbContext : DbContext
    {
        public MyDbContext(DbContextOptions<MyDbContext> options)
            : base(options) { }

        public DbSet<Score> Score { get; set; }
    }
}
