using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishPond
{
    public enum SpeciesId
    {
        Clownfish, BlueTang, Pufferfish, Squid, Octopus, Jellyfish,
        YellowTang, Butterflyfish, Angelfish, Lionfish, Stingray, SeaTurtle,
        Sardine
    }

    /// <summary>
    /// 생물 한 종의 설정. 길이·속도는 모두 "연못 반지름" 기준 비율이라
    /// 화면 크기가 달라도 같은 느낌으로 움직입니다.
    /// </summary>
    [Serializable]
    public class SpeciesDef
    {
        public SpeciesId id;
        public string displayName;

        [Tooltip("잡아서 통에 넣었을 때 점수 (해파리는 잡을 수 없음)")]
        public int points = 1;

        [Tooltip("등장 확률 가중치")]
        public float spawnWeight = 1f;

        [Tooltip("몸길이 (연못 반지름 대비)")]
        public float length = 0.13f;

        [Tooltip("헤엄 속도 (연못 반지름/초). 오징어는 분사 세기")]
        public float speed = 0.3f;

        [Tooltip("잡는 데 필요한 정지 시간 배수 (기본 0.4초 × 이 값)")]
        public float holdMultiplier = 1f;

        public bool catchable = true;

        [Header("에셋 교체 (선택)")]
        [Tooltip("Asset Store 등에서 가져온 프리팹. 비워두면 코드로 그린 그래픽을 씁니다.")]
        public GameObject prefabOverride;

        [Tooltip("켜면 물고기 옆모습이 보이게 눕힙니다 (몸이 납작한 실사 물고기용). 끄면 위에서 내려다본 모습.")]
        public bool prefabSideView = true;

        [Tooltip("프리팹 회전 보정. 기준: 모델의 앞(+Z)이 진행 방향, 위(+Y)가 등 쪽")]
        public Vector3 prefabRotation = Vector3.zero;

        [Tooltip("프리팹 크기 배율")]
        public float prefabScale = 1f;

        [Tooltip("프리팹에 애니메이터 컨트롤러가 없을 때 붙일 컨트롤러 (헤엄 애니메이션)")]
        public RuntimeAnimatorController animatorController;

        public SpeciesDef Clone() { return (SpeciesDef)MemberwiseClone(); }
    }

    public static class SpeciesDefaults
    {
        public static List<SpeciesDef> Create()
        {
            return new List<SpeciesDef>
            {
                //                                       점수  등장   길이   속도   잡는시간
                Make(SpeciesId.Clownfish,     "흰동가리",   1, 1.00f, 0.12f, 0.30f, 1.00f, true),
                Make(SpeciesId.YellowTang,    "노랑탱",     1, 0.90f, 0.13f, 0.32f, 1.00f, true),
                Make(SpeciesId.Sardine,       "정어리",     1, 0.80f, 0.14f, 0.36f, 1.00f, true),
                Make(SpeciesId.Butterflyfish, "나비고기",   2, 0.70f, 0.12f, 0.28f, 1.00f, true),
                Make(SpeciesId.BlueTang,      "블루탱",     2, 0.60f, 0.15f, 0.34f, 1.00f, true),
                Make(SpeciesId.Angelfish,     "엔젤피시",   3, 0.45f, 0.15f, 0.26f, 1.00f, true),
                Make(SpeciesId.Pufferfish,    "복어",       3, 0.40f, 0.12f, 0.18f, 1.00f, true),
                Make(SpeciesId.Lionfish,      "쏠배감펭",   4, 0.30f, 0.15f, 0.16f, 1.00f, true),
                Make(SpeciesId.Squid,         "오징어",     4, 0.30f, 0.19f, 1.20f, 1.00f, true),
                Make(SpeciesId.Octopus,       "문어",       5, 0.18f, 0.22f, 0.10f, 1.25f, true),
                Make(SpeciesId.Stingray,      "가오리",     5, 0.20f, 0.22f, 0.20f, 1.20f, true),
                Make(SpeciesId.SeaTurtle,     "바다거북",   7, 0.10f, 0.25f, 0.10f, 1.40f, true),
                Make(SpeciesId.Jellyfish,     "해파리",     0, 0.00f, 0.15f, 0.06f, 1.00f, false),
            };
        }

        static SpeciesDef Make(SpeciesId id, string name, int pts, float weight, float len, float spd, float hold, bool catchable)
        {
            return new SpeciesDef
            {
                id = id, displayName = name, points = pts, spawnWeight = weight,
                length = len, speed = spd, holdMultiplier = hold, catchable = catchable
            };
        }
    }

    /// <summary>
    /// 메뉴 "Fish Pond/생물 설정 에셋 만들기"로 생성됩니다.
    /// Assets/FishPond/Resources/FishPond/SpeciesCatalog.asset 위치에 있어야 게임이 읽습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "SpeciesCatalog", menuName = "Fish Pond/Species Catalog")]
    public class SpeciesCatalog : ScriptableObject
    {
        public const string ResourcePath = "FishPond/SpeciesCatalog";

        public List<SpeciesDef> species = SpeciesDefaults.Create();

        public static List<SpeciesDef> LoadOrDefault()
        {
            var list = SpeciesDefaults.Create();
            var cat = Resources.Load<SpeciesCatalog>(ResourcePath);
            if (cat == null || cat.species == null) return list;
            foreach (var s in cat.species)
            {
                if (s == null) continue;
                int i = list.FindIndex(d => d.id == s.id);
                if (i >= 0) list[i] = s.Clone();
            }
            return list;
        }
    }
}
