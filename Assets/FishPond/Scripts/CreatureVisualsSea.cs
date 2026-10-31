using UnityEngine;

namespace FishPond
{
    // ================================================================ 가오리: 넓은 날개로 너울너울
    public class StingrayVisual : CreatureVisual
    {
        readonly Transform body;
        readonly LineRenderer tail;
        readonly Vector3[] buf = new Vector3[10];

        public StingrayVisual(Transform parent, float length) : base(parent, length)
        {
            var size = new Vector2(L * 0.85f, L * 1.0f);
            shadow = SpritePart(root, "shadow", Art.StingrayBody, new Color(0f, 0.01f, 0.02f, 0.28f), -5, Vector2.zero, size).r.transform;
            tail = LinePart(root, "tail", buf.Length, new Color(0.62f, 0.52f, 0.36f, 1f), new Color(0.5f, 0.42f, 0.3f, 0.6f), 0.035f * L, Taper(0.15f), 0);
            body = SpritePart(root, "body", Art.StingrayBody, Color.white, 2, Vector2.zero, size).r.transform;
        }

        public override void Animate(float dt, float swim, float worldScale)
        {
            phase += dt * (2.2f + swim * 4f);
            UpdateWidths(worldScale);
            float flap = Mathf.Sin(phase);
            body.localScale = new Vector3(L * 0.85f * (1f + 0.04f * flap), L * (1f - 0.08f * Mathf.Abs(flap)), 1f);
            shadow.localScale = body.localScale;
            var p = new Vector2(-0.36f * L, 0f);
            float seg = 0.75f * L / (buf.Length - 1);
            for (int k = 0; k < buf.Length; k++)
            {
                float s = k / (buf.Length - 1f);
                buf[k] = p;
                p += new Vector2(-Mathf.Cos(Mathf.Sin(phase * 0.8f + s * 3f) * 0.35f * s), Mathf.Sin(Mathf.Sin(phase * 0.8f + s * 3f) * 0.35f * s)) * seg;
            }
            tail.SetPositions(buf);
        }
    }

    // ================================================================ 바다거북: 큰 앞발로 천천히 노 젓기
    public class TurtleVisual : CreatureVisual
    {
        readonly Transform frontL, frontR, backL, backR, head;

        public TurtleVisual(Transform parent, float length) : base(parent, length)
        {
            var skin = new Color(0.5f, 0.58f, 0.38f);
            var shell = new Vector2(L * 0.62f, L * 0.52f);
            shadow = SpritePart(root, "shadow", Art.Circle, new Color(0f, 0.01f, 0.02f, 0.3f), -5, Vector2.zero, shell * 1.1f).r.transform;

            frontL = Node(root, "frontL", new Vector2(L * 0.14f, L * 0.2f));
            frontR = Node(root, "frontR", new Vector2(L * 0.14f, -L * 0.2f));
            backL = Node(root, "backL", new Vector2(-L * 0.22f, L * 0.16f));
            backR = Node(root, "backR", new Vector2(-L * 0.22f, -L * 0.16f));
            SpritePart(frontL, "flipper", Art.Circle, skin, 0, new Vector2(-L * 0.04f, L * 0.13f), new Vector2(L * 0.14f, L * 0.34f), 30f);
            SpritePart(frontR, "flipper", Art.Circle, skin, 0, new Vector2(-L * 0.04f, -L * 0.13f), new Vector2(L * 0.14f, L * 0.34f), -30f);
            SpritePart(backL, "flipper", Art.Circle, skin, 0, new Vector2(-L * 0.04f, L * 0.05f), new Vector2(L * 0.12f, L * 0.14f), 20f);
            SpritePart(backR, "flipper", Art.Circle, skin, 0, new Vector2(-L * 0.04f, -L * 0.05f), new Vector2(L * 0.12f, L * 0.14f), -20f);

            head = Node(root, "head", new Vector2(L * 0.36f, 0f));
            SpritePart(head, "head", Art.Circle, skin, 1, Vector2.zero, new Vector2(L * 0.19f, L * 0.15f));
            SpritePart(head, "eyeL", Art.Circle, new Color(0.08f, 0.07f, 0.05f), 3, new Vector2(L * 0.03f, L * 0.045f), Vector2.one * L * 0.03f);
            SpritePart(head, "eyeR", Art.Circle, new Color(0.08f, 0.07f, 0.05f), 3, new Vector2(L * 0.03f, -L * 0.045f), Vector2.one * L * 0.03f);

            SpritePart(root, "shell", Art.TurtleShell, Color.white, 2, Vector2.zero, shell);
        }

        public override void Animate(float dt, float swim, float worldScale)
        {
            phase += dt * (1.2f + swim * 3f);
            float s = Mathf.Sin(phase);
            frontL.localRotation = Quaternion.Euler(0, 0, s * 35f);
            frontR.localRotation = Quaternion.Euler(0, 0, -s * 35f);
            backL.localRotation = Quaternion.Euler(0, 0, -s * 15f);
            backR.localRotation = Quaternion.Euler(0, 0, s * 15f);
            head.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(phase * 0.5f) * 8f);
        }
    }
}
