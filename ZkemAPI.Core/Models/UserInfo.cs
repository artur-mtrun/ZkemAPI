namespace ZkemAPI.Core.Models
{
    public class UserInfo
    {
        public string EnrollNumber { get; set; }
        public string Name { get; set; }
        public string CardNumber { get; set; }
        public string Password { get; set; }
        public int Privilege { get; set; }
        public bool Enabled { get; set; }
    }
} 