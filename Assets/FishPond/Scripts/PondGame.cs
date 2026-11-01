using System.Collections.Generic;
using UnityEngine;

namespace FishPond
{
    /// <summary>
    /// 두 게임(뜰채 바다, 상어 구조대)이 함께 쓰는 바탕.
    /// 카메라·둥근 무대·보정, 뜰채 입력(키보드/마우스/터치/카메라 추적), 잡기 판정,
    /// 게임 흐름(준비→카운트다운→진행→결과), 타이머·제목 글자, 산호초 꾸미기, F1 패널을 맡습니다.
    /// 게임마다 다른 것(배치, 배경, 점수, 생물이 다닐 수 있는 구역)은 자식 클래스가 정합니다.
    ///
    /// 키: Space 시작 · Esc 일시정지 · R 처음 화면 · F1 설정 패널 · F2 추적 보정 마커 · F11 전체화면
    /// </summary>
    public abstract partial class PondGame : MonoBehaviour
    {
        // ---------------------------------------------------------------- 판정값 (속도는 R/초 기준)
        public const int CAP = 2;            // 뜰채 하나에 담기는 최대 마릿수
        public const float HOLD = 0.4f;      // 생물 위에 이만큼 머물면 잡힘 (초)
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
        /// <summary>플레이어 자리 각도: P1 왼쪽 아래, P2 오른쪽 아래, P3 오른쪽 위, P4 왼쪽 위</summary>
        protected static readonly float[] PlayerDeg = { 225f, 315f, 45f, 135f };

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

        public float R { get; protected set; }
        public float NETR { get; protected set; }
        public bool IsPlaying { get { return state == GameState.Playing; } }

        public readonly Net[] nets = new Net[4];
        public Fx fx { get; private set; }
        public Sfx sfx { get; private set; }

        protected float halfW, halfH, circleR;
        protected Camera cam;
        protected Transform stage, env;
        protected readonly List<Creature> creatures = new List<Creature>();
        protected List<SpeciesDef> species;

        protected FloorText[] timerTexts, titleTexts, subTexts;
        protected FloorText bigText;
        protected float remaining, clock;
        float countT, resultT, startFlashT;
        int lastTick = 99, lastW, lastH;

        TrackingReceiver tracker;
        readonly Dictionary<int, int> grabs = new Dictionary<int, int>();
        readonly HashSet<int> knownPointers = new HashSet<int>();
        readonly HashSet<int> seenPointers = new HashSet<int>();
        readonly List<int> releaseList = new List<int>();
        readonly List<InputBridge.Pointer> pointers = new List<InputBridge.Pointer>();

        // ================================================================ 게임마다 정하는 것
        /// <summary>설정 저장 이름 (게임마다 따로 저장)</summary>
        protected abstract string SettingsKey { get; }
        /// <summary>패널·대기 화면에 쓰는 게임 이름</summary>
        public abstract string GameTitle { get; }
        /// <summary>circleR가 정해진 뒤 R, NETR, 게임 요소 배치</summary>
        protected abstract void LayoutGame();
        /// <summary>배경 만들기 (env 아래에)</summary>
        protected abstract void BuildEnvironment();
        protected abstract void OnStartGame();
        protected abstract void OnEnterReady();
        protected abstract void OnEndGame();
        /// <summary>매 프레임 게임 고유 처리 (생물 보충 등)</summary>
        protected abstract void UpdateGame(float dt);
        /// <summary>뜰채 i가 움직인 뒤 (진행 중일 때): 통에 넣기·구출 판정 등</summary>
        protected abstract void OnNetMoved(int i, float dt);
        /// <summary>뜰채 i의 시작 위치</summary>
        protected abstract Vector2 NetHome(int i);
        /// <summary>결과 화면 제목</summary>
        protected abstract void ResultTitle(out string title, out string sub, out Color color);
        /// <summary>게임 고유 그리기 (점수판 등)</summary>
        protected virtual void RenderGame() { }
        /// <summary>F1 패널의 게임 고유 설정</summary>
        protected virtual void DrawGameSettings() { }

        // ---------------------------------------------------------------- 생물이 다닐 수 있는 구역 (기본: 가운데 원)
        /// <summary>생물이 헤엄쳐 갈 다음 목표 지점</summary>
        public virtual Vector2 RandomTarget(Creature c)
        {
            float a = Random.value * Mathf.PI * 2f, r = Mathf.Sqrt(Random.value) * R * 0.75f;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        /// <summary>구역 가장자리 근처인가 (f: 0.7~0.8, 클수록 바깥)</summary>
        public virtual bool NearEdge(Creature c, Vector2 p, float f) { return p.magnitude > R * f; }
        /// <summary>구역 안쪽으로 향하는 방향</summary>
        public virtual Vector2 Inward(Creature c, Vector2 p) { return -p; }
        /// <summary>구역 밖으로 나가지 않게 (f: 보통 0.85~0.9)</summary>
        public virtual Vector2 ClampToZone(Creature c, Vector2 p, float f)
        {
            float m = p.magnitude;
            return m > R * f ? p / m * R * f : p;
        }
        /// <summary>근처의 천적 (상어 등). 있으면 도망칩니다.</summary>
        public virtual bool Threat(Creature c, out Vector2 from) { from = Vector2.zero; return false; }
        /// <summary>낚시통 안 위치 (통이 있는 게임만)</summary>
        public virtual bool BucketSlot(int b, out Vector2 pos, out float radius) { pos = Vector2.zero; radius = 1f; return false; }
        /// <summary>뜰채 손잡이가 향할 곳 = 플레이어가 서 있는 쪽</summary>
        public virtual Vector2 HandleAnchor(int i)
        {
            float a = PlayerDeg[i] * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * circleR;
        }
        /// <summary>플레이어 i가 읽기 좋은 글자 회전 (원 중심 쪽이 위)</summary>
        public float PlayerTextRot(int i)
        {
            float a = PlayerDeg[i] * Mathf.Deg2Rad;
            return Mathf.Atan2(-Mathf.Sin(a), -Mathf.Cos(a)) * Mathf.Rad2Deg - 90f;
        }

        // ================================================================ 시작
        protected virtual void Start()
        {
            if (!ignoreSavedSettings) settings = PondSettings.Load(SettingsKey);
            species = SpeciesCatalog.LoadOrDefault();
            baseRotations = new Dictionary<SpeciesId, Vector3>();
            foreach (var d in species) baseRotations[d.id] = d.prefabRotation;
            ApplyFlips();

            SetupCamera();
            stage = new GameObject("Stage").transform;
            fx = new Fx(stage);
            sfx = new Sfx(gameObject) { volume = settings.volume };
            for (int i = 0; i < 4; i++) nets[i] = new Net(i, PlayerColors[i], stage);

            var subColor = new Color(0.8f, 0.93f, 0.92f);
            timerTexts = new[] { new FloorText(stage, "timerA", 80, Color.white, 1f), new FloorText(stage, "timerB", 80, Color.white, 1f) };
            titleTexts = new[] { new FloorText(stage, "titleA", 80, Color.white, 1f), new FloorText(stage, "titleB", 80, Color.white, 1f) };
            subTexts = new[] { new FloorText(stage, "subA", 80, subColor, 1f), new FloorText(stage, "subB", 80, subColor, 1f) };
            bigText = new FloorText(stage, "big", 82, Color.white, 1f);

            CreateGameObjects();
            Relayout();
            ApplyCalibration();
            ResetNets();
            if (settings.trackingEnabled) StartTracking();
            EnterReady();
        }

        /// <summary>Relayout 전에 게임 고유 오브젝트를 만들 기회</summary>
        protected virtual void CreateGameObjects() { }

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
            cam.backgroundColor = Color.black;
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

        // ================================================================ 화면 배치 (모든 것이 둥근 화면 안)
        void Relayout()
        {
            lastW = Screen.width; lastH = Screen.height;
            float oldR = R;
            halfH = cam.orthographicSize;
            halfW = halfH * cam.aspect;
            circleR = Mathf.Min(halfW, halfH) * 0.98f;
            LayoutGame();

            if (oldR > 0f && Mathf.Abs(oldR - R) > 1e-4f)
            {
                float k = R / oldR;
                foreach (var c in creatures) c.Rescale(k);
                foreach (var n in nets) { n.pos *= k; n.target *= k; }
                OnRescale(k);
            }

            if (env != null) Destroy(env.gameObject);
            env = new GameObject("Environment").transform;
            env.SetParent(stage, false);
            swayers.Clear();
            BuildEnvironment();
            LayoutTexts();
        }

        protected virtual void OnRescale(float k) { }

        void ApplyCalibration()
        {
            stage.localPosition = new Vector3(settings.calibOffsetX, settings.calibOffsetY, 0f);
            stage.localScale = new Vector3(settings.calibScale * settings.calibAspect, settings.calibScale, 1f);
        }

        /// <summary>원 밖으로 나가지 않게 (여유 margin만큼 안쪽)</summary>
        public Vector2 KeepInside(Vector2 p, float margin)
        {
            float lim = circleR - margin;
            return p.magnitude > lim ? p.normalized * lim : p;
        }

        protected static Vector2 Dir(float deg)
        {
            float a = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        protected static Vector2 Up(float rotDeg)
        {
            float r = rotDeg * Mathf.Deg2Rad;
            return new Vector2(-Mathf.Sin(r), Mathf.Cos(r));
        }

        protected static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        protected SpriteRenderer Spr(Transform parent, string name, Sprite sp, Color c, int order, Vector2 pos, Vector2 scale, float rotDeg, float z)
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

        /// <summary>둥근 바닷물 + 모래 바닥 (셰이더)</summary>
        Dictionary<SpeciesId, Vector3> baseRotations;

        /// <summary>운영 설정의 "앞뒤 반대" 목록을 생물 모델 회전에 반영 (새로 나오는 생물부터)</summary>
        protected void ApplyFlips()
        {
            if (baseRotations == null) return;
            foreach (var d in species)
            {
                Vector3 r;
                if (!baseRotations.TryGetValue(d.id, out r)) continue;
                d.prefabRotation = IsFlipped(d.id) ? new Vector3(r.x, r.y + 180f, r.z) : r;
            }
        }

        public bool IsFlipped(SpeciesId id) { return IsFlipped(id.ToString()); }

        public bool IsFlipped(string key)
        {
            foreach (var t in (settings.flippedSpecies ?? "").Split(','))
                if (t.Trim() == key) return true;
            return false;
        }

        public void SetFlipped(SpeciesId id, bool on) { SetFlipped(id.ToString(), on); }

        public void SetFlipped(string key, bool on)
        {
            var list = new List<string>();
            foreach (var t in (settings.flippedSpecies ?? "").Split(','))
                if (t.Trim().Length > 0 && t.Trim() != key) list.Add(t.Trim());
            if (on) list.Add(key);
            settings.flippedSpecies = string.Join(",", list.ToArray());
            ApplyFlips();
        }

        Material causticMat;
        List<Texture2D> causticFrames;

        /// <summary>
        /// 물: 바닥 모델(에셋) + 물색·물결 빛(코스틱 텍스처 에셋). 에셋이 없으면 예전 셰이더 물로 대신함.
        /// </summary>
        protected void AddWater(Material fallback, float radius, int order)
        {
            var cat = SpeciesCatalog.Load();
            var sh = Shader.Find("FishPond/Caustics");
            bool hasFrames = cat != null && cat.causticFrames != null && cat.causticFrames.Exists(t => t != null);
            if (cat == null || cat.seabedPrefab == null || sh == null || !hasFrames)
            {
                AddShaderWater(fallback, radius, order);
                return;
            }
            // 바닥: 모델을 원 전체를 덮게 펴서 맨 뒤(z≈1)에 납작하게
            var bed = new DecorDef { kind = DecorKind.Rock, prefab = cat.seabedPrefab, rotation = cat.seabedRotation, scale = 1f };
            var bedT = Decor.Place(bed, env, Vector2.zero, radius * 2.7f, 0f, 0.97f, 0.02f, true);
            if (bedT != null) bedT.name = "Seabed";

            // 물 층: 바닥 바로 앞(z=0.95). 생물·장식이 앞에 있으면 깊이 테스트로 가려짐
            causticFrames = cat.causticFrames.FindAll(t => t != null);
            causticMat = new Material(sh) { name = "FishPond Caustics" };
            causticMat.SetTexture("_Tex0", causticFrames[0]);
            causticMat.SetTexture("_Tex1", causticFrames[causticFrames.Count > 1 ? 1 : 0]);
            var layer = new GameObject("WaterLayer");
            layer.transform.SetParent(env, false);
            layer.transform.localPosition = new Vector3(0f, 0f, 0.95f);
            // 바닥 모델이 원 밖으로 삐져나오므로 물 층을 크게 깔고, 원 밖은 셰이더에서 검게 덮음
            layer.transform.localScale = new Vector3(radius * 2.04f * 3f, radius * 2.04f * 3f, 1f);
            causticMat.SetFloat("_Radius", 1f / 3f);
            layer.AddComponent<MeshFilter>().sharedMesh = Art.Quad;
            var mr = layer.AddComponent<MeshRenderer>();
            mr.sharedMaterial = causticMat;
            mr.sortingOrder = order;
        }

        /// <summary>물결 빛 애니메이션: 프레임 두 장을 부드럽게 넘김 (초당 약 10장)</summary>
        void UpdateCaustics()
        {
            if (causticMat == null || causticFrames == null || causticFrames.Count == 0) return;
            float f = clock * 10f;
            int i = Mathf.FloorToInt(f);
            int n = causticFrames.Count;
            causticMat.SetTexture("_Tex0", causticFrames[((i % n) + n) % n]);
            causticMat.SetTexture("_Tex1", causticFrames[(((i + 1) % n) + n) % n]);
            causticMat.SetFloat("_Blend", f - i);
        }

        protected MeshRenderer AddShaderWater(Material mat, float radius, int order)
        {
            var water = new GameObject("Water");
            water.transform.SetParent(env, false);
            water.transform.localPosition = new Vector3(0f, 0f, 1f);
            water.transform.localScale = new Vector3(radius * 2.04f, radius * 2.04f, 1f);
            water.AddComponent<MeshFilter>().sharedMesh = Art.Quad;
            var wr = water.AddComponent<MeshRenderer>();
            wr.sharedMaterial = mat;
            wr.sortingOrder = order;
            return wr;
        }

        protected void AddEdgeShade()
        {
            Spr(env, "edgeShade", Art.EdgeShade, Color.white, 37, Vector2.zero, Vector2.one * circleR * 2f, 0f, 0.1f);
        }

        protected virtual void LayoutTexts()
        {
            float C = circleR;
            float fs = C * 0.075f;
            float rr = C * 0.845f;
            timerTexts[0].SetHeight(fs); timerTexts[0].Place(new Vector2(-rr, 0f), -90f, -3f);
            timerTexts[1].SetHeight(fs); timerTexts[1].Place(new Vector2(rr, 0f), 90f, -3f);
            foreach (var t in timerTexts) t.Backdrop(new Vector2(fs * 3.4f, fs * 1.5f), new Color(0.01f, 0.06f, 0.1f, 0.6f));

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

        // ================================================================ 산호초 꾸미기 (두 게임 공통)
        protected class Swayer { public Transform t; public Vector3 baseScale; public float phase, baseRot; }
        protected readonly List<Swayer> swayers = new List<Swayer>();
        float bubbleT;

        /// <summary>
        /// 산호초 고리 채우기. spot()이 산호 놓을 자리를 고르고, density로 양을 조절합니다.
        /// </summary>
        protected void BuildReef(System.Random rnd, System.Func<float, float, Vector2> spot, float rMin, float rMax, float density)
        {
            System.Func<float> rf = () => (float)rnd.NextDouble();
            float C = circleR;
            System.Func<int, int> Count = n => Mathf.Max(1, Mathf.RoundToInt(n * density));

            // 모든 장식은 3D 에셋 (없는 종류는 건너뜀)
            for (int i = 0; i < Count(22); i++)
                PlaceDecor(rnd, DecorKind.Rock, spot(rMin * 0.98f, rMax), C * (0.06f + rf() * 0.07f), 0.3f, 0.5f, false);
            for (int i = 0; i < Count(10); i++)
                PlaceDecor(rnd, i % 2 == 0 ? DecorKind.Starfish : DecorKind.Shell, spot(rMin, rMax * 0.98f), C * 0.05f, 0.28f, 0.15f, false);
            for (int i = 0; i < Count(115); i++)
            {
                var p = spot(rMin, rMax);
                float outer = Mathf.InverseLerp(rMin, C, p.magnitude);
                PlaceDecor(rnd, DecorKind.Coral, p, C * (0.09f + rf() * 0.08f + outer * 0.08f), 0.22f, 0.6f, false);
            }
            for (int i = 0; i < Count(8); i++)
                PlaceDecor(rnd, DecorKind.Anemone, spot(rMin * 1.03f, rMax * 0.96f), C * 0.1f, 0.18f, 0.4f, true);
            for (int i = 0; i < Count(7); i++)
                PlaceDecor(rnd, DecorKind.Urchin, spot(rMin, rMax * 0.98f), C * (0.035f + rf() * 0.02f), 0.17f, 0.2f, false);
            for (int i = 0; i < Count(26); i++)
            {
                var root = spot(rMin * 1.01f, rMax * 0.99f);
                AddKelp(root, Mathf.Atan2(-root.y, -root.x) + (rf() - 0.5f) * 1.6f, C * (0.09f + rf() * 0.1f), 0f, false, rf() * 10f);
            }
        }

        /// <summary>
        /// 물 가운데 바닥의 작은 산호·바위·수초 무더기 (생물 아래에 깔림).
        /// spot()이 무더기 중심을 고르고, 중심 둘레에 몇 개를 모아 놓음.
        /// </summary>
        protected void BuildFloorClusters(System.Random rnd, System.Func<Vector2> spot, int clusters, float unit, bool corals)
        {
            System.Func<float> rf = () => (float)rnd.NextDouble();
            for (int c = 0; c < clusters; c++)
            {
                var center = spot();
                PlaceDecor(rnd, DecorKind.Rock, center, unit * (1.1f + rf() * 0.6f), 0.5f, 0.35f, false);
                int n = 2 + rnd.Next(3);
                for (int k = 0; k < n; k++)
                {
                    float a = rf() * Mathf.PI * 2f;
                    var p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * unit * (0.6f + rf() * 0.7f);
                    float pick = rf();
                    if (corals && pick < 0.45f) PlaceDecor(rnd, DecorKind.Coral, p, unit * (0.7f + rf() * 0.5f), 0.48f, 0.35f, false);
                    else if (pick < 0.8f) PlaceDecor(rnd, DecorKind.Seaweed, p, unit * (0.8f + rf() * 0.5f), 0.47f, 0.35f, true);
                    else PlaceDecor(rnd, rf() < 0.5f ? DecorKind.Starfish : DecorKind.Shell, p, unit * 0.45f, 0.52f, 0.1f, false);
                }
            }
        }

        /// <summary>3D 장식 하나 놓기. sway = 물결에 살랑임</summary>
        protected Transform PlaceDecor(System.Random rnd, DecorKind kind, Vector2 p, float size, float zFront, float maxDepth, bool sway)
        {
            var d = Decor.Pick(rnd, kind);
            if (d == null) return null;
            float yaw = (float)rnd.NextDouble() * 360f;
            var t = Decor.Place(d, env, p, size, yaw, zFront, maxDepth);
            if (t != null && sway)
                swayers.Add(new Swayer { t = t, baseScale = t.localScale, phase = (float)rnd.NextDouble() * 10f, baseRot = yaw });
            return t;
        }

        /// <summary>해초 (3D 에셋). 뿌리에서 바깥쪽으로 length만큼 덮도록 놓음</summary>
        protected void AddKelp(Vector2 root, float angle, float length, float width, bool brown, float phase)
        {
            var rnd = new System.Random(Mathf.RoundToInt(phase * 1000f) + Mathf.RoundToInt(root.x * 7919f));
            var p = root + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length * 0.35f;
            PlaceDecor(rnd, DecorKind.Seaweed, p, length, 0.14f, 0.5f, true);
        }

        /// <summary>해초·말미잘 흔들림, 거품</summary>
        void UpdateAmbient(float dt)
        {
            UpdateCaustics();
            foreach (var sw in swayers)
            {
                if (sw.t == null) continue;
                float k = 1f + 0.05f * Mathf.Sin(clock * 1.7f + sw.phase);
                sw.t.localScale = new Vector3(sw.baseScale.x * k, sw.baseScale.y * k, sw.baseScale.z);
                sw.t.localRotation = Quaternion.Euler(0, 0, sw.baseRot + Mathf.Sin(clock * 0.9f + sw.phase) * 8f);
            }
            bubbleT -= dt;
            if (bubbleT <= 0f)
            {
                bubbleT = Random.Range(0.15f, 0.4f);
                float a = Random.value * Mathf.PI * 2f;
                float r = Random.Range(R * 0.3f, circleR * 0.9f);
                fx.Bubble(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, circleR * Random.Range(0.01f, 0.022f));
            }
        }

        // ================================================================ 매 프레임
        protected virtual void Update()
        {
            float dt = Mathf.Clamp(Time.deltaTime, 1e-4f, 0.05f);
            clock += dt;
            if (Screen.width != lastW || Screen.height != lastH) Relayout();
            ApplyCalibration();
            HandleKeys();
            sfx.volume = settings.volume;

            fx.Update(dt);
            UpdateAmbient(dt);
            if (state != GameState.Paused)
            {
                UpdateInput(dt);
                UpdateNets(dt);
                UpdateCreatures(dt);
                UpdateGame(dt);
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
                n.pos = KeepInside(n.pos, NETR * 0.6f);

                n.vel = (n.pos - old) / dt;
                n.speed += (n.vel.magnitude - n.speed) * (1f - Mathf.Exp(-dt * 10f));
                n.stunT = Mathf.Max(0f, n.stunT - dt);

                if (n.pos.magnitude < circleR * 0.8f && n.speed > 0.25f * R)
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
                OnNetMoved(i, dt);
            }
        }

        // ---------------------------------------------------------------- 생물 · 잡기 판정
        /// <summary>이 생물을 지금 뜰채로 잡을 수 있는가 (게임별 추가 조건)</summary>
        protected virtual bool CanBeCaught(Creature c) { return c.def.catchable; }

        void UpdateCreatures(float dt)
        {
            foreach (var n in nets) n.prog = 0f;
            for (int i = creatures.Count - 1; i >= 0; i--)
            {
                var c = creatures[i];
                c.Update(dt, this);
                if (c.fadingOut && c.alpha <= 0f) { OnCreatureRemoved(c); c.Destroy(); creatures.RemoveAt(i); }
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
                if (!CanBeCaught(c)) continue;

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

        protected virtual void OnCreatureRemoved(Creature c) { }

        protected SpeciesDef PickSpecies(System.Predicate<SpeciesDef> allow)
        {
            float total = 0f;
            foreach (var s in species) if (s.catchable && s.spawnWeight > 0f && s.prefabOverride != null && allow(s)) total += s.spawnWeight;
            if (total <= 0f) return null;
            float r = Random.value * total;
            foreach (var s in species)
            {
                if (!s.catchable || s.spawnWeight <= 0f || s.prefabOverride == null || !allow(s)) continue;
                r -= s.spawnWeight;
                if (r <= 0f) return s;
            }
            return null;
        }

        protected Creature SpawnAt(SpeciesDef def, Vector2 p, int zone)
        {
            if (def == null || def.prefabOverride == null) return null;
            var c = new Creature(def, stage, p) { zone = zone };
            creatures.Add(c);
            fx.Ripple(p, R * 0.08f, new Color(0.8f, 0.97f, 0.95f));
            return c;
        }

        protected void Catch(Creature c, int i)
        {
            c.Catch(i);
            nets[i].load.Add(c);
            fx.Ripple(c.pos, NETR * 1.2f, Color.white);
            sfx.Play("catch");
        }

        protected void NetPop(int i, string text, Color color)
        {
            float rot = PlayerTextRot(i);
            fx.Pop(KeepInside(nets[i].pos + Up(rot) * NETR * 1.4f, NETR * 0.8f), text, color, rot, NETR * 0.55f);
        }

        protected void DropLoad(int i, string text)
        {
            var n = nets[i];
            foreach (var c in n.load) c.StartFly(n.vel, this);
            n.load.Clear();
            n.fastT = 0f;
            NetPop(i, text, Color.white);
            sfx.Play("drop");
        }

        void Sting(int i, Creature jelly)
        {
            var n = nets[i];
            if (n.load.Count > 0) DropLoad(i, "찌릿!");
            else NetPop(i, "찌릿!", new Color(0.85f, 0.7f, 1f));
            n.stunT = 1.6f;
            jelly.Bounce(n.pos, R);
            fx.Ripple(n.pos, NETR * 1.5f, new Color(0.8f, 0.6f, 1f));
            sfx.Play("sting");
        }

        protected void ReleaseAllLoads()
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
                var n = nets[i];
                n.pos = n.target = NetHome(i);
                n.vel = Vector2.zero;
                n.speed = 0f; n.stunT = 0f; n.prog = 0f;
            }
        }

        protected void ClearCreatures()
        {
            foreach (var c in creatures) c.Destroy();
            creatures.Clear();
        }

        // ================================================================ 게임 흐름
        public void StartGame()
        {
            ReleaseAllLoads();
            fx.Clear();
            OnStartGame();
            ResetNets();
            remaining = settings.durationSec;
            countT = 3f;
            lastTick = 99;
            startFlashT = 0f;
            state = GameState.Countdown;
        }

        public void EnterReady()
        {
            ReleaseAllLoads();
            OnEnterReady();
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
            OnEndGame();
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
                title = GameTitle;
                sub = "스페이스바를 누르면 시작";
            }
            else if (state == GameState.Results) ResultTitle(out title, out sub, out titleColor);
            for (int i = 0; i < 2; i++)
            {
                titleTexts[i].Visible = title != null;
                subTexts[i].Visible = sub != null;
                if (title != null) { titleTexts[i].Text = title; titleTexts[i].Color = titleColor; }
                if (sub != null) subTexts[i].Text = sub;
            }
            RenderGame();
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

        protected virtual void OnDestroy() { StopTracking(); }

        void OnApplicationQuit() { StopTracking(); }
    }
}
