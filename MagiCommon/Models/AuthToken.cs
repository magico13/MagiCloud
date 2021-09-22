using System;

namespace MagiCommon.Models
{
    public class AuthToken
    {
        public string Id { get; set; }
        public string LinkedUserId { get; set; }
        //public DateTimeOffset? Expiration { get; set; }
        public DateTimeOffset Creation { get; set; }
    }
}
