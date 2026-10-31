using System;
using UnityEngine;

namespace FishPond
{
    /// <summary>운영자 설정. F1 패널에서 바꾸고 "저장"하면 PlayerPrefs에 남습니다.</summary>
    [Serializable]
    public class PondSettings
    {
        const string Key = "FishPond.Settings.v1";

        public int durationSec = 90;
        public int creatureCount = 12;
        public int jellyCount = 1;

        [Tooltip("원형 투사 모드: 원 바깥은 완전 검정, 낚시통은 원 안쪽 가장자리")]
        public bool roundProjection = false;

        // 투사 보정 (화면 전체를 이동·확대·가로세로 보정)
        public float calibScale = 1f;
        public float calibOffsetX = 0f;
        public float calibOffsetY = 0f;
        public float calibAspect = 1f;

        public bool trackingEnabled = true;
        public int trackingPort = 7777;

        public bool showHandles = true;
        public float volume = 0.8f;

        public static PondSettings Load()
        {
            try
            {
                if (PlayerPrefs.HasKey(Key))
                {
                    var s = JsonUtility.FromJson<PondSettings>(PlayerPrefs.GetString(Key));
                    if (s != null) return s;
                }
            }
            catch (Exception e) { Debug.LogWarning("[FishPond] 설정을 읽지 못해 기본값을 씁니다: " + e.Message); }
            return new PondSettings();
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }
    }
}
