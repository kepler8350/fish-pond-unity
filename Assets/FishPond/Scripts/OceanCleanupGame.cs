using System.Collections.Generic;
using UnityEngine;

namespace FishPond
{
    /// <summary>
    /// 게임 3 · 바다 청소대 — 떠다니는 쓰레기를 뜰채로 건져 자기 통에 넣는 4인 경쟁.
    ///  · 쓰레기: 종류마다 1~5점 (그물·타이어가 무겁고 점수 큼)
    ///  · 물고기를 건져 통에 넣으면 -2점, 물고기는 다시 바다로 돌아감
    ///  · 쓰레기가 많을수록 바다 건강이 떨어지고 산호가 칙칙하게 시듦 → 치우면 다시 살아남
    ///  · 마지막 15초는 새 쓰레기가 들어오지 않음. 다 치우면 "바다가 깨끗해졌어요!"
    /// 배치는 뜰채 바다와 같음: 가운데 바다, 바깥 산호초 고리, 대각선 4곳 수거통.
    /// </summary>
    [DisallowMultipleComponent]
    public class OceanCleanupGame : PondGame
    {
        public readonly Bucket[] buckets = new Bucket[4];
        float bucketR;

        readonly List<Creature>[] inBucket = { new List<Creature>(), new List<Creature>(), new List<Creature>(), new List<Creature>() };
        readonly int[] scores = new int[4], counts = new int[4];

        const int FishPenalty = 2;
        const float CalmTail = 15f;        // 마지막 몇 초는 새 쓰레기 없음
        const int MaxTrash = 22;

        float health = 1f;
        SpriteRenderer murk;                  // 바다 건강 0~1
        float trashT, fishT, bleachShown = -1f;
        int trashCleared, trashArrived;
        bool cleanCelebrated;
        readonly FloorText[] healthTexts = new FloorText[2];

        protected override string SettingsKey { get { return "OceanCleanup.Settings.v1"; } }
        public override string GameTitle { get { return "바다 청소대"; } }

        protected override void CreateGameObjects()
        {
            for (int i = 0; i < 4; i++) buckets[i] = new Bucket(i, PlayerColors[i], stage);
            for (int i = 0; i < 2; i++) healthTexts[i] = new FloorText(stage, "health" + i, 46, Color.white, 1f);
        }

        protected override void LayoutGame()
        {
            bucketR = circleR * 0.14f;
            for (int i = 0; i < 4; i++) buckets[i].Layout(Dir(PlayerDeg[i]) * circleR * 0.8f, bucketR, PlayerTextRot(i));
            R = circleR * 0.66f;
            NETR = Mathf.Max(circleR * 0.07f, R * 0.16f);
        }

        protected override void LayoutTexts()
        {
            base.LayoutTexts();
            float C = circleR, fs = C * 0.055f;
            // 바다 건강은 위·아래 (아래쪽 사람은 그대로, 위쪽 사람은 뒤집어 읽음)
            healthTexts[0].SetHeight(fs); healthTexts[0].Place(new Vector2(0f, -C * 0.86f), 0f, -3f);
            healthTexts[1].SetHeight(fs); healthTexts[1].Place(new Vector2(0f, C * 0.86f), 180f, -3f);
            foreach (var t in healthTexts) t.Backdrop(new Vector2(fs * 7.5f, fs * 1.5f), new Color(0.01f, 0.06f, 0.1f, 0.6f));
        }

        public override bool BucketSlot(int b, out Vector2 pos, out float radius)
        {
            pos = buckets[b].pos; radius = buckets[b].radius;
            return true;
        }

        public override Vector2 HandleAnchor(int i) { return buckets[i].pos; }

        protected override Vector2 NetHome(int i)
        {
            var b = buckets[i];
            return b.pos - b.pos.normalized * b.radius * 1.9f;
        }

        bool ReefAngleFree(float deg)
        {
            float bucketGap = Mathf.Asin(Mathf.Clamp01(bucketR / (circleR * 0.8f))) * Mathf.Rad2Deg + 2f;
            foreach (var b in PlayerDeg)
                if (Mathf.Abs(Mathf.DeltaAngle(deg, b)) < bucketGap) return false;
            foreach (var t in new[] { 0f, 90f, 180f, 270f })
                if (Mathf.Abs(Mathf.DeltaAngle(deg, t)) < 11f) return false;   // 타이머·바다 건강 글자 자리
            return true;
        }

        protected override void BuildEnvironment()
        {
            var rnd = new System.Random(11);
            System.Func<float> rf = () => (float)rnd.NextDouble();
            AddWater(Art.Water, circleR, 1);
            BuildReef(rnd, (rMin, rMax) =>
            {
                for (int tries = 0; tries < 40; tries++)
                {
                    float deg = rf() * 360f;
                    if (!ReefAngleFree(deg)) continue;
                    return Dir(deg) * Mathf.Lerp(rMin, rMax, Mathf.Sqrt(rf()));
                }
                return new Vector2(rMax, 0f);
            }, R * 1.03f, circleR * 0.96f, 1f);
            BuildFloorClusters(rnd, () =>
            {
                float a = rf() * Mathf.PI * 2f;
                return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R * (0.2f + rf() * 0.65f);
            }, 8, R * 0.2f, true);
            AddEdgeShade();
            // 오염된 바다: 쓰레기가 많을수록 물 전체가 탁한 녹갈색으로 흐려짐 (생물 아래, 산호 위)
            murk = Spr(env, "murk", Art.Circle, new Color(0.25f, 0.22f, 0.12f, 0f), 36, Vector2.zero, Vector2.one * circleR * 1.96f, 0f, 0.12f);
            bleachShown = -1f;
        }

        // ---------------------------------------------------------------- 생물 · 쓰레기
        int TrashCount()
        {
            int n = 0;
            foreach (var c in creatures) if (c.IsTrash && c.InPond && !c.fadingOut) n++;
            return n;
        }

        int FishCount()
        {
            int n = 0;
            foreach (var c in creatures) if (!c.IsTrash && !c.IsJelly && c.InPond && !c.fadingOut) n++;
            return n;
        }

        void SpawnFish()
        {
            var p = Dir(Random.value * 360f) * Mathf.Sqrt(Random.value) * R * 0.75f;
            SpawnAt(PickSpecies(s => SpeciesDefaults.IsSeaLife(s.id)), p, 0);
        }

        /// <summary>쓰레기는 바다 가장자리에서 흘러 들어옴</summary>
        void SpawnTrash(bool anywhere)
        {
            var def = PickSpecies(s => SpeciesDefaults.IsTrash(s.id));
            if (def == null) return;
            var p = anywhere ? Dir(Random.value * 360f) * Mathf.Sqrt(Random.value) * R * 0.85f
                             : Dir(Random.value * 360f) * R * 0.9f;
            if (SpawnAt(def, p, 0) != null) trashArrived++;
        }

        float GameElapsedFrac { get { return settings.durationSec > 0 ? 1f - remaining / settings.durationSec : 0f; } }

        protected override void UpdateGame(float dt)
        {
            int trash = TrashCount();
            if (IsPlaying)
            {
                // 쓰레기는 점점 더 자주 들어오다가 마지막 15초는 멈춤
                bool calm = remaining <= CalmTail;
                trashT -= dt;
                if (!calm && trashT <= 0f && trash < MaxTrash)
                {
                    SpawnTrash(false);
                    trashT = Mathf.Lerp(4.5f, 2.2f, GameElapsedFrac) * Random.Range(0.8f, 1.2f);
                }
                if (calm && trash == 0 && !cleanCelebrated) Celebrate();
            }
            // 물고기 수 유지 (건강할수록 많이 모여듦)
            int wantFish = Mathf.RoundToInt(settings.creatureCount * Mathf.Lerp(0.5f, 1f, health));
            fishT -= dt;
            if (fishT <= 0f && FishCount() < wantFish) { SpawnFish(); fishT = 0.8f; }

            // 바다 건강: 쓰레기 수에 따라 천천히 따라감
            float target = 1f - Mathf.Clamp01(trash / (float)MaxTrash);
            float rate = target > health ? 0.12f : 0.02f;     // 회복은 빠르게, 나빠지는 건 천천히
            health = Mathf.MoveTowards(health, target, rate * dt);
            ApplyReefHealth();
        }

        void Celebrate()
        {
            cleanCelebrated = true;
            sfx.Play("start");
            for (int i = 0; i < 6; i++) SpawnFish();
            for (int i = 0; i < 8; i++)
                fx.Ripple(Dir(i * 45f) * R * 0.5f, R * 0.25f, new Color(0.7f, 1f, 0.9f));
        }

        // ---------------------------------------------------------------- 산호 건강 표시 (시들면 칙칙한 회갈색)
        MaterialPropertyBlock mpb;
        static readonly string[] ColorProps = { "_Color", "_BaseColor", "baseColorFactor" };
        readonly Dictionary<Renderer, Color[]> baseColors = new Dictionary<Renderer, Color[]>();

        void ApplyReefHealth()
        {
            float bleach = 1f - health;
            if (Mathf.Abs(bleach - bleachShown) < 0.02f) return;
            bleachShown = bleach;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            if (murk != null) murk.color = new Color(0.25f, 0.22f, 0.12f, 0.42f * bleach);
            // 색을 곱하는 방식이라 질감 색은 남으므로, 어둡고 누런 회갈색으로 눌러서 시든 느낌을 냄
            var dead = new Color(0.4f, 0.36f, 0.3f);
            foreach (var t in livingDecor)
            {
                if (t == null) continue;
                foreach (var r in t.GetComponentsInChildren<Renderer>())
                {
                    Color[] baseCols;
                    var mats = r.sharedMaterials;
                    if (!baseColors.TryGetValue(r, out baseCols))
                    {
                        baseCols = new Color[mats.Length];
                        for (int m = 0; m < mats.Length; m++)
                        {
                            baseCols[m] = Color.white;
                            if (mats[m] == null) continue;
                            foreach (var p in ColorProps)
                                if (mats[m].HasProperty(p)) { baseCols[m] = mats[m].GetColor(p); break; }
                        }
                        baseColors[r] = baseCols;
                    }
                    for (int m = 0; m < mats.Length; m++)
                    {
                        if (mats[m] == null) continue;
                        var bc = baseCols[m];
                        float lum = bc.r * 0.3f + bc.g * 0.59f + bc.b * 0.11f;
                        var target = new Color(dead.r * lum, dead.g * lum, dead.b * lum, bc.a);
                        var c = Color.Lerp(bc, target, bleach);
                        r.GetPropertyBlock(mpb, m);
                        foreach (var p in ColorProps) if (mats[m].HasProperty(p)) mpb.SetColor(p, c);
                        r.SetPropertyBlock(mpb, m);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- 수거통에 넣기
        protected override void OnNetMoved(int i, float dt)
        {
            var n = nets[i];
            if (Vector2.Distance(n.pos, buckets[i].pos) < buckets[i].radius * 0.8f) Deposit(i);
        }

        void Deposit(int i)
        {
            var n = nets[i];
            if (n.load.Count == 0) return;
            int pts = 0, trashIn = 0, fishBack = 0;
            var b = buckets[i];
            foreach (var c in n.load)
            {
                if (c.IsTrash)
                {
                    pts += c.def.points;
                    trashIn++;
                    trashCleared++;
                    c.ToBucket(i);
                    inBucket[i].Add(c);
                }
                else
                {
                    // 물고기는 통에 넣지 않고 바다로 돌려보냄 + 감점
                    pts -= FishPenalty;
                    fishBack++;
                    c.StartFly(-b.pos.normalized * R * 3.5f, this);
                }
            }
            counts[i] += trashIn;
            scores[i] = Mathf.Max(0, scores[i] + pts);
            n.load.Clear();
            while (inBucket[i].Count > 8)
            {
                var old = inBucket[i][0];
                inBucket[i].RemoveAt(0);
                creatures.Remove(old);
                old.Destroy();
            }
            var at = b.pos + Up(b.textRot) * b.radius * 1.35f;
            if (fishBack > 0)
            {
                fx.Pop(at, pts >= 0 ? "+" + pts : pts.ToString(), new Color(1f, 0.45f, 0.4f), b.textRot, b.radius * 0.5f);
                fx.Pop(b.pos - Up(b.textRot) * b.radius * 1.6f, "물고기는 놓아줘요!", new Color(1f, 0.6f, 0.5f), b.textRot, b.radius * 0.32f);
                sfx.Play("drop");
            }
            else
            {
                fx.Pop(at, "+" + pts, PlayerColors[i], b.textRot, b.radius * 0.5f);
                sfx.Play("deposit");
            }
            fx.Ripple(b.pos, b.radius * 1.1f, Color.white);
        }

        void ClearBuckets()
        {
            for (int i = 0; i < 4; i++)
            {
                foreach (var c in inBucket[i]) { creatures.Remove(c); c.Destroy(); }
                inBucket[i].Clear();
            }
        }

        void ResetRound()
        {
            ClearBuckets();
            for (int i = 0; i < 4; i++) { scores[i] = 0; counts[i] = 0; buckets[i].SetRank(null, Color.white); }
            trashCleared = 0; trashArrived = 0; cleanCelebrated = false;
            trashT = 2f; fishT = 0f;
        }

        // ---------------------------------------------------------------- 흐름
        protected override void OnStartGame()
        {
            ResetRound();
            ClearCreatures();
            health = 0.75f;
            for (int i = 0; i < settings.creatureCount / 2; i++) SpawnFish();
            for (int i = 0; i < 8; i++) SpawnTrash(true);
        }

        protected override void OnEnterReady()
        {
            ResetRound();
            if (creatures.Count == 0)
            {
                health = 0.75f;
                for (int i = 0; i < settings.creatureCount / 2; i++) SpawnFish();
                for (int i = 0; i < 8; i++) SpawnTrash(true);
            }
        }

        protected override void OnEndGame()
        {
            for (int i = 0; i < 4; i++)
            {
                int better = 0;
                for (int j = 0; j < 4; j++)
                    if (scores[j] > scores[i] || (scores[j] == scores[i] && counts[j] > counts[i])) better++;
                buckets[i].SetRank((better + 1) + "등", better == 0 && scores[i] > 0 ? PlayerColors[i] : Color.white);
            }
        }

        protected override void ResultTitle(out string text, out string sub, out Color color)
        {
            int left = TrashCount();
            int pct = trashArrived > 0 ? Mathf.RoundToInt(100f * trashCleared / Mathf.Max(1, trashCleared + left)) : 0;
            sub = (left == 0 ? "바다가 깨끗해졌어요! " : "남은 쓰레기 " + left + "개 · ") + "함께 치운 쓰레기 " + trashCleared + "개 (" + pct + "%)   스페이스바: 다시 하기";
            int top = Mathf.Max(Mathf.Max(scores[0], scores[1]), Mathf.Max(scores[2], scores[3]));
            color = Color.white;
            if (top == 0) { text = "쓰레기를 못 치웠어요"; return; }
            int bestCount = -1;
            for (int i = 0; i < 4; i++) if (scores[i] == top) bestCount = Mathf.Max(bestCount, counts[i]);
            var winners = new List<string>();
            int last = 0;
            for (int i = 0; i < 4; i++)
                if (scores[i] == top && counts[i] == bestCount) { winners.Add(PlayerNames[i]); last = i; }
            if (winners.Count > 1) text = string.Join(" · ", winners.ToArray()) + " 공동 우승";
            else { text = PlayerNames[last] + " 우승!"; color = PlayerColors[last]; }
        }

        protected override void RenderGame()
        {
            for (int i = 0; i < 4; i++) buckets[i].SetScore(scores[i], counts[i], "개");
            int pct = Mathf.RoundToInt(health * 100f);
            string label = "바다 건강 " + pct + "%";
            var col = Color.Lerp(new Color(1f, 0.45f, 0.35f), new Color(0.5f, 1f, 0.75f), health);
            foreach (var t in healthTexts) { t.Text = label; t.Color = col; }
        }
    }
}
