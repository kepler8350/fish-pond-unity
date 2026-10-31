using UnityEngine;

namespace FishPond
{
    /// <summary>운영자용 설정 패널(F1)과 추적 보정 마커(F2)</summary>
    public partial class FishPondGame
    {
        bool showPanel;
        Rect panelRect = new Rect(20, 20, 440, 10);
        Vector2 panelScroll;
        string portText;
        GameObject calibRoot;

        float GuiScale { get { return Mathf.Max(1f, Screen.height / 1080f); } }

        bool PointerOverPanel(Vector2 screen)
        {
            if (!showPanel) return false;
            float s = GuiScale;
            var gui = new Vector2(screen.x / s, (Screen.height - screen.y) / s);
            return panelRect.Contains(gui);
        }

        void OnGUI()
        {
            float s = GuiScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            if (Art.Font != null) GUI.skin.font = Art.Font;

            if (!showPanel)
            {
                if (state == GameState.Ready)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.35f);
                    GUI.Label(new Rect(16f, Screen.height / s - 32f, 900f, 28f),
                        "F1 설정 · Space 시작 · Esc 일시정지 · R 처음 화면 · F2 보정 마커 · F11 전체화면");
                    GUI.color = Color.white;
                }
                return;
            }

            float h = Mathf.Min(Screen.height / s - 40f, 900f);
            panelRect = new Rect(20f, 20f, 440f, h);
            GUILayout.BeginArea(panelRect, GUI.skin.box);
            panelScroll = GUILayout.BeginScrollView(panelScroll);

            GUILayout.Label("<b>뜰채 바다 · 운영 설정</b>  (F1 닫기)", Rich());
            GUILayout.Space(6);

            GUILayout.Label("게임 시간: " + settings.durationSec + "초");
            GUILayout.BeginHorizontal();
            foreach (var d in new[] { 60, 90, 120, 180 })
                if (GUILayout.Toggle(settings.durationSec == d, d + "초", "Button")) settings.durationSec = d;
            GUILayout.EndHorizontal();

            GUILayout.Label("연못 생물 수: " + settings.creatureCount + "마리");
            settings.creatureCount = Mathf.RoundToInt(GUILayout.HorizontalSlider(settings.creatureCount, 4, 24));
            GUILayout.Label("해파리 (닿으면 뜰채가 1.6초 마비): " + settings.jellyCount + "마리");
            settings.jellyCount = Mathf.RoundToInt(GUILayout.HorizontalSlider(settings.jellyCount, 0, 3));

            GUILayout.Space(8);
            GUILayout.Label("<b>투사</b>", Rich());
            settings.roundProjection = GUILayout.Toggle(settings.roundProjection, " 원형 투사 모드 (원 밖은 검정, 통은 원 안쪽)");
            settings.showHandles = GUILayout.Toggle(settings.showHandles, " 뜰채 손잡이 그리기 (실물 뜰채를 쓰면 끄기)");
            settings.calibScale = Slider("크기", settings.calibScale, 0.5f, 1.5f);
            settings.calibAspect = Slider("가로 비율 (원이 타원이면 조정)", settings.calibAspect, 0.8f, 1.2f);
            settings.calibOffsetX = Slider("좌우 이동", settings.calibOffsetX, -3f, 3f);
            settings.calibOffsetY = Slider("상하 이동", settings.calibOffsetY, -3f, 3f);
            if (GUILayout.Button("투사 보정 초기화"))
            {
                settings.calibScale = 1f; settings.calibAspect = 1f; settings.calibOffsetX = 0f; settings.calibOffsetY = 0f;
            }

            GUILayout.Space(8);
            GUILayout.Label("<b>카메라 추적</b>", Rich());
            bool track = GUILayout.Toggle(settings.trackingEnabled, " 추적 프로그램 좌표 받기 (UDP)");
            GUILayout.BeginHorizontal();
            GUILayout.Label("포트", GUILayout.Width(40));
            if (portText == null) portText = settings.trackingPort.ToString();
            portText = GUILayout.TextField(portText, 5, GUILayout.Width(80));
            if (GUILayout.Button("적용", GUILayout.Width(60)))
            {
                int p;
                if (int.TryParse(portText, out p) && p > 0 && p < 65536) { settings.trackingPort = p; if (settings.trackingEnabled) StartTracking(); }
                GUIUtility.keyboardControl = 0;
            }
            GUILayout.EndHorizontal();
            if (track != settings.trackingEnabled)
            {
                settings.trackingEnabled = track;
                if (track) StartTracking(); else StopTracking();
            }
            if (tracker != null)
            {
                string status = "수신 " + tracker.Packets + "회";
                for (int i = 0; i < 4; i++) { Vector2 v; status += "  " + PlayerNames[i] + (tracker.TryGet(i, 0.3, out v) ? " ●" : " ○"); }
                GUILayout.Label(status);
                if (!string.IsNullOrEmpty(tracker.Error)) GUILayout.Label("<color=#ff8a7a>" + tracker.Error + "</color>", Rich());
            }
            if (GUILayout.Button(calibRoot != null ? "보정 마커 숨기기 (F2)" : "보정 마커 보이기 (F2)")) ToggleCalibrationMarkers();

            GUILayout.Space(8);
            GUILayout.Label("소리 크기");
            settings.volume = GUILayout.HorizontalSlider(settings.volume, 0f, 1f);

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("저장")) settings.Save();
            if (GUILayout.Button(state == GameState.Ready || state == GameState.Results ? "게임 시작" : "다시 시작")) StartGame();
            if (GUILayout.Button("처음 화면")) EnterReady();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("프로그램 종료")) Application.Quit();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static GUIStyle Rich()
        {
            var st = new GUIStyle(GUI.skin.label) { richText = true };
            return st;
        }

        static float Slider(string label, float v, float min, float max)
        {
            GUILayout.Label(label + ": " + v.ToString("0.00"));
            return GUILayout.HorizontalSlider(v, min, max);
        }

        /// <summary>
        /// 투사 화면 네 귀퉁이(왼쪽 위 1 → 오른쪽 위 2 → 오른쪽 아래 3 → 왼쪽 아래 4)에 십자 표시.
        /// 추적 프로그램 보정 때 뜰채를 순서대로 이 위에 올려놓습니다.
        /// 위치는 화면 가장자리에서 5% 안쪽이며, 투사 보정(크기·이동)의 영향을 받지 않습니다.
        /// </summary>
        void ToggleCalibrationMarkers()
        {
            if (calibRoot != null) { Destroy(calibRoot); calibRoot = null; return; }
            calibRoot = new GameObject("CalibrationMarkers");
            var vps = new[] { new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.05f), new Vector2(0.95f, 0.95f), new Vector2(0.05f, 0.95f) };
            float size = cam.orthographicSize * 0.08f;
            for (int i = 0; i < 4; i++)
            {
                var w = cam.ViewportToWorldPoint(new Vector3(vps[i].x, 1f - vps[i].y, 10f));
                var m = new GameObject("marker" + (i + 1)).transform;
                m.SetParent(calibRoot.transform, false);
                m.position = new Vector3(w.x, w.y, -4f);
                Spr(m, "h", Art.Square, Color.white, 90, Vector2.zero, new Vector2(size * 2f, size * 0.12f), 0f, 0f);
                Spr(m, "v", Art.Square, Color.white, 90, Vector2.zero, new Vector2(size * 0.12f, size * 2f), 0f, 0f);
                Spr(m, "ring", Art.Ring(0.1f), new Color(1f, 0.85f, 0.3f), 90, Vector2.zero, Vector2.one * size * 1.4f, 0f, 0f);
                var label = new FloorText(m, "num", 91, new Color(1f, 0.85f, 0.3f), size * 0.9f);
                label.Text = (i + 1).ToString();
                label.Place(new Vector2(vps[i].x < 0.5f ? size * 1.4f : -size * 1.4f, vps[i].y < 0.5f ? -size * 1.4f : size * 1.4f), 0f, 0f);
            }
        }
    }
}
