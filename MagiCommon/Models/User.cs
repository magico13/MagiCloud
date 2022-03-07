namespace MagiCommon.Models
{
    public class User
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool IsLocked { get; set; }
        public int LoginFailures { get; set; }
    }
}
