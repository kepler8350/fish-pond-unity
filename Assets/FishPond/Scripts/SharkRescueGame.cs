using System.Collections.Generic;
using UnityEngine;

namespace FishPond
{
    /// <summary>
    /// 게임 2 · 상어 구조대 — 4명이 함께 물고기를 구하는 협동 게임.
    /// 둥근 바다를 가운데 벽으로 반 나눔:
    ///   왼쪽  = 상어 바다: 상어가 물고기를 쫓아가 잡아먹음, 물고기가 계속 새로 들어옴
    ///   오른쪽 = 안전한 산호초: 뜰채로 떠서 이쪽으로 넘기면 구출
    /// 물고기를 담은 뜰채에 상어가 부딪히면 물고기를 떨어뜨립니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class SharkRescueGame : PondGame
    {
        const int MaxSafeFish = 24;

        float zoneR, gap;
        readonly List<Shark> sharks = new List<Shark>();
        readonly List<float> spawnQueue = new List<float>();
        readonly List<Creature> safeFish = new List<Creature>();
        readonly int[] rescued = new int[4];
        int eaten;

        FloorText[] badges, statusTexts, zoneLabels;

        protected override string SettingsKey { get { return "SharkRescue.Settings.v1"; } }
        public override string GameTitle { get { return "상어 구조대"; } }

        public Vector2 SharkZoneCenter { get { return new Vector2(-zoneR * 0.5f, 0f); } }

        // 잡아먹히는 쪽은 상어가 쫓기 쉬운 보통 물고기만 (거북·가오리·오징어·문어·해파리 제외)
        static bool IsReefFish(SpeciesDef s)
        {
            switch (s.id)
            {
                case SpeciesId.Clownfish: case SpeciesId.YellowTang: case SpeciesId.Butterflyfish: case SpeciesId.Sardine:
                case SpeciesId.BlueTang: case SpeciesId.Angelfish: case SpeciesId.Pufferfish: case SpeciesId.Lionfish:
                    return true;
                default: return false;
            }
        }

        protected override void CreateGameObjects()
        {
            var light = new Color(0.85f, 0.95f, 0.95f);
            badges = new FloorText[4];
            for (int i = 0; i < 4; i++) badges[i] = new FloorText(stage, "badge" + i, 80, PlayerColors[i], 1f);
            statusTexts = new[] { new FloorText(stage, "statusA", 80, light, 1f), new FloorText(stage, "statusB", 80, light, 1f) };
            zoneLabels = new[] { new FloorText(stage, "sharkLabel", 6, new Color(1f, 0.55f, 0.5f, 0.45f), 1f), new FloorText(stage, "safeLabel", 6, new Color(0.7f, 1f, 0.85f, 0.45f), 1f) };
        }

        protected override void LayoutGame()
        {
            R = circleR * 0.66f;          // 생물 크기·속도 기준 (뜰채 바다와 같게)
            zoneR = circleR * 0.86f;      // 물고기·상어가 다니는 반지름
            gap = circleR * 0.055f;       // 가운데 벽 절반 두께
            NETR = Mathf.Max(circleR * 0.07f, R * 0.16f);
        }

        protected override void OnRescale(float k)
        {
            foreach (var s in sharks) s.pos *= k;
        }

        protected override Vector2 NetHome(int i) { return Dir(PlayerDeg[i]) * circleR * 0.72f; }

        // ---------------------------------------------------------------- 구역 (0 상어 쪽 = 왼쪽, 1 안전한 쪽 = 오른쪽)
        static float Side(int zone) { return zone == 0 ? -1f : 1f; }

        Vector2 RandomInZone(int zone, float radiusFrac)
        {
            for (int k = 0; k < 30; k++)
            {
                var p = Random.insideUnitCircle * zoneR * radiusFrac;
                p.x = Side(zone) * Mathf.Abs(p.x);
                if (Mathf.Abs(p.x) > gap + zoneR * 0.08f) return p;
            }
            return new Vector2(Side(zone) * zoneR * 0.5f, 0f);
        }

        public override Vector2 RandomTarget(Creature c) { return RandomInZone(c.zone, 0.85f); }

        public override bool NearEdge(Creature c, Vector2 p, float f)
        {
            return p.magnitude > zoneR * (f + 0.12f) || Side(c.zone) * p.x < gap + zoneR * 0.1f;
        }

        public override Vector2 Inward(Creature c, Vector2 p) { return new Vector2(Side(c.zone) * zoneR * 0.5f, 0f) - p; }

        public override Vector2 ClampToZone(Creature c, Vector2 p, float f)
        {
            float lim = zoneR * Mathf.Min(f + 0.05f, 0.95f);
            if (p.magnitude > lim) p = p.normalized * lim;
            float minX = gap + circleR * 0.02f;
            if (Side(c.zone) * p.x < minX) p.x = Side(c.zone) * minX;
            return p;
        }

        public override bool Threat(Creature c, out Vector2 from)
        {
            from = Vector2.zero;
            if (c.zone != 0) return false;
            float best = R * 0.4f;
            bool found = false;
            foreach (var s in sharks)
            {
                float d = Vector2.Distance(s.Mouth, c.pos);
                if (d < best) { best = d; from = s.pos; found = true; }
            }
            return found;
        }

        protected override bool CanBeCaught(Creature c) { return c.zone == 0 && c.def.catchable; }

        // ---------------------------------------------------------------- 상어용
        public Vector2 RandomSharkPoint() { return RandomInZone(0, 0.8f); }

        public bool NearSharkEdge(Vector2 p) { return p.magnitude > zoneR * 0.82f || p.x > -gap - zoneR * 0.12f; }

        public Vector2 ClampShark(Vector2 p, float margin)
        {
            float lim = zoneR - margin;
            if (p.magnitude > lim) p = p.normalized * lim;
            if (p.x > -gap - margin) p.x = -gap - margin;
            return p;
        }

        public bool Alive(Creature c) { return creatures.Contains(c); }

        public Creature NearestPrey(Vector2 from, float range)
        {
            Creature best = null;
            float bd = range;
            foreach (var c in creatures)
            {
                if (c.zone != 0 || c.state != Creature.State.Swim || c.fadingOut || c.alpha < 1f) continue;
                float d = Vector2.Distance(from, c.pos);
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        public void SharkEats(Shark s, Creature fish)
        {
            if (IsPlaying) eaten++;
            fx.Ripple(fish.pos, NETR * 1.2f, new Color(1f, 0.45f, 0.4f));
            fx.Pop(fish.pos, "꿀꺽", new Color(1f, 0.6f, 0.55f), 0f, NETR * 0.45f);
            sfx.Play("chomp");
            creatures.Remove(fish);
            fish.Destroy();
            if (IsPlaying) spawnQueue.Add(Random.Range(0.8f, 1.6f));
        }

        public void SharkBitesNet(int i, Shark s)
        {
            DropLoad(i, "앗! 상어!");
            nets[i].stunT = 1f;
            fx.Ripple(nets[i].pos, NETR * 1.5f, new Color(1f, 0.45f, 0.4f));
            sfx.Play("chomp");
        }

        // ---------------------------------------------------------------- 배경: 어두운 상어 바다 | 벽 | 밝은 산호초
        protected override void BuildEnvironment()
        {
            var rnd = new System.Random(11);
            System.Func<float> rf = () => (float)rnd.NextDouble();
            float C = circleR;
            AddWater(Art.Water, C, 1);

            // 상어 쪽은 깊고 어둡게
            Spr(env, "sharkShade", Art.HalfDiscLeft, new Color(0f, 0.04f, 0.12f, 0.5f), 2, Vector2.zero, Vector2.one * C * 2f, 0f, 0.9f);

            // 바깥 고리: 배지 자리(대각선)를 비워 둠
            System.Func<float, bool> angleFree = deg =>
            {
                foreach (var b in PlayerDeg) if (Mathf.Abs(Mathf.DeltaAngle(deg, b)) < 13f) return false;
                return true;
            };

            // 안전한 쪽(오른쪽): 산호초
            BuildReef(rnd, (rMin, rMax) =>
            {
                for (int k = 0; k < 40; k++)
                {
                    float deg = (rf() - 0.5f) * 160f;
                    if (!angleFree(deg)) continue;
                    return Dir(deg) * Mathf.Lerp(rMin, rMax, Mathf.Sqrt(rf()));
                }
                return new Vector2(rMax, 0f);
            }, zoneR, C * 0.96f, 0.55f);

            // 상어 쪽(왼쪽): 바위와 갈색 해초만
            for (int i = 0; i < 18; i++)
            {
                float deg = 180f + (rf() - 0.5f) * 160f;
                if (!angleFree(deg)) continue;
                var p = Dir(deg) * Mathf.Lerp(zoneR, C * 0.96f, rf());
                PlaceDecor(rnd, DecorKind.Rock, p, C * (0.06f + rf() * 0.07f), 0.3f, 0.5f, false);
            }
            for (int i = 0; i < 14; i++)
            {
                float deg = 180f + (rf() - 0.5f) * 160f;
                if (!angleFree(deg)) continue;
                var root = Dir(deg) * Mathf.Lerp(zoneR * 1.01f, C * 0.95f, rf());
                AddKelp(root, Mathf.Atan2(-root.y, -root.x) + (rf() - 0.5f) * 1.4f, C * (0.1f + rf() * 0.08f), C * 0.02f, true, rf() * 10f);
            }

            // 가운데 벽: 바위 줄 + 빛나는 그물 선
            for (float y = -C * 0.95f; y <= C * 0.95f; y += C * 0.075f)
            {
                float sz = C * (0.07f + rf() * 0.03f);
                var p = new Vector2((rf() - 0.5f) * gap * 0.8f, y);
                if (p.magnitude > C * 0.96f) continue;
                PlaceDecor(rnd, DecorKind.Rock, p, sz, 0.1f, 0.4f, false);
                if (rnd.Next(3) == 0) PlaceDecor(rnd, DecorKind.Coral, p + new Vector2(gap * 0.7f, 0f), sz * 0.8f, 0.1f, 0.4f, false);
            }
            Spr(env, "fence", Art.Square, new Color(0.55f, 0.95f, 1f, 0.35f), 30, Vector2.zero, new Vector2(C * 0.012f, C * 1.9f), 0f, 0.08f);

            // 물 가운데 바닥: 상어 쪽은 바위·수초, 안전한 쪽은 산호도
            BuildFloorClusters(rnd, () =>
            {
                for (int k = 0; k < 30; k++)
                {
                    var p = Random.insideUnitCircle * zoneR * 0.8f;
                    if (p.x < -gap - zoneR * 0.12f) return p;
                }
                return new Vector2(-zoneR * 0.5f, 0f);
            }, 4, zoneR * 0.06f, false);
            BuildFloorClusters(rnd, () =>
            {
                for (int k = 0; k < 30; k++)
                {
                    var p = Random.insideUnitCircle * zoneR * 0.8f;
                    if (p.x > gap + zoneR * 0.12f) return p;
                }
                return new Vector2(zoneR * 0.5f, 0f);
            }, 4, zoneR * 0.06f, true);
            AddEdgeShade();
        }

        protected override void LayoutTexts()
        {
            base.LayoutTexts();
            float C = circleR, fs = C * 0.075f;
            // 타이머·현황은 가운데 벽 위아래 (아래쪽 사람은 그대로, 위쪽 사람은 뒤집어서 읽음)
            timerTexts[0].SetHeight(fs); timerTexts[0].Place(new Vector2(0f, -C * 0.86f), 0f, -3f);
            timerTexts[1].SetHeight(fs); timerTexts[1].Place(new Vector2(0f, C * 0.86f), 180f, -3f);
            statusTexts[0].SetHeight(fs * 0.5f); statusTexts[0].Place(new Vector2(0f, -C * 0.755f), 0f, -3f);
            statusTexts[1].SetHeight(fs * 0.5f); statusTexts[1].Place(new Vector2(0f, C * 0.755f), 180f, -3f);
            foreach (var t in statusTexts) t.Backdrop(new Vector2(fs * 4.4f, fs * 0.9f), new Color(0.01f, 0.06f, 0.1f, 0.55f));
            for (int i = 0; i < 4; i++)
            {
                badges[i].SetHeight(C * 0.042f);
                badges[i].Place(Dir(PlayerDeg[i]) * C * 0.885f, PlayerTextRot(i), -3f);
                badges[i].Backdrop(new Vector2(C * 0.24f, C * 0.13f), new Color(0.01f, 0.06f, 0.1f, 0.6f));
            }
            zoneLabels[0].SetHeight(R * 0.1f); zoneLabels[0].Place(new Vector2(-zoneR * 0.5f, 0f), 90f, 0.5f);
            zoneLabels[1].SetHeight(R * 0.1f); zoneLabels[1].Place(new Vector2(zoneR * 0.5f, 0f), -90f, 0.5f);
            zoneLabels[0].Text = "상어 바다";
            zoneLabels[1].Text = "안전한 산호초";
        }

        // ---------------------------------------------------------------- 진행
        void SpawnFish()
        {
            var p = RandomInZone(0, 0.85f);
            // 상어 바다 바깥쪽에서 들어오는 느낌
            p = new Vector2(-Mathf.Abs(p.x), p.y);
            SpawnAt(PickSpecies(IsReefFish), p, 0);
        }

        void EnsureSharks()
        {
            int want = Mathf.Clamp(settings.sharkCount, 1, 4);
            while (sharks.Count < want) sharks.Add(new Shark(stage, RandomSharkPoint(), R * 0.42f));
            while (sharks.Count > want) { sharks[sharks.Count - 1].Destroy(); sharks.RemoveAt(sharks.Count - 1); }
        }

        protected override void UpdateGame(float dt)
        {
            EnsureSharks();
            foreach (var s in sharks) s.Update(dt, this);

            for (int i = spawnQueue.Count - 1; i >= 0; i--)
            {
                spawnQueue[i] -= dt;
                if (spawnQueue[i] <= 0f) { spawnQueue.RemoveAt(i); SpawnFish(); }
            }
            int inDanger = 0;
            foreach (var c in creatures) if (c.zone == 0 && !c.fadingOut) inDanger++;
            if (inDanger + spawnQueue.Count < settings.creatureCount) spawnQueue.Add(0.7f);

            safeFish.RemoveAll(c => !Alive(c));
            while (safeFish.Count > MaxSafeFish) { safeFish[0].fadingOut = true; safeFish.RemoveAt(0); }
        }

        protected override void OnNetMoved(int i, float dt)
        {
            var n = nets[i];
            if (n.load.Count == 0 || n.pos.x < gap + NETR * 0.4f) return;
            // 안전한 쪽으로 넘어옴 → 놓아 주기
            int count = n.load.Count;
            foreach (var c in n.load)
            {
                c.zone = 1;
                c.Release();
                c.pos = ClampToZone(c, c.pos, 0.85f);
                safeFish.Add(c);
            }
            n.load.Clear();
            rescued[i] += count;
            fx.Ripple(n.pos, NETR * 1.6f, new Color(0.55f, 1f, 0.75f));
            NetPop(i, "+" + count + " 구출!", new Color(0.6f, 1f, 0.75f));
            sfx.Play("deposit");
        }

        int TotalRescued { get { return rescued[0] + rescued[1] + rescued[2] + rescued[3]; } }

        protected override void OnStartGame()
        {
            ClearCreatures();
            safeFish.Clear();
            spawnQueue.Clear();
            for (int i = 0; i < 4; i++) rescued[i] = 0;
            eaten = 0;
            for (int i = 0; i < settings.creatureCount; i++) SpawnFish();
            foreach (var s in sharks) s.pos = RandomSharkPoint();
        }

        protected override void OnEnterReady()
        {
            for (int i = 0; i < 4; i++) rescued[i] = 0;
            eaten = 0;
            if (creatures.Count == 0) for (int i = 0; i < settings.creatureCount; i++) SpawnFish();
            EnsureSharks();
        }

        protected override void OnEndGame() { }

        protected override void ResultTitle(out string title, out string sub, out Color color)
        {
            int total = TotalRescued;
            color = total >= eaten ? new Color(0.6f, 1f, 0.75f) : new Color(1f, 0.7f, 0.6f);
            title = total > 0 ? "물고기 " + total + "마리 구출!" : "한 마리도 못 구했어요";
            int best = -1, bestN = 0;
            for (int i = 0; i < 4; i++) if (rescued[i] > bestN) { bestN = rescued[i]; best = i; }
            sub = "잡아먹힌 물고기 " + eaten + "마리" + (best >= 0 ? " · 구조왕 " + PlayerNames[best] : "") + " · 스페이스바: 다시 하기";
        }

        protected override void RenderGame()
        {
            bool play = state == GameState.Playing || state == GameState.Countdown || state == GameState.Paused;
            bool results = state == GameState.Results;
            int best = -1, bestN = 0;
            for (int i = 0; i < 4; i++) if (rescued[i] > bestN) { bestN = rescued[i]; best = i; }
            for (int i = 0; i < 4; i++)
            {
                badges[i].Text = PlayerNames[i] + (results && i == best ? " 구조왕!" : "") + "\n구출 " + rescued[i];
            }
            foreach (var t in statusTexts)
            {
                t.Visible = play || results;
                t.Text = "구출 " + TotalRescued + "  ·  먹힘 " + eaten;
            }
            foreach (var z in zoneLabels) z.Visible = play;
        }

        protected override void DrawGameSettings()
        {
            GUILayout.Label("상어: " + settings.sharkCount + "마리");
            settings.sharkCount = Mathf.RoundToInt(GUILayout.HorizontalSlider(settings.sharkCount, 1, 4));
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            foreach (var s in sharks) s.Destroy();
        }
    }
}
