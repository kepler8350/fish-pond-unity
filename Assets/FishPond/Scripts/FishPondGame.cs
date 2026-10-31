using System.Collections.Generic;
using UnityEngine;

namespace FishPond
{
    /// <summary>
    /// 뜰채 바다 — 바닥 투사형 4인 바다 생물 잡기 게임의 본체.
    /// 빈 씬에 이 컴포넌트 하나만 있으면 카메라·연못·생물·뜰채·통을 모두 코드로 만듭니다.
    ///
    /// 키: Space 시작 · Esc 일시정지 · R 처음 화면 · F1 설정 패널 · F2 추적 보정 마커 · F11 전체화면
    /// </summary>
    [DisallowMultipleComponent]
    public partial class FishPondGame : MonoBehaviour
    {
        // ---------------------------------------------------------------- 판정값 (속도는 연못 반지름 R/초 기준)
        public const int CAP = 2;            // 뜰채 하나에 담기는 최대 마릿수
        public const float HOLD = 0.4f;      // 물고기 위에 이만큼 머물면 잡힘 (초)
        public const float CATCH = 1.0f;     // 이보다 빠르게 움직이는 뜰채로는 못 잡음
        public const float FLEE = 1.5f;      // 이보다 빠른 뜰채가 다가오면 물고기가 도망
        public const float ESCAPE = 3.2f;    // 담은 채로 이보다 빠르게 휘두르면 놓침
        public const float KEYSPD = 0.85f;   // 키보드 조작 속도

        public static readonly string[] PlayerNames = { "P1", "P2", "P3", "P4" };
        public static readonly Color[] PlayerColors =
        {
            new Color(1f, 0.42f, 0.35f), new Color(1f, 0.82f, 0.25f),
            new Color(0.24f, 0.86f, 0.59f), new Color(0.35f, 0.66f, 1f)
        };

        // 위, 아래, 왼쪽, 오른쪽
        static readonly KeyCode[][] MoveKeys =
        {
            new[] { KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D },
            new[] { KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow },
            new[] { KeyCode.I, KeyCode.K, KeyCode.J, KeyCode.L },
            new[] { KeyCode.T, KeyCode.G, KeyCode.F, KeyCode.H },
        };

        public enum GameState { Ready, Countdown, Playing, Paused, Results }

        [Tooltip("켜면 F1 패널에 저장된 값을 무시하고 아래 기본 설정으로 시작합니다.")]
        public bool ignoreSavedSettings = false;
        public PondSettings settings = new PondSettings();

        public GameState state = GameState.Ready;
        GameState stateBeforePause;

        public float R { get; private set; }
        public float NETR { get; private set; }
        public bool IsPlaying { get { return state == GameState.Playing; } }

        public readonly Net[] nets = new Net[4];
        public readonly Bucket[] buckets = new Bucket[4];
        public Fx fx { get; private set; }
        public Sfx sfx { get; private set; }

        float halfW, halfH, circleR, bucketR;
        Camera cam;
        Transform stage, env;

        readonly List<Creature> creatures = new List<Creature>();
        readonly List<Creature>[] inBucket = { new List<Creature>(), new List<Creature>(), new List<Creature>(), new List<Creature>() };
        readonly List<float> spawnQueue = new List<float>();
        readonly int[] scores = new int[4], counts = new int[4];
        List<SpeciesDef> species;
        SpeciesDef jellyDef;

        FloorText[] timerTexts, titleTexts, subTexts;
        FloorText bigText;

        float remaining, countT, resultT, startFlashT, clock;
        int lastTick = 99, lastW, lastH;
        bool lastRound;

        TrackingReceiver tracker;
        readonly Dictionary<int, int> grabs = new Dictionary<int, int>();
        readonly HashSet<int> knownPointers = new HashSet<int>();
        readonly HashSet<int> seenPointers = new HashSet<int>();
        readonly List<int> releaseList = new List<int>();
        readonly List<InputBridge.Pointer> pointers = new List<InputBridge.Pointer>();

        class Seaweed { public LineRenderer line; public Vector2 root; public float angle, length, phase; }
        readonly List<Seaweed> seaweeds = new List<Seaweed>();
        readonly Vector3[] weedBuf = new Vector3[7];

        // ================================================================ 시작
        void Start()
        {
            if (!ignoreSavedSettings) settings = PondSettings.Load();
            species = SpeciesCatalog.LoadOrDefault();
            jellyDef = species.Find(s => s.id == SpeciesId.Jellyfish);

            SetupCamera();
            stage = new GameObject("Stage").transform;
            fx = new Fx(stage);
            sfx = new Sfx(gameObject) { volume = settings.volume };

            for (int i = 0; i < 4; i++)
            {
                buckets[i] = new Bucket(i, PlayerColors[i], stage);
                nets[i] = new Net(i, PlayerColors[i], stage);
            }

            timerTexts = new[] { new FloorText(stage, "timerA", 80, Color.white, 1f), new FloorText(stage, "timerB", 80, Color.white, 1f) };
            titleTexts = new[] { new FloorText(stage, "titleA", 80, Color.white, 1f), new FloorText(stage, "titleB", 80, Color.white, 1f) };
            subTexts = new[] { new FloorText(stage, "subA", 80, new Color(0.8f, 0.93f, 0.92f), 1f), new FloorText(stage, "subB", 80, new Color(0.8f, 0.93f, 0.92f), 1f) };
            bigText = new FloorText(stage, "big", 82, Color.white, 1f);

            Relayout();
            ApplyCalibration();
            ResetNets();
            for (int i = 0; i < settings.creatureCount; i++) Spawn(PickSpecies());
            if (settings.trackingEnabled) StartTracking();
            EnterReady();
        }

        void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var g = new GameObject("Main Camera");
                g.tag = "MainCamera";
                cam = g.AddComponent<Camera>();
            }
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;

            // Asset Store 3D 모델을 쓸 때 필요한 조명 (위에서 아래로)
            var lg = new GameObject("Light");
            var light = lg.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lg.transform.rotation = Quaternion.identity;
            RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.62f);
        }

        // ================================================================ 화면 배치
        void Relayout()
        {
            lastW = Screen.width; lastH = Screen.height; lastRound = settings.roundProjection;
            float oldR = R;
            halfH = cam.orthographicSize;
            halfW = halfH * cam.aspect;
            float M = 2f * Mathf.Min(halfW, halfH);
            var bp = new Vector2[4];
            var rot = new float[4];

            if (!settings.roundProjection)
            {
                bucketR = M * 0.105f;
                float pad = M * 0.025f;
                float bx = halfW - pad - bucketR, by = halfH - pad - bucketR;
                bp[0] = new Vector2(-bx, -by); bp[1] = new Vector2(bx, -by);
                bp[2] = new Vector2(bx, by); bp[3] = new Vector2(-bx, by);
                rot[0] = 0f; rot[1] = 0f; rot[2] = 180f; rot[3] = 180f;
                R = Mathf.Max(M * 0.15f, Mathf.Min(M * 0.45f, bp[0].magnitude - bucketR * 1.25f));
                circleR = 0f;
            }
            else
            {
                circleR = Mathf.Min(halfW, halfH) * 0.98f;
                bucketR = circleR * 0.17f;
                float d = circleR - circleR * 0.04f - bucketR;
                float[] deg = { 225f, 315f, 45f, 135f };
                for (int i = 0; i < 4; i++)
                {
                    float a = deg[i] * Mathf.Deg2Rad;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    bp[i] = dir * d;
                    rot[i] = Mathf.Atan2(-dir.y, -dir.x) * Mathf.Rad2Deg - 90f;
                }
                R = d - bucketR * 0.9f;
            }
            NETR = Mathf.Max(M * 0.035f, R * 0.16f);

            for (int i = 0; i < 4; i++) buckets[i].Layout(bp[i], bucketR, rot[i]);

            if (oldR > 0f && Mathf.Abs(oldR - R) > 1e-4f)
            {
                float k = R / oldR;
                foreach (var c in creatures) c.Rescale(k);
                foreach (var n in nets) { n.pos *= k; n.target *= k; }
            }

            cam.backgroundColor = settings.roundProjection ? Color.black : new Color(0.02f, 0.06f, 0.08f);
            BuildEnvironment();
            LayoutTexts();
        }

        void ApplyCalibration()
        {
            stage.localPosition = new Vector3(settings.calibOffsetX, settings.calibOffsetY, 0f);
            stage.localScale = new Vector3(settings.calibScale * settings.calibAspect, settings.calibScale, 1f);
        }

        SpriteRenderer Spr(Transform parent, string name, Sprite sp, Color c, int order, Vector2 pos, Vector2 scale, float rotDeg, float z)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = new Vector3(pos.x, pos.y, z);
            g.transform.localRotation = Quaternion.Euler(0, 0, rotDeg);
            g.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.color = c;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>바닥 모래, 물, 갯바위, 불가사리, 해초</summary>
        void BuildEnvironment()
        {
            if (env != null) Destroy(env.gameObject);
            env = new GameObject("Environment").transform;
            env.SetParent(stage, false);
            seaweeds.Clear();
            var rnd = new System.Random(7);
            System.Func<float> rf = () => (float)rnd.NextDouble();
            const float Tau = Mathf.PI * 2f;

            if (settings.roundProjection)
                Spr(env, "ground", Art.Circle, new Color(0.13f, 0.12f, 0.09f), 0, Vector2.zero, Vector2.one * circleR * 2f, 0f, 3f);
            else
                Spr(env, "ground", Art.SoftCircle, new Color(0.2f, 0.17f, 0.12f), 0, Vector2.zero, Vector2.one * R * 3.6f, 0f, 3f);

            for (int i = 0; i < 220; i++)
            {
                float a = rf() * Tau, r = R * (1.1f + rf() * 0.6f);
                if (settings.roundProjection && r > circleR * 0.96f) continue;
                Spr(env, "sand", Art.Circle, new Color(0.85f, 0.78f, 0.6f, 0.05f + rf() * 0.1f), 1,
                    new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, Vector2.one * R * (0.008f + rf() * 0.02f), 0f, 2.5f);
            }

            var water = new GameObject("Water");
            water.transform.SetParent(env, false);
            water.transform.localPosition = new Vector3(0f, 0f, 1f);
            water.transform.localScale = new Vector3(R * 2.04f, R * 2.04f, 1f);
            water.AddComponent<MeshFilter>().sharedMesh = Art.Quad;
            var wr = water.AddComponent<MeshRenderer>();
            wr.sharedMaterial = Art.Water;
            wr.sortingOrder = 2;

            for (int i = 0; i < 14; i++)
            {
                float a = rf() * Tau;
                var g = new GameObject("seaweed");
                g.transform.SetParent(env, false);
                g.transform.localPosition = new Vector3(0f, 0f, 0.4f);
                var lr = g.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.positionCount = weedBuf.Length;
                lr.sharedMaterial = Art.Unlit;
                lr.alignment = LineAlignment.TransformZ;
                lr.numCapVertices = 3;
                lr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f));
                lr.widthMultiplier = R * 0.022f;
                lr.startColor = new Color(0.12f, 0.42f, 0.25f, 0.9f);
                lr.endColor = new Color(0.32f, 0.64f, 0.34f, 0.75f);
                lr.sortingOrder = 6;
                seaweeds.Add(new Seaweed
                {
                    line = lr,
                    root = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R * 0.95f,
                    angle = a + Mathf.PI + (rf() - 0.5f) * 0.9f,
                    length = R * (0.1f + rf() * 0.08f),
                    phase = rf() * 10f
                });
            }

            for (int ring = 0; ring < 2; ring++)
            {
                int n = Mathf.RoundToInt(Tau / 0.075f);
                for (int i = 0; i < n; i++)
                {
                    float a = (i + ring * 0.5f) / n * Tau + (rf() - 0.5f) * 0.03f;
                    float rr = R * (1.01f + ring * 0.055f + rf() * 0.02f);
                    var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                    var size = new Vector2(R * (0.035f + rf() * 0.022f), R * (0.026f + rf() * 0.016f)) * 2f;
                    float sh = 0.27f + rf() * 0.2f;
                    float rot = (a + rf() * 0.6f) * Mathf.Rad2Deg;
                    Spr(env, "rock", Art.Circle, new Color(sh, sh + 0.03f, sh + 0.025f), 30, p, size, rot, -0.3f);
                    Spr(env, "rockLight", Art.Circle, new Color(1f, 1f, 1f, 0.1f), 31, p + new Vector2(-0.25f, 0.3f) * size.y, size * 0.5f, rot, -0.31f);
                }
            }

            var starColors = new[] { new Color(1f, 0.5f, 0.2f), new Color(0.9f, 0.27f, 0.27f), new Color(0.62f, 0.38f, 0.88f) };
            for (int i = 0; i < 5; i++)
            {
                float a = (i / 5f + rf() * 0.1f) * Tau;
                Spr(env, "starfish", Art.Starfish, starColors[i % 3], 32,
                    new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R * 1.04f, Vector2.one * R * 0.075f, rf() * 360f, -0.32f);
            }
        }

        void LayoutTexts()
        {
            float M = 2f * Mathf.Min(halfW, halfH);
            if (settings.roundProjection)
            {
                float rr = R * 1.12f + (circleR - R * 1.12f) * 0.5f;
                float fs = Mathf.Max(0.2f, Mathf.Min(M * 0.06f, (circleR - R * 1.12f) * 0.7f));
                timerTexts[0].SetHeight(fs); timerTexts[0].Place(new Vector2(-rr, 0f), -90f, -3f);
                timerTexts[1].SetHeight(fs); timerTexts[1].Place(new Vector2(rr, 0f), 90f, -3f);
            }
            else if (halfW - R > M * 0.16f)
            {
                float x = R + (halfW - R) * 0.5f;
                float fs = Mathf.Min(M * 0.075f, (halfW - R) * 0.35f);
                timerTexts[0].SetHeight(fs); timerTexts[0].Place(new Vector2(-x, 0f), 0f, -3f);
                timerTexts[1].SetHeight(fs); timerTexts[1].Place(new Vector2(x, 0f), 180f, -3f);
            }
            else
            {
                float y = R + (halfH - R) * 0.5f;
                float fs = Mathf.Max(0.2f, Mathf.Min(M * 0.06f, (halfH - R) * 0.9f));
                timerTexts[0].SetHeight(fs); timerTexts[0].Place(new Vector2(0f, -y), 0f, -3f);
                timerTexts[1].SetHeight(fs); timerTexts[1].Place(new Vector2(0f, y), 180f, -3f);
            }

            for (int i = 0; i < 2; i++)
            {
                float sgn = i == 0 ? -1f : 1f, rot = i == 0 ? 0f : 180f;
                titleTexts[i].SetHeight(R * 0.2f);
                titleTexts[i].Place(new Vector2(0f, sgn * R * 0.14f), rot, -3f);
                subTexts[i].SetHeight(R * 0.065f);
                subTexts[i].Place(new Vector2(0f, sgn * R * 0.36f), rot, -3f);
            }
            bigText.SetHeight(R * 0.42f);
            bigText.Place(Vector2.zero, 0f, -3.2f);
        }

        // ================================================================ 매 프레임
        void Update()
        {
            float dt = Mathf.Clamp(Time.deltaTime, 1e-4f, 0.05f);
            clock += dt;
            if (Screen.width != lastW || Screen.height != lastH || settings.roundProjection != lastRound) Relayout();
            ApplyCalibration();
            HandleKeys();
            sfx.volume = settings.volume;

            fx.Update(dt);
            if (state != GameState.Paused)
            {
                UpdateInput(dt);
                UpdateNets(dt);
                UpdateCreatures(dt);
                UpdateSpawns(dt);
                UpdateState(dt);
            }
            Render();
        }

        void HandleKeys()
        {
            if (GUIUtility.keyboardControl != 0) return; // 설정 패널 글자 입력 중
            if (InputBridge.Down(KeyCode.F1)) showPanel = !showPanel;
            if (InputBridge.Down(KeyCode.F2)) ToggleCalibrationMarkers();
            if (InputBridge.Down(KeyCode.F11)) Screen.fullScreen = !Screen.fullScreen;
            if (InputBridge.Down(KeyCode.Space) || InputBridge.Down(KeyCode.Return))
            {
                if (state == GameState.Ready || state == GameState.Results) StartGame();
                else if (state == GameState.Paused) Resume();
            }
            if (InputBridge.Down(KeyCode.Escape))
            {
                if (state == GameState.Playing || state == GameState.Countdown) Pause();
                else if (state == GameState.Paused) Resume();
            }
            if (InputBridge.Down(KeyCode.R) || InputBridge.Down(KeyCode.Backspace)) EnterReady();
        }

        // ---------------------------------------------------------------- 입력 → 뜰채 목표 위치
        Vector2 ScreenToStage(Vector2 screen)
        {
            var w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));
            return stage.InverseTransformPoint(w);
        }

        Vector2 ViewportToStage(Vector2 vpTopLeft)
        {
            var w = cam.ViewportToWorldPoint(new Vector3(vpTopLeft.x, 1f - vpTopLeft.y, 10f));
            return stage.InverseTransformPoint(w);
        }

        void UpdateInput(float dt)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector2 vp;
                if (tracker != null && tracker.TryGet(i, 0.3, out vp))
                {
                    nets[i].external = true;
                    nets[i].target = ViewportToStage(vp);
                }
                else nets[i].external = false;
            }

            InputBridge.GetPointers(pointers);
            seenPointers.Clear();
            foreach (var p in pointers)
            {
                seenPointers.Add(p.id);
                if (!knownPointers.Contains(p.id))
                {
                    knownPointers.Add(p.id);
                    if (!PointerOverPanel(p.screen))
                    {
                        var sp = ScreenToStage(p.screen);
                        int best = -1;
                        float bd = NETR * 3f;
                        for (int i = 0; i < 4; i++)
                        {
                            if (grabs.ContainsValue(i)) continue;
                            float d = Vector2.Distance(nets[i].pos, sp);
                            if (d < bd) { bd = d; best = i; }
                        }
                        if (best >= 0) { grabs[p.id] = best; nets[best].pointer = true; }
                    }
                }
                int ni;
                if (grabs.TryGetValue(p.id, out ni)) nets[ni].target = ScreenToStage(p.screen);
            }
            releaseList.Clear();
            foreach (var id in knownPointers) if (!seenPointers.Contains(id)) releaseList.Add(id);
            foreach (var id in releaseList)
            {
                knownPointers.Remove(id);
                int ni;
                if (grabs.TryGetValue(id, out ni)) { nets[ni].pointer = false; grabs.Remove(id); }
            }

            for (int i = 0; i < 4; i++)
            {
                var n = nets[i];
                if (n.pointer || n.external) continue;
                var k = MoveKeys[i];
                float dx = (InputBridge.Held(k[3]) ? 1f : 0f) - (InputBridge.Held(k[2]) ? 1f : 0f);
                float dy = (InputBridge.Held(k[0]) ? 1f : 0f) - (InputBridge.Held(k[1]) ? 1f : 0f);
                n.target = (dx != 0f || dy != 0f) ? n.pos + new Vector2(dx, dy).normalized * KEYSPD * R * dt : n.pos;
            }
        }

        void UpdateNets(float dt)
        {
            for (int i = 0; i < 4; i++)
            {
                var n = nets[i];
                var old = n.pos;
                if (n.pointer || n.external) n.pos += (n.target - n.pos) * (1f - Mathf.Exp(-dt * 28f));
                else n.pos = n.target;

                if (settings.roundProjection)
                {
                    if (n.pos.magnitude > circleR) n.pos = n.pos.normalized * circleR;
                }
                else n.pos = new Vector2(Mathf.Clamp(n.pos.x, -halfW, halfW), Mathf.Clamp(n.pos.y, -halfH, halfH));

                n.vel = (n.pos - old) / dt;
                n.speed += (n.vel.magnitude - n.speed) * (1f - Mathf.Exp(-dt * 10f));
                n.stunT = Mathf.Max(0f, n.stunT - dt);

                if (n.pos.magnitude < R && n.speed > 0.25f * R)
                {
                    n.rippleT -= dt;
                    if (n.rippleT <= 0f) { fx.Ripple(n.pos, NETR * 0.7f, new Color(0.85f, 0.98f, 0.96f)); n.rippleT = 0.15f; }
                }

                if (state != GameState.Playing || n.load.Count == 0) { n.fastT = 0f; continue; }
                if (n.speed > ESCAPE * R)
                {
                    n.fastT += dt;
                    if (n.fastT > 0.1f) { DropLoad(i, "놓쳤다!"); continue; }
                }
                else n.fastT = 0f;
                if (Vector2.Distance(n.pos, buckets[i].pos) < buckets[i].radius * 0.8f) Deposit(i);
            }
        }

        // ---------------------------------------------------------------- 생물
        void UpdateCreatures(float dt)
        {
            foreach (var n in nets) n.prog = 0f;
            for (int i = creatures.Count - 1; i >= 0; i--)
            {
                var c = creatures[i];
                c.Update(dt, this);
                if (c.fadingOut && c.alpha <= 0f) { c.Destroy(); creatures.RemoveAt(i); }
            }
            if (!IsPlaying) return;

            for (int ci = 0; ci < creatures.Count; ci++)
            {
                var c = creatures[ci];
                if (c.state != Creature.State.Swim || c.alpha < 1f || c.fadingOut) continue;

                if (c.IsJelly)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        var n = nets[j];
                        if (n.stunT <= 0f && Vector2.Distance(c.pos, n.pos) < NETR * 0.85f + c.Radius(R) * 0.4f) { Sting(j, c); break; }
                    }
                    continue;
                }
                if (!c.def.catchable) continue;

                int hit = -1;
                for (int j = 0; j < 4; j++)
                {
                    var n = nets[j];
                    if (n.load.Count >= CAP || n.stunT > 0f || n.speed > CATCH * R) continue;
                    if (Vector2.Distance(c.pos, n.pos) < NETR * 0.72f) { hit = j; break; }
                }
                if (hit >= 0)
                {
                    if (c.holdNet != hit) { c.holdNet = hit; c.hold = 0f; }
                    c.hold += dt;
                    float req = c.HoldRequired;
                    nets[hit].prog = Mathf.Max(nets[hit].prog, c.hold / req);
                    if (c.hold >= req) Catch(c, hit);
                }
                else
                {
                    c.hold = Mathf.Max(0f, c.hold - dt * 2f);
                    if (c.hold <= 0f) c.holdNet = -1;
                }
            }
        }

        SpeciesDef PickSpecies()
        {
            float total = 0f;
            foreach (var s in species) if (s.catchable && s.spawnWeight > 0f) total += s.spawnWeight;
            if (total <= 0f) return null;
            float r = Random.value * total;
            foreach (var s in species)
            {
                if (!s.catchable || s.spawnWeight <= 0f) continue;
                r -= s.spawnWeight;
                if (r <= 0f) return s;
            }
            return null;
        }

        void Spawn(SpeciesDef def)
        {
            if (def == null) return;
            float a = Random.value * Mathf.PI * 2f, r = Mathf.Sqrt(Random.value) * R * 0.75f;
            var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            creatures.Add(new Creature(def, stage, p));
            fx.Ripple(p, R * 0.08f, new Color(0.8f, 0.97f, 0.95f));
        }

        void UpdateSpawns(float dt)
        {
            for (int i = spawnQueue.Count - 1; i >= 0; i--)
            {
                spawnQueue[i] -= dt;
                if (spawnQueue[i] <= 0f) { spawnQueue.RemoveAt(i); Spawn(PickSpecies()); }
            }
            int active = 0, jellies = 0;
            Creature anyJelly = null;
            foreach (var c in creatures)
            {
                if (c.fadingOut || !c.InPond) continue;
                if (c.IsJelly) { jellies++; anyJelly = c; } else active++;
            }
            if (active + spawnQueue.Count < settings.creatureCount) spawnQueue.Add(0.5f);
            if (jellyDef != null && jellies < settings.jellyCount) Spawn(jellyDef);
            else if (jellies > settings.jellyCount && anyJelly != null) anyJelly.fadingOut = true;
        }

        // ---------------------------------------------------------------- 잡기 · 놓치기 · 넣기
        Vector2 Up(float rotDeg)
        {
            float r = rotDeg * Mathf.Deg2Rad;
            return new Vector2(-Mathf.Sin(r), Mathf.Cos(r));
        }

        void Catch(Creature c, int i)
        {
            c.Catch(i);
            nets[i].load.Add(c);
            fx.Ripple(c.pos, NETR * 1.2f, Color.white);
            sfx.Play("catch");
        }

        void DropLoad(int i, string text)
        {
            var n = nets[i];
            foreach (var c in n.load) c.StartFly(n.vel, this);
            n.load.Clear();
            n.fastT = 0f;
            float rot = buckets[i].textRot;
            fx.Pop(n.pos + Up(rot) * NETR * 1.4f, text, Color.white, rot, NETR * 0.55f);
            sfx.Play("drop");
        }

        void Deposit(int i)
        {
            var n = nets[i];
            int pts = 0;
            foreach (var c in n.load)
            {
                pts += c.def.points;
                c.ToBucket(i);
                inBucket[i].Add(c);
                spawnQueue.Add(Random.Range(1f, 2.2f));
            }
            counts[i] += n.load.Count;
            scores[i] += pts;
            n.load.Clear();
            while (inBucket[i].Count > 8)
            {
                var old = inBucket[i][0];
                inBucket[i].RemoveAt(0);
                creatures.Remove(old);
                old.Destroy();
            }
            var b = buckets[i];
            fx.Pop(b.pos + Up(b.textRot) * b.radius * 1.35f, "+" + pts, PlayerColors[i], b.textRot, b.radius * 0.5f);
            fx.Ripple(b.pos, b.radius * 1.1f, Color.white);
            sfx.Play("deposit");
        }

        void Sting(int i, Creature jelly)
        {
            var n = nets[i];
            if (n.load.Count > 0) DropLoad(i, "찌릿!");
            else
            {
                float rot = buckets[i].textRot;
                fx.Pop(n.pos + Up(rot) * NETR * 1.4f, "찌릿!", new Color(0.85f, 0.7f, 1f), rot, NETR * 0.55f);
            }
            n.stunT = 1.6f;
            jelly.Bounce(n.pos, R);
            fx.Ripple(n.pos, NETR * 1.5f, new Color(0.8f, 0.6f, 1f));
            sfx.Play("sting");
        }

        void ReleaseAllLoads()
        {
            foreach (var n in nets)
            {
                foreach (var c in n.load) c.Release();
                n.load.Clear();
                n.fastT = 0f;
            }
        }

        void ResetNets()
        {
            for (int i = 0; i < 4; i++)
            {
                var b = buckets[i];
                float a = Mathf.Atan2(-b.pos.y, -b.pos.x);
                var p = b.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * b.radius * 1.9f;
                var n = nets[i];
                n.pos = n.target = p;
                n.vel = Vector2.zero;
                n.speed = 0f; n.stunT = 0f; n.prog = 0f;
            }
        }

        void ClearBuckets()
        {
            for (int i = 0; i < 4; i++)
            {
                foreach (var c in inBucket[i]) { creatures.Remove(c); c.Destroy(); }
                inBucket[i].Clear();
            }
        }

        // ================================================================ 게임 흐름
        void StartGame()
        {
            ReleaseAllLoads();
            ClearBuckets();
            foreach (var c in creatures) c.Destroy();
            creatures.Clear();
            spawnQueue.Clear();
            fx.Clear();
            for (int i = 0; i < 4; i++) { scores[i] = 0; counts[i] = 0; buckets[i].SetRank(null, Color.white); }
            for (int i = 0; i < settings.creatureCount; i++) Spawn(PickSpecies());
            ResetNets();
            remaining = settings.durationSec;
            countT = 3f;
            lastTick = 99;
            startFlashT = 0f;
            state = GameState.Countdown;
        }

        void EnterReady()
        {
            ReleaseAllLoads();
            ClearBuckets();
            for (int i = 0; i < 4; i++) { scores[i] = 0; counts[i] = 0; buckets[i].SetRank(null, Color.white); }
            state = GameState.Ready;
        }

        void Pause() { stateBeforePause = state; state = GameState.Paused; }

        void Resume() { if (state == GameState.Paused) state = stateBeforePause; }

        void EndGame()
        {
            state = GameState.Results;
            resultT = 0f;
            ReleaseAllLoads();
            sfx.Play("end");

            // 점수가 같으면 마릿수가 많은 쪽이 앞섭니다.
            for (int i = 0; i < 4; i++)
            {
                int better = 0;
                for (int j = 0; j < 4; j++)
                    if (scores[j] > scores[i] || (scores[j] == scores[i] && counts[j] > counts[i])) better++;
                buckets[i].SetRank((better + 1) + "등", better == 0 && scores[i] > 0 ? PlayerColors[i] : Color.white);
            }
        }

        void UpdateState(float dt)
        {
            switch (state)
            {
                case GameState.Countdown:
                {
                    countT -= dt;
                    int c = Mathf.CeilToInt(countT);
                    if (c != lastTick && c > 0) { lastTick = c; sfx.Play("count"); }
                    if (countT <= 0f) { state = GameState.Playing; startFlashT = 0.8f; lastTick = 99; sfx.Play("start"); }
                    break;
                }
                case GameState.Playing:
                {
                    remaining -= dt;
                    startFlashT -= dt;
                    int c = Mathf.CeilToInt(remaining);
                    if (c <= 5 && c > 0 && c != lastTick) { lastTick = c; sfx.Play("tick"); }
                    if (remaining <= 0f) { remaining = 0f; EndGame(); }
                    break;
                }
                case GameState.Results:
                    resultT += dt;
                    if (resultT > 60f) EnterReady();
                    break;
            }
        }

        // ================================================================ 그리기
        void Render()
        {
            foreach (var n in nets) n.Render(this, clock);
            for (int i = 0; i < 4; i++) buckets[i].SetScore(scores[i], counts[i]);

            foreach (var w in seaweeds)
            {
                var p = w.root;
                float seg = w.length / (weedBuf.Length - 1);
                for (int k = 0; k < weedBuf.Length; k++)
                {
                    float s = k / (weedBuf.Length - 1f);
                    float a = w.angle + Mathf.Sin(clock * 1.3f + w.phase + s * 2f) * 0.35f * s;
                    weedBuf[k] = p;
                    p += new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * seg;
                }
                w.line.SetPositions(weedBuf);
            }

            bool showTimer = state == GameState.Countdown || state == GameState.Playing || state == GameState.Paused;
            int t = Mathf.Max(0, Mathf.CeilToInt(remaining));
            string timer = (t / 60) + ":" + (t % 60).ToString("00");
            bool warn = state == GameState.Playing && remaining <= 10f;
            foreach (var tt in timerTexts)
            {
                tt.Visible = showTimer;
                if (!showTimer) continue;
                tt.Text = timer;
                tt.Color = warn ? new Color(1f, 0.42f, 0.35f) : Color.white;
            }

            string big = null;
            if (state == GameState.Countdown) big = Mathf.Max(1, Mathf.CeilToInt(countT)).ToString();
            else if (state == GameState.Playing && startFlashT > 0f) big = "시작!";
            else if (state == GameState.Paused) big = "일시정지";
            bigText.Visible = big != null;
            if (big != null) bigText.Text = big;

            string title = null, sub = null;
            Color titleColor = Color.white;
            if (state == GameState.Ready)
            {
                title = "뜰채 바다";
                sub = "스페이스바를 누르면 시작";
            }
            else if (state == GameState.Results)
            {
                WinnerText(out title, out titleColor);
                sub = "스페이스바: 다시 하기";
            }
            for (int i = 0; i < 2; i++)
            {
                titleTexts[i].Visible = title != null;
                subTexts[i].Visible = sub != null;
                if (title != null) { titleTexts[i].Text = title; titleTexts[i].Color = titleColor; }
                if (sub != null) subTexts[i].Text = sub;
            }
        }

        void WinnerText(out string text, out Color color)
        {
            int top = Mathf.Max(Mathf.Max(scores[0], scores[1]), Mathf.Max(scores[2], scores[3]));
            color = Color.white;
            if (top == 0) { text = "아무도 못 잡았어요"; return; }
            int bestCount = -1;
            for (int i = 0; i < 4; i++) if (scores[i] == top) bestCount = Mathf.Max(bestCount, counts[i]);
            var winners = new List<string>();
            int last = 0;
            for (int i = 0; i < 4; i++)
                if (scores[i] == top && counts[i] == bestCount) { winners.Add(PlayerNames[i]); last = i; }
            if (winners.Count > 1) text = string.Join(" · ", winners.ToArray()) + " 공동 우승";
            else { text = PlayerNames[last] + " 우승!"; color = PlayerColors[last]; }
        }

        // ================================================================ 추적
        void StartTracking()
        {
            if (tracker == null) tracker = new TrackingReceiver();
            tracker.Start(settings.trackingPort);
        }

        void StopTracking()
        {
            if (tracker != null) { tracker.Dispose(); tracker = null; }
        }

        void OnDestroy() { StopTracking(); }

        void OnApplicationQuit() { StopTracking(); }
    }
}
