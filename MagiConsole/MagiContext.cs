using Microsoft.EntityFrameworkCore;
using System;

namespace MagiConsole;

public class MagiContext : DbContext
{
    public DbSet<FileData> Files { get; set; }

    public DbSet<UserData> Users { get; set; }

    public MagiContext(DbContextOptions<MagiContext> options) : base(options) { }
}

public class FileData
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Extension { get; set; }
    public DateTimeOffset LastModified { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
    public string Hash { get; set; }
    public FileStatus Status { get; set; }

    public override string ToString()
    {
        return $"{Name}.{Extension}";
    }
}

public enum FileStatus
{
    Unmodified = 0, //File not changed locally
    New = 1, //file is new, upload it
    Removed = 2, //file has been deleted locally, remove it from server
    Updated = 3 //file has been updated, sync to server
}

public class UserData
{
    public string Id { get; set; }
    public string Username { get; set; }
    public string Token { get; set; }
}
