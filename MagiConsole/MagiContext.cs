using Microsoft.EntityFrameworkCore;
using System;

namespace MagiConsole
{
    public class MagiContext : DbContext
    {
        public DbSet<FileData> Files { get; set; }

        public MagiContext(DbContextOptions<MagiContext> options) : base(options) { }
    }

    public class FileData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Extension { get; set; }
        public DateTimeOffset LastModified { get; set; }
        public string Hash { get; set; }
        public FileStatus Status { get; set; }

        public override bool Equals(object obj)
        {
            return (obj as FileData).Id == Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }

    public enum FileStatus
    {
        Unmodified = 0, //File not changed locally
        New = 1, //file is new, upload it
        Removed = 2, //file has been deleted locally, remove it from server
        Updated = 3 //file has been updated, sync to server
    }
}
