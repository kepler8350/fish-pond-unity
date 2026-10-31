using UnityEngine;

namespace FishPond
{
    /// <summary>
    /// 상어 구조대의 상어. 상어 쪽 바다만 돌아다니며, 가까운 물고기를 쫓아가 잡아먹습니다.
    /// 물고기를 담은 뜰채에 부딪히면 물고기를 떨어뜨리게 만듭니다.
    /// </summary>
    public class Shark
    {
        public readonly GameObject go;
        public Vector2 pos;
        public float heading;
        public readonly float length;         // 스테이지 단위 몸길이

        readonly Transform tail, body, shadow;
        Vector2 target;
        float wanderT, cooldown, phase, chomp;
        Creature prey;
        public bool Hunting { get { return prey != null; } }

        public Shark(Transform parent, Vector2 start, float lengthStage)
        {
            length = lengthStage;
            pos = start;
            heading = Random.value * Mathf.PI * 2f;
            go = new GameObject("Shark");
            go.transform.SetParent(parent, false);

            shadow = MakeSprite("shadow", Art.SharkBody, new Color(0f, 0.01f, 0.03f, 0.35f), 8, Vector2.zero, new Vector2(1f, 1f)).transform;
            tail = new GameObject("tailPivot").transform;
            tail.SetParent(go.transform, false);
            tail.localPosition = new Vector2(-0.47f, 0f);
            MakeSprite("tail", Art.SharkTail, Color.white, 31, Vector2.zero, new Vector2(0.32f, 0.42f)).transform.SetParent(tail, false);
            body = MakeSprite("body", Art.SharkBody, Color.white, 32, Vector2.zero, Vector2.one).transform;
        }

        SpriteRenderer MakeSprite(string name, Sprite sp, Color c, int order, Vector2 pos, Vector2 scale)
        {
            var g = new GameObject(name);
            g.transform.SetParent(go.transform, false);
            g.transform.localPosition = pos;
            g.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.color = c;
            sr.sortingOrder = order;
            return sr;
        }

        static Vector2 Dir(float a) { return new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
        static float Ang(Vector2 v) { return Mathf.Atan2(v.y, v.x); }

        public Vector2 Mouth { get { return pos + Dir(heading) * length * 0.45f; } }

        public void Update(float dt, SharkRescueGame g)
        {
            float R = g.R;
            cooldown -= dt;
            wanderT -= dt;

            // 사냥감 고르기 (진행 중이고 배부르지 않을 때)
            if (prey != null && (prey.state != Creature.State.Swim || prey.zone != 0 || prey.fadingOut || !g.Alive(prey))) prey = null;
            if (g.IsPlaying && cooldown <= 0f && prey == null) prey = g.NearestPrey(pos, R * 0.75f);
            if (!g.IsPlaying) prey = null;

            float speed, turn;
            float desired;
            if (prey != null)
            {
                var lead = prey.pos + (prey.pos - pos).normalized * length * 0.1f;
                desired = Ang(lead - pos);
                speed = R * 0.5f;
                turn = 2.4f;
            }
            else
            {
                if (wanderT <= 0f || (target - pos).magnitude < R * 0.1f)
                {
                    target = g.RandomSharkPoint();
                    wanderT = Random.Range(3f, 6f);
                }
                desired = Ang(target - pos);
                speed = R * (cooldown > 0f ? 0.16f : 0.22f);
                turn = 1.2f;
            }
            if (g.NearSharkEdge(pos)) desired = Ang(g.SharkZoneCenter - pos);

            float d = Mathf.DeltaAngle(heading * Mathf.Rad2Deg, desired * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            heading += Mathf.Clamp(d, -turn * dt, turn * dt);
            pos += Dir(heading) * speed * dt;
            pos = g.ClampShark(pos, length * 0.35f);

            // 잡아먹기
            if (prey != null && Vector2.Distance(Mouth, prey.pos) < length * 0.22f + prey.Radius(R) * 0.5f)
            {
                g.SharkEats(this, prey);
                prey = null;
                cooldown = Random.Range(2.2f, 3.2f);
                chomp = 1f;
            }

            // 물고기 담은 뜰채 물기
            if (g.IsPlaying && cooldown <= 0f)
            {
                for (int i = 0; i < 4; i++)
                {
                    var n = g.nets[i];
                    if (n.load.Count == 0 || n.stunT > 0f) continue;
                    if (Vector2.Distance(Mouth, n.pos) < g.NETR * 0.9f + length * 0.12f)
                    {
                        g.SharkBitesNet(i, this);
                        cooldown = 1.2f;
                        chomp = 1f;
                        break;
                    }
                }
            }

            // 그리기
            phase += dt * (prey != null ? 11f : 5f);
            chomp = Mathf.Max(0f, chomp - dt * 3f);
            var t = go.transform;
            t.localPosition = new Vector3(pos.x, pos.y, -0.4f);
            t.localRotation = Quaternion.Euler(0, 0, heading * Mathf.Rad2Deg);
            t.localScale = new Vector3(length, length, 1f);
            tail.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(phase) * (prey != null ? 26f : 16f));
            body.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(phase + Mathf.PI) * 3f);
            body.localScale = new Vector3(1f + chomp * 0.08f, 1f - chomp * 0.06f, 1f);
            float c = Mathf.Cos(-heading), s = Mathf.Sin(-heading);
            var off = new Vector2(0.18f, -0.26f) * 0.5f;
            shadow.localPosition = new Vector3(off.x * c - off.y * s, off.x * s + off.y * c, 0.02f);
        }

        public void Destroy() { Object.Destroy(go); }
    }
}
