using System;

namespace MagiCommon.Models
{
    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string TokenName { get; set; }
        public int? DesiredTimeout { get; set; }
        public DateTimeOffset? DesiredExpiration { get; set; }
    }
}
