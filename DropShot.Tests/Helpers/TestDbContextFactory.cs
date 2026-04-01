using DropShot.Data;
using Microsoft.EntityFrameworkCore;

namespace DropShot.Tests.Helpers;

public class TestDbContextFactory : IDbContextFactory<MyDbContext>
{
    private readonly DbContextOptions<MyDbContext> _options;

    public TestDbContextFactory(string? dbName = null)
    {
        dbName ??= Guid.NewGuid().ToString();
        _options = new DbContextOptionsBuilder<MyDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
    }

    public MyDbContext CreateDbContext() => new(_options);
}
