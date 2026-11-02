using System.Collections.Generic;
using UnityEngine;

namespace FishPond
{
    /// <summary>
    /// 게임 4 · 거북이 알 지키기 — 4명이 함께 하는 협동 방어 게임.
    /// 원 가운데 모래섬, 섬 한가운데 둥지에 거북이 알. 둘레는 바다.
    ///  1단계 (알 지키기): 게가 바다에서 기어 올라오고 갈매기가 날아와 알을 노림.
    ///     · 게 = 뜰채로 떠서 바다(섬 밖)로 옮기면 쫓아냄
    ///     · 갈매기 = 뜰채를 휘둘러 닿게 하면 놀라서 날아감 (잡을 수는 없음)
    ///  2단계 (부화): 남은 시간이 얼마 안 남으면 알이 깨고 아기 거북이 바다로 기어감.
    ///     · 천적은 이제 아기 거북을 노림. 아기 거북을 떠서 바다에 넣어 줘도 됨
    ///  결과: 바다에 무사히 들어간 아기 거북 수 (별 3개까지)
    /// </summary>
    [DisallowMultipleComponent]
    public class TurtleNestGame : PondGame
    {
        enum Kind { Crab, Gull, Baby, Egg }

        class Agent
        {
            public Kind kind;
            public Creature target;
            public bool leaving;
            public float t, phase, dir;
            public SpriteRenderer shadow;
        }

        const int MaxCrabs = 6;

        float islandR, nestR;
        SpeciesDef crabDef, gullDef, babyDef, eggDef;
        readonly Dictionary<Creature, Agent> agents = new Dictionary<Creature, Agent>();
        readonly List<Creature> eggs = new List<Creature>();
        readonly List<Creature> babies = new List<Creature>();
        readonly List<KeyValuePair<Creature, float>> hatchQueue = new List<KeyValuePair<Creature, float>>();
        readonly int[] shooed = new int[4], carried = new int[4];
        int saved, eatenEggs, eatenBabies, startEggs;
        bool hatchPhase;
        float crabT, gullT, fishT;
        Material eggMat;

        FloorText[] badges, statusTexts;

        protected override string SettingsKey { get { return "TurtleNest.Settings.v1"; } }
        public override string GameTitle { get { return "거북이 알 지키기"; } }

        float HatchAt { get { return Mathf.Min(35f, settings.durationSec * 0.4f); } }

        // ================================================================ 준비
        protected override void CreateGameObjects()
        {
            crabDef = species.Find(s => s.id == SpeciesId.Crab);
            gullDef = species.Find(s => s.id == SpeciesId.Seagull);
            eggDef = species.Find(s => s.id == SpeciesId.TurtleEgg);
            babyDef = species.Find(s => s.id == SpeciesId.Hatchling);
            // 아기 거북 모델이 따로 없으면 바다거북 모델을 작게 씀
            var turtle = species.Find(s => s.id == SpeciesId.SeaTurtle);
            if (babyDef != null && babyDef.prefabOverride == null && turtle != null)
            {
                babyDef.prefabOverride = turtle.prefabOverride;
                babyDef.prefabSideView = turtle.prefabSideView;
                babyDef.prefabRotation = turtle.prefabRotation;
                babyDef.prefabTilt = turtle.prefabTilt;
                babyDef.prefabScale = turtle.prefabScale;
                babyDef.animatorController = turtle.animatorController;
            }
            var sh = Shader.Find("Standard");
            if (sh != null)
            {
                eggMat = new Material(sh) { name = "TurtleEgg" };
                eggMat.color = new Color(0.97f, 0.95f, 0.88f);
                if (eggMat.HasProperty("_Glossiness")) eggMat.SetFloat("_Glossiness", 0.35f);
            }

            var light = new Color(0.9f, 0.97f, 0.95f);
            badges = new FloorText[4];
            for (int i = 0; i < 4; i++) badges[i] = new FloorText(stage, "badge" + i, 80, PlayerColors[i], 1f);
            statusTexts = new[] { new FloorText(stage, "statusA", 80, light, 1f), new FloorText(stage, "statusB", 80, light, 1f) };
        }

        protected override void LayoutGame()
        {
            R = circleR * 0.66f;
            islandR = circleR * 0.56f;
            nestR = islandR * 0.2f;
            NETR = Mathf.Max(circleR * 0.07f, R * 0.16f);
        }

        protected override void OnRescale(float k)
        {
            foreach (var kv in agents) if (kv.Value.shadow != null) kv.Value.shadow.transform.localScale *= k;
        }

        protected override Vector2 NetHome(int i) { return Dir(PlayerDeg[i]) * circleR * 0.72f; }

        // ================================================================ 구역: 0 = 바다 물고기(섬 둘레 바다), 1 = 섬 위의 생물
        float SeaIn { get { return islandR * 1.1f; } }
        float SeaOut { get { return circleR * 0.9f; } }

        public override Vector2 RandomTarget(Creature c)
        {
            if (c.zone != 0) return base.RandomTarget(c);
            float a = Random.value * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(SeaIn * 1.05f, SeaOut * 0.95f);
        }

        public override bool NearEdge(Creature c, Vector2 p, float f)
        {
            if (c.zone != 0) return p.magnitude > circleR * 0.88f;
            float r = p.magnitude;
            return r < SeaIn * 1.05f || r > SeaOut * 0.95f;
        }

        public override Vector2 Inward(Creature c, Vector2 p)
        {
            if (c.zone != 0) return -p;
            return p.normalized * (SeaIn + SeaOut) * 0.5f - p;
        }

        public override Vector2 ClampToZone(Creature c, Vector2 p, float f)
        {
            float r = p.magnitude;
            if (r < 1e-5f) return p;
            if (c.zone == 0) return p / r * Mathf.Clamp(r, SeaIn, SeaOut);
            float lim = circleR * 0.92f;
            return r > lim ? p / r * lim : p;
        }

        protected override bool CanBeCaught(Creature c)
        {
            Agent a;
            if (!agents.TryGetValue(c, out a)) return false;   // 바다 물고기는 잡지 않음
            // 게는 섬(모래) 위에 올라온 뒤에만 잡힘 (바다에 가만히 둔 뜰채에 저절로 걸리지 않게)
            if (a.kind == Kind.Crab) return !a.leaving && c.pos.magnitude < islandR * 1.02f;
            return a.kind == Kind.Baby;
        }

        // ================================================================ 배경: 바다 + 가운데 모래섬
        bool AngleFree(float deg)
        {
            foreach (var b in PlayerDeg) if (Mathf.Abs(Mathf.DeltaAngle(deg, b)) < 13f) return false;
            foreach (var t in new[] { 0f, 90f, 180f, 270f })
                if (Mathf.Abs(Mathf.DeltaAngle(deg, t)) < 11f) return false;   // 타이머·현황 글자 자리
            return true;
        }

        protected override void BuildEnvironment()
        {
            var rnd = new System.Random(23);
            System.Func<float> rf = () => (float)rnd.NextDouble();
            float C = circleR;
            AddWater(Art.Water, C, 1);
            AddIsland();

            // 바깥 고리: 산호초 (플레이어 자리·글자 자리는 비움)
            BuildReef(rnd, (rMin, rMax) =>
            {
                for (int k = 0; k < 40; k++)
                {
                    float deg = rf() * 360f;
                    if (!AngleFree(deg)) continue;
                    return Dir(deg) * Mathf.Lerp(rMin, rMax, Mathf.Sqrt(rf()));
                }
                return new Vector2(rMax, 0f);
            }, C * 0.8f, C * 0.96f, 0.75f);

            // 얕은 바다: 해안선 따라 바위 몇 개
            for (int i = 0; i < 9; i++)
            {
                float deg = rf() * 360f;
                if (!AngleFree(deg)) continue;
                PlaceDecor(rnd, DecorKind.Rock, Dir(deg) * islandR * (1.0f + rf() * 0.08f), C * (0.04f + rf() * 0.04f), 0.35f, 0.3f, false);
            }
            // 섬 위: 조개껍데기·불가사리 (둥지 근처는 비움)
            for (int i = 0; i < 12; i++)
            {
                var p = Dir(rf() * 360f) * islandR * (0.35f + rf() * 0.55f);
                var kind = i % 2 == 0 ? DecorKind.Shell : DecorKind.Starfish;
                PlaceDecor(rnd, kind, p, C * (0.025f + rf() * 0.015f), 0.6f, 0.08f, false);
            }
            // 바다 바닥 무더기 (섬과 산호초 사이)
            BuildFloorClusters(rnd, () =>
            {
                float a = rf() * 360f;
                return Dir(a) * C * (0.66f + rf() * 0.1f);
            }, 5, C * 0.07f, true);
            AddEdgeShade();
        }

        /// <summary>
        /// 모래섬: 물 바닥과 같은 모래 모델을 물 층(z 0.95)보다 앞에 한 번 더 깔고,
        /// 재질을 SandIsland 셰이더로 바꿔 둥글게 오려 냄 (물빛이 덮이지 않은 마른 모래).
        /// </summary>
        void AddIsland()
        {
            var sh = Shader.Find("FishPond/SandIsland");
            var cat = SpeciesCatalog.Load();
            if (sh == null || cat == null || cat.seabedPrefab == null) return;
            var bed = new DecorDef { kind = DecorKind.Rock, prefab = cat.seabedPrefab, rotation = cat.seabedRotation, scale = 1f };
            var t = Decor.Place(bed, env, Vector2.zero, islandR * 2.4f, 0f, 0.9f, 0.02f, true);
            if (t == null) return;
            t.name = "SandIsland";
            if (sandMats == null) sandMats = new Dictionary<Material, Material>();
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled) continue;
                var mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    var src = mats[m];
                    if (src == null) continue;
                    Material dst;
                    if (!sandMats.TryGetValue(src, out dst))
                    {
                        dst = new Material(sh) { name = "SandIsland" };
                        var tex = MainTexture(src);
                        if (tex != null) dst.SetTexture("_MainTex", tex);
                        sandMats[src] = dst;
                    }
                    mats[m] = dst;
                }
                r.sharedMaterials = mats;
                r.sortingOrder = 2;
            }
            UpdateIslandShape();
        }

        Dictionary<Material, Material> sandMats;

        static Texture MainTexture(Material m)
        {
            foreach (var name in m.GetTexturePropertyNames())
            {
                string n = name.ToLowerInvariant();
                if (!(n.Contains("basecolor") || n.Contains("maintex") || n.Contains("albedo") || n.Contains("diffuse"))) continue;
                var t = m.GetTexture(name);
                if (t != null) return t;
            }
            return m.mainTexture;
        }

        /// <summary>섬 모양(중심·반지름)을 화면 좌표로 셰이더에 알려 줌 (투사 보정으로 무대가 움직여도 맞게)</summary>
        void UpdateIslandShape()
        {
            if (sandMats == null) return;
            var c = stage.TransformPoint(Vector3.zero);
            var sc = stage.lossyScale;
            var size = new Vector4(islandR * Mathf.Abs(sc.x), islandR * Mathf.Abs(sc.y), 0f, 0f);
            foreach (var m in sandMats.Values)
            {
                m.SetVector("_Center", new Vector4(c.x, c.y, 0f, 0f));
                m.SetVector("_Size", size);
            }
        }

        protected override void LayoutTexts()
        {
            base.LayoutTexts();
            float C = circleR, fs = C * 0.075f;
            statusTexts[0].SetHeight(fs * 0.55f); statusTexts[0].Place(new Vector2(0f, -C * 0.86f), 0f, -3f);
            statusTexts[1].SetHeight(fs * 0.55f); statusTexts[1].Place(new Vector2(0f, C * 0.86f), 180f, -3f);
            foreach (var t in statusTexts) t.Backdrop(new Vector2(fs * 6.5f, fs * 1.0f), new Color(0.01f, 0.06f, 0.1f, 0.6f));
            for (int i = 0; i < 4; i++)
            {
                badges[i].SetHeight(C * 0.038f);
                badges[i].Place(Dir(PlayerDeg[i]) * C * 0.885f, PlayerTextRot(i), -3f);
                badges[i].Backdrop(new Vector2(C * 0.25f, C * 0.13f), new Color(0.01f, 0.06f, 0.1f, 0.6f));
            }
        }

        // ================================================================ 생물 만들기
        Agent AddAgent(Creature c, Kind kind)
        {
            var a = new Agent { kind = kind, phase = Random.value * 10f, t = Random.Range(0.6f, 1.4f) };
            agents[c] = a;
            return a;
        }

        void SpawnEggs()
        {
            int n = Mathf.Clamp(settings.eggCount, 4, 20);
            startEggs = n;
            if (eggDef == null || eggDef.prefabOverride == null) return;
            for (int i = 0; i < n; i++)
            {
                // 해바라기 씨 배열로 둥지에 촘촘히
                float r = nestR * Mathf.Sqrt((i + 0.5f) / n);
                float a = i * 2.39996f;
                var c = SpawnAt(eggDef, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, 1);
                if (c == null) continue;
                c.heading = Random.value * Mathf.PI * 2f;
                if (eggMat != null)
                    foreach (var rr in c.go.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = rr.sharedMaterials;
                        for (int m = 0; m < mats.Length; m++) mats[m] = eggMat;
                        rr.sharedMaterials = mats;
                    }
                eggs.Add(c);
                AddAgent(c, Kind.Egg).dir = c.heading;
            }
        }

        void SpawnSeaFish()
        {
            var def = PickSpecies(s => SpeciesDefaults.IsSeaLife(s.id) && s.id != SpeciesId.SeaTurtle);
            float a = Random.value * Mathf.PI * 2f;
            SpawnAt(def, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(SeaIn * 1.05f, SeaOut * 0.95f), 0);
        }

        int CountKind(Kind k)
        {
            int n = 0;
            foreach (var kv in agents) if (kv.Value.kind == k && !kv.Key.fadingOut) n++;
            return n;
        }

        void SpawnCrab()
        {
            if (crabDef == null || crabDef.prefabOverride == null) return;
            float deg = Random.value * 360f;
            var c = SpawnAt(crabDef, Dir(deg) * circleR * 0.84f, 1);
            if (c == null) return;
            c.heading = (deg + 180f) * Mathf.Deg2Rad;
            AddAgent(c, Kind.Crab);
        }

        void SpawnGull()
        {
            if (gullDef == null || gullDef.prefabOverride == null) return;
            float deg = Random.value * 360f;
            var c = SpawnAt(gullDef, Dir(deg) * circleR * 0.95f, 1);
            if (c == null) return;
            c.heading = (deg + 180f + Random.Range(-40f, 40f)) * Mathf.Deg2Rad;
            c.lift = 2.2f;
            c.scaleMul = 0.7f;   // 날개를 편 폭이 몸길이의 두 배쯤이라 작게
            var a = AddAgent(c, Kind.Gull);
            // 날고 있는 높이를 보여 주는 그림자
            a.shadow = Spr(stage, "gullShadow", Art.SoftCircle, new Color(0f, 0.02f, 0.05f, 0.35f), 9,
                           c.pos, new Vector2(R * gullDef.length * 0.7f, R * gullDef.length * 1.1f), 0f, 0.2f);
        }

        void Hatch(Creature egg)
        {
            if (!eggs.Remove(egg)) return;
            var p = egg.pos;
            RemoveCreature(egg);
            fx.Ripple(p, NETR * 0.8f, new Color(1f, 0.95f, 0.75f));
            if (babyDef == null || babyDef.prefabOverride == null) return;
            var b = SpawnAt(babyDef, p, 1);
            if (b == null) return;
            b.scaleMul = 1.4f;   // 바닥 투사에서 잘 보이게 조금 크게
            var a = AddAgent(b, Kind.Baby);
            // 둥지 바깥쪽으로, 조금씩 다른 방향
            a.dir = Mathf.Atan2(p.y, p.x) + Random.Range(-0.45f, 0.45f);
            if (p.magnitude < 1e-3f) a.dir = Random.value * Mathf.PI * 2f;
            b.heading = a.dir;
            babies.Add(b);
        }

        void RemoveCreature(Creature c)
        {
            Agent a;
            if (agents.TryGetValue(c, out a) && a.shadow != null) Destroy(a.shadow.gameObject);
            agents.Remove(c);
            eggs.Remove(c);
            babies.Remove(c);
            creatures.Remove(c);
            c.Destroy();
        }

        protected override void OnCreatureRemoved(Creature c)
        {
            Agent a;
            if (agents.TryGetValue(c, out a) && a.shadow != null) Destroy(a.shadow.gameObject);
            agents.Remove(c);
            eggs.Remove(c);
            babies.Remove(c);
        }

        void ResetAll()
        {
            foreach (var kv in agents) if (kv.Value.shadow != null) Destroy(kv.Value.shadow.gameObject);
            agents.Clear();
            eggs.Clear();
            babies.Clear();
            hatchQueue.Clear();
            ClearCreatures();
            for (int i = 0; i < 4; i++) { shooed[i] = 0; carried[i] = 0; }
            saved = eatenEggs = eatenBabies = 0;
            hatchPhase = false;
            crabT = 3f; gullT = 8f; fishT = 0f;
            SpawnEggs();
            for (int i = 0; i < settings.creatureCount / 2; i++) SpawnSeaFish();
        }

        // ================================================================ 움직임 (게·갈매기·아기 거북·알)
        Creature NearestPrey(Vector2 from)
        {
            var list = hatchPhase && babies.Count > 0 ? babies : eggs;
            Creature best = null;
            float bd = float.MaxValue;
            foreach (var c in list)
            {
                if (c.state != Creature.State.Swim || c.fadingOut) continue;
                float d = (c.pos - from).sqrMagnitude;
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        bool Alive(Creature c) { return c != null && agents.ContainsKey(c) && !c.fadingOut && c.state == Creature.State.Swim; }

        public override bool MoveCustom(Creature c, float dt)
        {
            Agent a;
            if (!agents.TryGetValue(c, out a)) return false;
            switch (a.kind)
            {
                case Kind.Egg: MoveEgg(c, a, dt); break;
                case Kind.Crab: MoveCrab(c, a, dt); break;
                case Kind.Gull: MoveGull(c, a, dt); break;
                case Kind.Baby: MoveBaby(c, a, dt); break;
            }
            return true;
        }

        void MoveEgg(Creature c, Agent a, float dt)
        {
            c.Anim = 0f;
            // 부화 직전에는 달그락달그락 흔들림
            bool soon = IsPlaying && !hatchPhase && remaining < HatchAt + 6f;
            c.heading = a.dir + (soon ? Mathf.Sin(clock * 22f + a.phase) * 0.18f : 0f);
        }

        void MoveCrab(Creature c, Agent a, float dt)
        {
            float R0 = R;
            Vector2 goal;
            if (a.leaving)
            {
                goal = c.pos.normalized * circleR;
                if (c.pos.magnitude > circleR * 0.86f) c.fadingOut = true;
            }
            else
            {
                if (!Alive(a.target)) a.target = NearestPrey(c.pos);
                goal = a.target != null ? a.target.pos : Vector2.zero;
            }
            // 게걸음: 잠깐 가다 멈췄다를 반복
            a.t -= dt;
            if (a.t <= -0.45f) a.t = Random.Range(0.8f, 1.6f);
            bool moving = a.t > 0f || a.leaving;
            var to = goal - c.pos;
            float dist = to.magnitude;
            if (moving && dist > 1e-4f)
            {
                var dir = to / dist;
                var side = new Vector2(-dir.y, dir.x) * Mathf.Sin(clock * 3f + a.phase) * 0.35f;
                float spd = crabDef.speed * R0 * (a.leaving ? 1.8f : 1f) * (c.hold > 0f ? 0.25f : 1f);
                c.pos += (dir + side).normalized * spd * dt;
                float want = Mathf.Atan2(dir.y, dir.x);
                c.heading = Mathf.LerpAngle(c.heading * Mathf.Rad2Deg, want * Mathf.Rad2Deg, 1f - Mathf.Exp(-dt * 4f)) * Mathf.Deg2Rad;
            }
            c.Anim = moving ? 1f : 0.2f;
            c.pos = ClampToZone(c, c.pos, 1f);

            if (!a.leaving && a.target != null && IsPlaying && dist < R0 * (crabDef.length * 0.45f + 0.03f))
            {
                Eat(c, a, a.target);
            }
        }

        void MoveGull(Creature c, Agent a, float dt)
        {
            float speed = gullDef.speed * R * (a.leaving ? 2.4f : 1f);
            float want;
            if (a.leaving)
            {
                want = Mathf.Atan2(c.pos.y, c.pos.x);
                if (c.pos.magnitude > circleR * 0.9f) c.fadingOut = true;
            }
            else
            {
                if (!Alive(a.target)) a.target = NearestPrey(c.pos);
                var goal = a.target != null ? a.target.pos : Vector2.zero;
                var to = goal - c.pos;
                // 먹이 위를 빙 돌다가 내려꽂음
                float dist = to.magnitude;
                want = Mathf.Atan2(to.y, to.x);
                if (dist > R * 0.25f) want += Mathf.Sin(clock * 0.8f + a.phase) * 0.6f;
                if (a.target != null && IsPlaying && dist < R * 0.06f) Eat(c, a, a.target);

                // 뜰채를 휘두르면 놀라서 날아감
                for (int i = 0; i < 4; i++)
                {
                    var n = nets[i];
                    if (n.stunT > 0f || n.speed < 0.3f * R) continue;
                    if (Vector2.Distance(n.pos, c.pos) < NETR * 1.15f + R * gullDef.length * 0.3f)
                    {
                        a.leaving = true;
                        a.target = null;
                        c.heading = Mathf.Atan2(c.pos.y - n.pos.y, c.pos.x - n.pos.x);
                        if (IsPlaying)
                        {
                            shooed[i]++;
                            NetPop(i, "훠이!", new Color(0.85f, 0.95f, 1f));
                            sfx.Play("shoo");
                        }
                        break;
                    }
                }
            }
            c.heading = Mathf.LerpAngle(c.heading * Mathf.Rad2Deg, want * Mathf.Rad2Deg, 1f - Mathf.Exp(-dt * (a.leaving ? 3f : 1.6f))) * Mathf.Deg2Rad;
            c.pos += new Vector2(Mathf.Cos(c.heading), Mathf.Sin(c.heading)) * speed * dt;
            c.Anim = 1f;
            if (a.shadow != null)
            {
                a.shadow.transform.localPosition = new Vector3(c.pos.x + R * 0.06f, c.pos.y - R * 0.09f, 0.2f);
                a.shadow.transform.localRotation = Quaternion.Euler(0f, 0f, c.heading * Mathf.Rad2Deg);
                a.shadow.color = new Color(0f, 0.02f, 0.05f, 0.35f * c.alpha);
            }
        }

        void MoveBaby(Creature c, Agent a, float dt)
        {
            // 아기 거북: 바다 쪽으로 엉금엉금 (멈칫멈칫)
            a.t -= dt;
            if (a.t <= -0.3f) a.t = Random.Range(0.6f, 1.2f);
            bool moving = a.t > 0f;
            float wiggle = Mathf.Sin(clock * 5f + a.phase) * 0.25f;
            c.heading = Mathf.LerpAngle(c.heading * Mathf.Rad2Deg, (a.dir + wiggle) * Mathf.Rad2Deg, 1f - Mathf.Exp(-dt * 3f)) * Mathf.Deg2Rad;
            if (moving)
            {
                float spd = babyDef.speed * R * (c.hold > 0f ? 0.3f : 1f);
                c.pos += new Vector2(Mathf.Cos(c.heading), Mathf.Sin(c.heading)) * spd * dt;
            }
            c.Anim = moving ? 1f : 0.2f;
            if (c.pos.magnitude >= islandR * 1.03f) ReachSea(c, -1);
        }

        void Eat(Creature predator, Agent a, Creature prey)
        {
            if (!Alive(prey)) return;
            bool isEgg = eggs.Contains(prey);
            if (isEgg) eatenEggs++; else eatenBabies++;
            fx.Ripple(prey.pos, NETR * 1.1f, new Color(1f, 0.45f, 0.4f));
            fx.Pop(prey.pos, isEgg ? "앗, 알이!" : "앗!", new Color(1f, 0.6f, 0.55f), 0f, NETR * 0.45f);
            sfx.Play("chomp");
            RemoveCreature(prey);
            a.leaving = true;
            a.target = null;
        }

        /// <summary>아기 거북이 바다에 들어감 (by = 옮겨 준 플레이어, -1이면 스스로)</summary>
        void ReachSea(Creature c, int by)
        {
            if (c.fadingOut) return;
            c.fadingOut = true;
            babies.Remove(c);
            saved++;
            if (by >= 0) carried[by]++;
            fx.Ripple(c.pos, NETR * 1.3f, new Color(0.55f, 1f, 0.8f));
            fx.Pop(c.pos, "바다로!", new Color(0.6f, 1f, 0.8f), 0f, NETR * 0.4f);
            sfx.Play("deposit");
        }

        // ================================================================ 진행
        protected override void UpdateGame(float dt)
        {
            UpdateIslandShape();
            // 바다 물고기 유지
            fishT -= dt;
            int fish = 0;
            foreach (var c in creatures) if (c.zone == 0 && !c.fadingOut) fish++;
            if (fishT <= 0f && fish < settings.creatureCount / 2) { SpawnSeaFish(); fishT = 1f; }

            if (!IsPlaying) return;
            float frac = settings.durationSec > 0 ? 1f - remaining / settings.durationSec : 0f;

            // 부화
            if (!hatchPhase && remaining <= HatchAt)
            {
                hatchPhase = true;
                if (eggs.Count > 0)
                {
                    foreach (var e in eggs) hatchQueue.Add(new KeyValuePair<Creature, float>(e, Random.Range(0f, 2.5f)));
                    fx.Pop(Vector2.zero, "부화!", new Color(1f, 0.95f, 0.7f), 0f, R * 0.18f);
                    fx.Pop(Vector2.zero, "부화!", new Color(1f, 0.95f, 0.7f), 180f, R * 0.18f);
                    sfx.Play("hatch");
                }
            }
            for (int i = hatchQueue.Count - 1; i >= 0; i--)
            {
                float t = hatchQueue[i].Value - dt;
                if (t <= 0f) { var e = hatchQueue[i].Key; hatchQueue.RemoveAt(i); Hatch(e); }
                else hatchQueue[i] = new KeyValuePair<Creature, float>(hatchQueue[i].Key, t);
            }

            // 천적: 점점 자주
            crabT -= dt;
            if (crabT <= 0f)
            {
                if (CountKind(Kind.Crab) < MaxCrabs) SpawnCrab();
                crabT = Mathf.Lerp(4.5f, 2.0f, frac) * Random.Range(0.8f, 1.2f);
            }
            gullT -= dt;
            if (gullT <= 0f)
            {
                if (CountKind(Kind.Gull) < (hatchPhase ? 3 : 2)) SpawnGull();
                gullT = Mathf.Lerp(10f, 5f, frac) * Random.Range(0.8f, 1.2f);
            }

            // 지킬 것이 하나도 없으면 일찍 끝
            if (eggs.Count == 0 && babies.Count == 0 && hatchQueue.Count == 0 && remaining > 0.5f)
                remaining = 0.01f;
        }

        protected override void OnNetMoved(int i, float dt)
        {
            var n = nets[i];
            if (n.load.Count == 0 || n.pos.magnitude < islandR * 1.06f) return;
            // 바다 위로 옮김 → 게는 바다로 쫓아내고, 아기 거북은 바다에 넣어 줌
            int crabs = 0, babs = 0;
            foreach (var c in n.load)
            {
                Agent a;
                if (!agents.TryGetValue(c, out a)) continue;
                c.Release();
                if (a.kind == Kind.Crab)
                {
                    a.leaving = true;
                    c.fadingOut = true;
                    crabs++;
                }
                else if (a.kind == Kind.Baby) { ReachSea(c, i); babs++; }
            }
            n.load.Clear();
            shooed[i] += crabs;
            fx.Ripple(n.pos, NETR * 1.5f, new Color(0.6f, 0.95f, 1f));
            if (babs > 0) NetPop(i, "+" + babs + " 바다로!", new Color(0.6f, 1f, 0.8f));
            else if (crabs > 0) { NetPop(i, "게 퇴치!", new Color(0.85f, 0.95f, 1f)); sfx.Play("deposit"); }
        }

        protected override void OnStartGame() { ResetAll(); }

        protected override void OnEnterReady()
        {
            if (eggs.Count == 0 || state == GameState.Results) ResetAll();
        }

        protected override void OnEndGame() { }

        int Stars
        {
            get
            {
                if (startEggs <= 0 || saved <= 0) return 0;
                float r = saved / (float)startEggs;
                return r >= 0.75f ? 3 : r >= 0.4f ? 2 : 1;
            }
        }

        protected override void ResultTitle(out string title, out string sub, out Color color)
        {
            int stars = Stars;
            color = stars >= 3 ? new Color(0.6f, 1f, 0.8f) : stars >= 1 ? new Color(1f, 0.95f, 0.7f) : new Color(1f, 0.7f, 0.6f);
            title = saved > 0 ? "아기 거북 " + saved + "마리 바다로!" : "아기 거북을 지키지 못했어요";
            string starText = new string('★', stars) + new string('☆', 3 - stars);
            int best = -1, bestN = 0;
            for (int i = 0; i < 4; i++) { int s = shooed[i] + carried[i]; if (s > bestN) { bestN = s; best = i; } }
            sub = starText + "  알 " + startEggs + "개 중 " + saved + "마리" + (best >= 0 ? " · 지킴이왕 " + PlayerNames[best] : "") + " · 스페이스바: 다시 하기";
        }

        protected override void RenderGame()
        {
            bool play = state == GameState.Playing || state == GameState.Countdown || state == GameState.Paused;
            bool results = state == GameState.Results;
            int best = -1, bestN = 0;
            for (int i = 0; i < 4; i++) { int s = shooed[i] + carried[i]; if (s > bestN) { bestN = s; best = i; } }
            for (int i = 0; i < 4; i++)
                badges[i].Text = PlayerNames[i] + (results && i == best ? " 지킴이왕!" : "") + "\n퇴치 " + shooed[i] + " · 구출 " + carried[i];
            string status;
            if (!hatchPhase)
            {
                int toHatch = Mathf.Max(0, Mathf.CeilToInt(remaining - HatchAt));
                status = "남은 알 " + eggs.Count + "개" + (play ? " · 부화까지 " + toHatch + "초" : "");
            }
            else status = "바다로 " + saved + "마리 · 해변에 " + (babies.Count + hatchQueue.Count) + "마리";
            foreach (var t in statusTexts)
            {
                t.Visible = play || results;
                t.Text = status;
            }
        }

        protected override void DrawGameSettings()
        {
            GUILayout.Label("거북이 알: " + settings.eggCount + "개 (다음 판부터)");
            settings.eggCount = Mathf.RoundToInt(GUILayout.HorizontalSlider(settings.eggCount, 6, 16));
            GUILayout.Label("남은 시간이 " + Mathf.RoundToInt(HatchAt) + "초가 되면 알이 부화합니다.");
            foreach (var id in new[] { SpeciesId.Crab, SpeciesId.Seagull })
            {
                bool f = IsFlipped(id);
                var def = species.Find(s => s.id == id);
                bool nf = GUILayout.Toggle(f, " " + (def != null ? def.displayName : id.ToString()) + " 앞뒤 반대 (다음 판부터)");
                if (nf != f) SetFlipped(id, nf);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            foreach (var kv in agents) if (kv.Value.shadow != null) Destroy(kv.Value.shadow.gameObject);
        }
    }
}
