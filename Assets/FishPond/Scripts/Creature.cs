using UnityEngine;

namespace FishPond
{
    /// <summary>
    /// 연못 생물 한 마리. 위치는 Stage 로컬 좌표(연못 중심 = 0,0)이고
    /// 속도는 모두 연못 반지름 R에 비례합니다.
    /// </summary>
    public class Creature
    {
        public enum State { Swim, Caught, Fly, InBucket }

        const float Tau = Mathf.PI * 2f;

        public readonly SpeciesDef def;
        public readonly GameObject go;
        public readonly CreatureVisual vis;
        public readonly float size;
        /// <summary>구역 번호 (상어 구조대: 0 상어 쪽, 1 안전한 쪽)</summary>
        public int zone;

        public State state = State.Swim;
        public Vector2 pos;
        public float heading;
        public float alpha;
        public bool fadingOut;

        // 잡기 판정
        public float hold;
        public int holdNet = -1;

        // 상태별
        public int net = -1;
        public int bucket = -1;
        float orbit;
        float bucketAngle, bucketRadius, bucketSpeed;
        Vector2 flyFrom, flyTo;
        float flyT, flyDur, hop;

        // 이동
        Vector2 target, vel;
        float wanderT, boost, swimAnim;
        float jetT, squish, fleeCd;        // 오징어
        float puffT, puffK;                // 복어
        float inkCd, crawlT, dashT, dashSpeed, dashK; // 문어
        bool crawling = true;
        float pulseT;                      // 해파리

        int lastOrder = int.MinValue;
        float lastAlpha = -1f;

        public Creature(SpeciesDef def, Transform parent, Vector2 pos)
        {
            this.def = def;
            go = new GameObject(def.displayName);
            go.transform.SetParent(parent, false);
            vis = CreatureVisual.Create(def, go.transform);
            size = Random.Range(0.88f, 1.12f);
            this.pos = pos;
            heading = Random.value * Tau;
            jetT = Random.Range(0.3f, 1.5f);
            crawlT = Random.Range(0.5f, 2f);
            pulseT = Random.value * 1.8f;
            inkCd = 1f;
            vis.SetAlpha(0f);
        }

        public bool IsJelly { get { return def.id == SpeciesId.Jellyfish; } }
        public bool InPond { get { return state != State.InBucket; } }
        public float HoldRequired { get { return PondGame.HOLD * def.holdMultiplier * (puffK > 0.5f ? 2f : 1f); } }
        public float Radius(float R) { return def.length * R * size * 0.5f; }

        static Vector2 Dir(float a) { return new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
        static float Ang(Vector2 v) { return Mathf.Atan2(v.y, v.x); }

        void Steer(float desired, float rate, float dt)
        {
            float d = Mathf.DeltaAngle(heading * Mathf.Rad2Deg, desired * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            heading += Mathf.Clamp(d, -rate * dt, rate * dt);
        }

        PondGame game;

        void NewTarget(float R)
        {
            target = game.RandomTarget(this);
            wanderT = Random.Range(2f, 5f);
        }

        bool ClampInPond(float R, float f)
        {
            var q = game.ClampToZone(this, pos, f);
            if ((q - pos).sqrMagnitude < 1e-10f) return false;
            pos = q;
            return true;
        }

        float AwayFromEdge(float desired, float f)
        {
            return game.NearEdge(this, pos, f) ? Ang(game.Inward(this, pos)) : desired;
        }

        // ============================================================ 매 프레임
        public void Update(float dt, PondGame g)
        {
            game = g;
            if (fadingOut) alpha = Mathf.Max(0f, alpha - dt * 1.5f);
            else if (alpha < 1f) alpha = Mathf.Min(1f, alpha + dt * 1.3f);

            switch (state)
            {
                case State.Swim:
                    switch (def.id)
                    {
                        case SpeciesId.Squid: SwimSquid(dt, g); break;
                        case SpeciesId.Octopus: SwimOctopus(dt, g); break;
                        case SpeciesId.Jellyfish: SwimJelly(dt, g); break;
                        default: SwimFish(dt, g); break;
                    }
                    break;

                case State.Caught:
                {
                    var n = g.nets[net];
                    orbit += dt * 1.6f;
                    var tp = n.pos + Dir(orbit) * g.NETR * 0.32f;
                    pos += (tp - pos) * (1f - Mathf.Exp(-dt * 14f));
                    heading = orbit + Mathf.PI * 0.5f;
                    swimAnim = 1f;
                    puffK = Mathf.MoveTowards(puffK, 0f, dt * 2f);
                    dashK = Mathf.MoveTowards(dashK, 0f, dt * 2f);
                    break;
                }

                case State.Fly:
                {
                    flyT += dt;
                    float p = Mathf.Min(1f, flyT / flyDur), e = 1f - (1f - p) * (1f - p);
                    pos = Vector2.Lerp(flyFrom, flyTo, e);
                    heading = Ang(flyTo - flyFrom);
                    hop = Mathf.Sin(p * Mathf.PI);
                    swimAnim = 1f;
                    if (p >= 1f)
                    {
                        state = State.Swim;
                        boost = 1f;
                        hop = 0f;
                        g.fx.Ripple(pos, g.NETR * 1.1f, new Color(0.85f, 0.98f, 0.96f));
                        NewTarget(g.R);
                    }
                    break;
                }

                case State.InBucket:
                {
                    Vector2 bp; float br;
                    g.BucketSlot(bucket, out bp, out br);
                    bucketAngle += bucketSpeed * dt;
                    pos = bp + Dir(bucketAngle) * bucketRadius * br;
                    heading = bucketAngle + Mathf.Sign(bucketSpeed) * Mathf.PI * 0.5f;
                    swimAnim = 0.4f;
                    break;
                }
            }
            Render(dt, g);
        }

        // ------------------------------------------------------------ 흰동가리 · 블루탱 · 복어
        void SwimFish(float dt, PondGame g)
        {
            float R = g.R;
            wanderT -= dt;
            if (wanderT <= 0f || (target - pos).magnitude < R * 0.08f) NewTarget(R);
            float desired = Ang(target - pos);

            bool calm = def.id == SpeciesId.SeaTurtle;
            float turnRate = def.id == SpeciesId.SeaTurtle ? 0.7f : def.id == SpeciesId.Stingray ? 1.1f : 1.8f;
            foreach (var n in g.nets)
            {
                float d = Vector2.Distance(pos, n.pos);
                if (!calm && n.speed > PondGame.FLEE * R && d < g.NETR * 2.8f) { desired = Ang(pos - n.pos); boost = 1f; }
                if (def.id == SpeciesId.Pufferfish && puffT <= 0f && d < g.NETR * 2.4f && g.IsPlaying)
                {
                    puffT = 2.6f;
                    g.sfx.Play("puff", 0.7f);
                }
            }
            Vector2 threat;
            if (g.Threat(this, out threat)) { desired = Ang(pos - threat); boost = Mathf.Max(boost, 0.5f); }
            desired = AwayFromEdge(desired, 0.8f);

            if (puffT > 0f) { puffT -= dt; puffK = Mathf.MoveTowards(puffK, 1f, dt * 5f); }
            else puffK = Mathf.MoveTowards(puffK, 0f, dt * 1.2f);

            Steer(desired, turnRate + boost * 4f, dt);
            float spd = def.speed * g.settings.fishSpeed * R * (1f + boost * 1.1f) * (hold > 0f ? 0.3f : 1f) * (1f - 0.55f * puffK);
            pos += Dir(heading) * spd * dt;
            boost = Mathf.Max(0f, boost - dt * 0.8f);
            swimAnim = Mathf.Clamp01(spd / (R * 0.6f));
            ClampInPond(R, 0.9f);
        }

        // ------------------------------------------------------------ 오징어: 물을 뿜어 툭툭 튀어나감
        void SwimSquid(float dt, PondGame g)
        {
            float R = g.R, slow = hold > 0f ? 0.3f : 1f;
            jetT -= dt; fleeCd -= dt; wanderT -= dt;
            if (wanderT <= 0f) NewTarget(R);
            Steer(AwayFromEdge(Ang(target - pos), 0.7f), 1.4f, dt);

            if (fleeCd <= 0f)
            {
                foreach (var n in g.nets)
                {
                    if (n.speed > 0.5f * R && Vector2.Distance(pos, n.pos) < g.NETR * 2.6f)
                    {
                        heading = Ang(pos - n.pos);
                        vel = Dir(heading) * R * def.speed * g.settings.fishSpeed * 1.4f;
                        fleeCd = 1.4f;
                        jetT = Random.Range(1.2f, 2f);
                        squish = 1f;
                        if (g.IsPlaying) g.sfx.Play("jet", 0.6f);
                        break;
                    }
                }
            }
            if (jetT <= 0f)
            {
                vel += Dir(heading) * R * def.speed * g.settings.fishSpeed * 0.9f;
                jetT = Random.Range(1.3f, 2.4f);
                squish = 1f;
            }
            vel *= Mathf.Exp(-dt * 2.2f);
            pos += vel * dt * slow;
            squish = Mathf.Max(0f, squish - dt * 3f);
            swimAnim = Mathf.Clamp01(vel.magnitude / R);
            if (ClampInPond(R, 0.9f)) vel *= 0.5f;
        }

        // ------------------------------------------------------------ 문어: 천천히 기다가, 뜰채가 오면 먹물 쏘고 도망
        void SwimOctopus(float dt, PondGame g)
        {
            float R = g.R, slow = hold > 0f ? 0.3f : 1f;
            inkCd -= dt;
            if (dashT > 0f)
            {
                dashT -= dt;
                dashSpeed *= Mathf.Exp(-dt * 2f);
                pos += Dir(heading) * dashSpeed * dt;
                dashK = Mathf.MoveTowards(dashK, 1f, dt * 8f);
                swimAnim = 1f;
            }
            else
            {
                dashK = Mathf.MoveTowards(dashK, 0f, dt * 2f);
                crawlT -= dt;
                if (crawlT <= 0f)
                {
                    crawling = !crawling;
                    crawlT = crawling ? Random.Range(1.5f, 3f) : Random.Range(0.6f, 1.4f);
                }
                wanderT -= dt;
                if (wanderT <= 0f || (target - pos).magnitude < R * 0.08f) NewTarget(R);
                float desired = AwayFromEdge(Ang(target - pos), 0.75f);
                if (crawling)
                {
                    Steer(desired, 1.2f, dt);
                    pos += Dir(heading) * def.speed * g.settings.fishSpeed * R * dt * slow;
                    swimAnim = 0.6f;
                }
                else swimAnim = 0.15f;
            }

            if (inkCd <= 0f)
            {
                foreach (var n in g.nets)
                {
                    float d = Vector2.Distance(pos, n.pos);
                    if (d < g.NETR * 1.9f && (n.speed > 0.3f * R || d < g.NETR * 1.15f))
                    {
                        g.fx.Ink(pos, g.NETR * 2.2f);
                        heading = Ang(pos - n.pos);
                        dashT = 0.8f;
                        dashSpeed = R * 1.4f;
                        inkCd = 6f;
                        hold = 0f;
                        holdNet = -1;
                        if (g.IsPlaying) g.sfx.Play("ink");
                        break;
                    }
                }
            }
            ClampInPond(R, 0.9f);
        }

        // ------------------------------------------------------------ 해파리: 둥실둥실 (잡을 수 없음, 닿으면 찌릿)
        void SwimJelly(float dt, PondGame g)
        {
            float R = g.R;
            pulseT -= dt; wanderT -= dt;
            if (wanderT <= 0f) NewTarget(R);
            Steer(AwayFromEdge(Ang(target - pos), 0.7f), 0.5f, dt);
            if (pulseT <= 0f) { vel += Dir(heading) * R * def.speed * g.settings.fishSpeed * 2f; pulseT = 1.8f; }
            vel *= Mathf.Exp(-dt * 1.2f);
            pos += vel * dt;
            swimAnim = 0.3f;
            if (ClampInPond(R, 0.85f)) vel *= 0.3f;
        }

        public void Bounce(Vector2 from, float R)
        {
            heading = Ang(pos - from);
            vel = Dir(heading) * R * 0.5f;
        }

        // ============================================================ 상태 전환
        public void Catch(int netIndex)
        {
            state = State.Caught;
            net = netIndex;
            hold = 0f;
            holdNet = -1;
            orbit = Random.value * Tau;
            puffT = 0f;
        }

        public void Release()
        {
            state = State.Swim;
            net = -1;
            boost = 1f;
            hop = 0f;
        }

        public void StartFly(Vector2 netVel, PondGame g)
        {
            state = State.Fly;
            net = -1;
            flyT = 0f;
            flyFrom = pos;
            var to = pos + netVel * 0.12f;
            game = g;
            to = g.ClampToZone(this, to, 0.75f);
            flyTo = to;
            flyDur = 0.45f + (to - pos).magnitude / (g.R * 3f);
        }

        public void ToBucket(int b)
        {
            state = State.InBucket;
            bucket = b;
            net = -1;
            bucketAngle = Random.value * Tau;
            bucketRadius = Random.Range(0.25f, 0.55f);
            bucketSpeed = Random.Range(0.6f, 1.2f) * (Random.value < 0.5f ? -1f : 1f);
            puffK = 0f; dashK = 0f; squish = 0f; hop = 0f;
        }

        public void Rescale(float k)
        {
            pos *= k; target *= k; vel *= k; flyFrom *= k; flyTo *= k;
        }

        public void Destroy()
        {
            Object.Destroy(go);
        }

        // ============================================================ 그리기
        void Render(float dt, PondGame g)
        {
            float R = g.R, len = def.length * R * size;
            float scale = R * size, z = 0f;
            int order = 10;
            Vector2 shadowOff = new Vector2(0.18f, -0.26f) * len;
            Vector2 drawPos = pos;

            switch (state)
            {
                case State.Caught:
                    scale *= Mathf.Min(1f, g.NETR * 1.25f / Mathf.Max(len, 0.0001f));
                    order = 52; z = -0.9f; shadowOff *= 0.6f;
                    break;
                case State.Fly:
                    scale *= 1f + hop * 0.35f;
                    drawPos.y += hop * g.NETR * 0.6f;
                    order = 66; z = -1.5f; shadowOff *= 1f + hop * 2f;
                    break;
                case State.InBucket:
                {
                    Vector2 bp; float br;
                    g.BucketSlot(bucket, out bp, out br);
                    scale = Mathf.Min(scale * 0.45f, br * 0.5f / def.length);
                    order = 42; z = -0.65f; shadowOff *= 0.4f;
                    break;
                }
            }

            var t = go.transform;
            t.localPosition = new Vector3(drawPos.x, drawPos.y, z);
            t.localRotation = Quaternion.Euler(0, 0, heading * Mathf.Rad2Deg);
            t.localScale = new Vector3(scale, scale, scale);

            if (order != lastOrder) { vis.SetSorting(order); lastOrder = order; }
            if (Mathf.Abs(alpha - lastAlpha) > 0.001f) { vis.SetAlpha(alpha); lastAlpha = alpha; }

            vis.SetPuff(puffK);
            vis.SetSquish(squish);
            vis.SetDash(dashK);
            vis.SetShadow(shadowOff, heading, scale);
            vis.Animate(dt, swimAnim, scale);
        }
    }
}
