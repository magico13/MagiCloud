using System;

namespace MagiCommon.Models
{
    public class AuthToken
    {
        public string Id { get; set; }
        public string LinkedUserId { get; set; }
        public string Name { get; set; }
        public DateTimeOffset Creation { get; set; }
        public DateTimeOffset? Expiration { get; set; }
        public int? Timeout{ get; set; }
        public DateTimeOffset? LastUpdated { get; set; }
    }
}
