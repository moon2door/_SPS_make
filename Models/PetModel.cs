namespace _SPS.Models
{
    public class PetModel
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Species { get; set; }

        // [추가] 성별 (Male / Female)
        public string Gender { get; set; }

        public string Age { get; set; }
        public string Description { get; set; }
        public string OwnerId { get; set; }

        // [수정] 기존 ImageUrl 하나를 4개로 확장 (상사 요구사항 반영)
        public string ImageUrl1 { get; set; } // 전면 (Front) - AI 분석용
        public string ImageUrl2 { get; set; } // 측면 (Side)
        public string ImageUrl3 { get; set; } // 자유 (Free)
        public string ImageUrl4 { get; set; } // 보호자와 함께 (With Owner)

        public string Weight { get; set; }
        public string Condition { get; set; }
        public string Contact { get; set; }
        public string Location { get; set; }
        public string Feature { get; set; }
    }
}