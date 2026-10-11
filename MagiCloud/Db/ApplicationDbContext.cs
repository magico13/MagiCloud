using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MagiCloud.Db;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions options) : base(options)
    {
    }
}

/// <summary>
/// Provides a design-time <see cref="ApplicationDbContext"/> so that tools such as
/// <c>dotnet-ef</c> can create the context without a running application or a real
/// connection string.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlite("DataSource=magicloud-design.db");
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}