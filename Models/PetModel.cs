namespace _SPS.Models
{
    public class PetModel
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Species { get; set; } // Android의 'breed'에 대응
        public string Age { get; set; }
        public string Description { get; set; }
        public string OwnerId { get; set; } // Android의 'userId'에 대응
        public string ImageUrl { get; set; }

        // [추가된 속성들]
        public string Weight { get; set; }
        public string Condition { get; set; }
        public string Contact { get; set; }
        public string Location { get; set; }
        public string Feature { get; set; } // Android의 'feature'에 대응
    }
}