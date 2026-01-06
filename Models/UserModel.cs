namespace _SPS.Models
{
    public class UserModel
    {
        public string Uid { get; set; }

        public string Nickname { get; set; }

        public DateTime CreationDate { get; set; } = DateTime.Now;
    }
}