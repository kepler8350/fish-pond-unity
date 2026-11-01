using System.Collections.Generic;
using UnityEngine;

namespace FishPond
{
    /// <summary>
    /// 게임 1 · 뜰채 바다 — 바다 생물을 떠서 자기 낚시통에 넣는 4인 경쟁.
    /// 원 안 배치: 가운데(0.66C) 바다, 바깥 고리 산호초, 대각선 4곳 낚시통, 좌우 타이머.
    /// </summary>
    [DisallowMultipleComponent]
    public class FishPondGame : PondGame
    {
        public readonly Bucket[] buckets = new Bucket[4];
        float bucketR;

        readonly List<Creature>[] inBucket = { new List<Creature>(), new List<Creature>(), new List<Creature>(), new List<Creature>() };
        readonly List<float> spawnQueue = new List<float>();
        readonly int[] scores = new int[4], counts = new int[4];
        SpeciesDef jellyDef;

        protected override string SettingsKey { get { return "FishPond.Settings.v2"; } }
        public override string GameTitle { get { return "뜰채 바다"; } }

        protected override void CreateGameObjects()
        {
            jellyDef = species.Find(s => s.id == SpeciesId.Jellyfish);
            for (int i = 0; i < 4; i++) buckets[i] = new Bucket(i, PlayerColors[i], stage);
        }

        protected override void LayoutGame()
        {
            bucketR = circleR * 0.14f;
            for (int i = 0; i < 4; i++) buckets[i].Layout(Dir(PlayerDeg[i]) * circleR * 0.8f, bucketR, PlayerTextRot(i));
            R = circleR * 0.66f;
            NETR = Mathf.Max(circleR * 0.07f, R * 0.16f);
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

        /// <summary>낚시통·타이머 자리를 비워 두고 산호 놓을 자리를 고릅니다.</summary>
        bool ReefAngleFree(float deg)
        {
            float bucketGap = Mathf.Asin(Mathf.Clamp01(bucketR / (circleR * 0.8f))) * Mathf.Rad2Deg + 2f;
            foreach (var b in PlayerDeg)
                if (Mathf.Abs(Mathf.DeltaAngle(deg, b)) < bucketGap) return false;
            foreach (var t in new[] { 0f, 180f })
                if (Mathf.Abs(Mathf.DeltaAngle(deg, t)) < 11f) return false;
            return true;
        }

        protected override void BuildEnvironment()
        {
            var rnd = new System.Random(7);
            System.Func<float> rf = () => (float)rnd.NextDouble();
            AddWater(Art.Water, circleR, 1);

            // 헤엄치는 바다 바닥의 잔잔한 장식 (생물 아래, 흐리게)
            for (int i = 0; i < 10; i++)
            {
                var p = Dir(rf() * 360f) * R * (0.25f + rf() * 0.65f);
                var kind = i % 3 == 0 ? DecorKind.Starfish : i % 3 == 1 ? DecorKind.Shell : DecorKind.Rock;
                PlaceDecor(rnd, kind, p, R * (kind == DecorKind.Rock ? 0.045f : 0.05f), 0.6f, 0.15f, false);
            }

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

            // 물 가운데에도 바위·산호·수초 무더기
            BuildFloorClusters(rnd, () =>
            {
                float a = rf() * Mathf.PI * 2f;
                return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R * (0.2f + rf() * 0.65f);
            }, 8, R * 0.2f, true);
            AddEdgeShade();
        }

        // ---------------------------------------------------------------- 생물 보충 · 통에 넣기
        void Spawn(SpeciesDef def)
        {
            var p = Dir(Random.value * 360f) * Mathf.Sqrt(Random.value) * R * 0.75f;
            SpawnAt(def, p, 0);
        }

        SpeciesDef PickAny() { return PickSpecies(s => true); }

        protected override void UpdateGame(float dt)
        {
            for (int i = spawnQueue.Count - 1; i >= 0; i--)
            {
                spawnQueue[i] -= dt;
                if (spawnQueue[i] <= 0f) { spawnQueue.RemoveAt(i); Spawn(PickAny()); }
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

        protected override void OnNetMoved(int i, float dt)
        {
            var n = nets[i];
            if (Vector2.Distance(n.pos, buckets[i].pos) < buckets[i].radius * 0.8f) Deposit(i);
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

        void ClearBuckets()
        {
            for (int i = 0; i < 4; i++)
            {
                foreach (var c in inBucket[i]) { creatures.Remove(c); c.Destroy(); }
                inBucket[i].Clear();
            }
        }

        // ---------------------------------------------------------------- 흐름
        protected override void OnStartGame()
        {
            ClearBuckets();
            ClearCreatures();
            spawnQueue.Clear();
            for (int i = 0; i < 4; i++) { scores[i] = 0; counts[i] = 0; buckets[i].SetRank(null, Color.white); }
            for (int i = 0; i < settings.creatureCount; i++) Spawn(PickAny());
        }

        protected override void OnEnterReady()
        {
            ClearBuckets();
            for (int i = 0; i < 4; i++) { scores[i] = 0; counts[i] = 0; buckets[i].SetRank(null, Color.white); }
            if (creatures.Count == 0) for (int i = 0; i < settings.creatureCount; i++) Spawn(PickAny());
        }

        protected override void OnEndGame()
        {
            // 점수가 같으면 마릿수가 많은 쪽이 앞섭니다.
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
            sub = "스페이스바: 다시 하기";
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

        protected override void RenderGame()
        {
            for (int i = 0; i < 4; i++) buckets[i].SetScore(scores[i], counts[i]);
        }

        protected override void DrawGameSettings()
        {
            GUILayout.Label("해파리 (닿으면 뜰채가 1.6초 마비): " + settings.jellyCount + "마리");
            settings.jellyCount = Mathf.RoundToInt(GUILayout.HorizontalSlider(settings.jellyCount, 0, 3));
        }
    }
}
