using System;
using UnityEngine;

namespace FishPond
{
    /// <summary>운영자 설정. F1 패널에서 바꾸고 "저장"하면 PlayerPrefs에 남습니다.</summary>
    [Serializable]
    public class PondSettings
    {
        [NonSerialized] string key = "FishPond.Settings.v2";

        public int durationSec = 90;
        public int creatureCount = 14;
        public int jellyCount = 1;       // 뜰채 바다
        public int sharkCount = 2;       // 상어 구조대
        public float fishSpeed = 0.55f;  // 물고기 헤엄 속도 배율

        // 투사 보정 (둥근 화면 전체를 이동·확대·가로세로 보정)
        public float calibScale = 1f;
        public float calibOffsetX = 0f;
        public float calibOffsetY = 0f;
        public float calibAspect = 1f;

        public bool trackingEnabled = true;
        public int trackingPort = 7777;

        public bool showHandles = true;
        public float volume = 0.8f;

        public static PondSettings Load(string key)
        {
            try
            {
                if (PlayerPrefs.HasKey(key))
                {
                    var s = new PondSettings();
                    JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(key), s);   // 새로 생긴 항목은 기본값 유지
                    if (s != null) { s.key = key; return s; }
                }
            }
            catch (Exception e) { Debug.LogWarning("[FishPond] 설정을 읽지 못해 기본값을 씁니다: " + e.Message); }
            return new PondSettings { key = key };
        }

        public void Save()
        {
            PlayerPrefs.SetString(key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }
    }
}
