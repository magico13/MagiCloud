using System;

namespace MagiCommon.Models
{
    public class ElasticObject
    {
        public string Id { get; set; } = string.Empty;
        public string ParentId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTimeOffset LastUpdated { get; set; }
        public bool IsPublic { get; set; }
        public bool IsDeleted { get; set; }
    }
}
