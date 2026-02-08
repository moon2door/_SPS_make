using System;

namespace _SPS.Models
{
    // 사용자 유형 정의
    public enum UserType
    {
        ShelterAndRescue,   // 보호소 및 구조대
        AdoptionApplicant,  // 입양 희망자
        LostPetSeeker,      // 실종 동물 찾는 사람
        OtherOrganization   // 기타 기관
    }

    public class UserModel
    {
        public string Uid { get; set; }
        public string Email { get; set; } // 이메일 정보도 DB에 저장해두면 유용합니다.
        public string Nickname { get; set; }

        // 새로 추가된 필드들
        public UserType UserType { get; set; }
        public string OrganizationName { get; set; } // 기관명 (선택)
        public string Address { get; set; }          // 주소 (기관/발견위치/거주지 등)
        public string PhoneNumber { get; set; }      // 연락처

        public DateTime CreationDate { get; set; } = DateTime.Now;
    }
}