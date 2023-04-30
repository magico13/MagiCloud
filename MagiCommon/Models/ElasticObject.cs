using System;

namespace MagiCommon.Models
{
    public class ElasticObject
    {
        public string Id { get; set; }
        public string ParentId { get; set; }
        public string Name { get; set; }
        public string UserId { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
        public bool IsPublic { get; set; }
        public bool IsDeleted { get; set; }
    }
}
