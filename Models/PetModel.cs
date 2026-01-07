namespace _SPS.Models
{
    public class PetModel
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Species { get; set; }
        public string Gender { get; set; }
        public string Age { get; set; }
        public string Status { get; set; }
        public string Description { get; set; }
        public string OwnerId { get; set; }
        public string ImageUrl1 { get; set; } // 전면 (Front) - AI 분석용
        public string ImageUrl2 { get; set; } // 측면 (Side)
        public string ImageUrl3 { get; set; } // 자유1 (Free1)
        public string ImageUrl4 { get; set; } // 자유2 (Free2)
        public string Weight { get; set; }
        public string Condition { get; set; }
        public string Contact { get; set; }
        public string Location { get; set; }
        public string Feature { get; set; }
    }
}