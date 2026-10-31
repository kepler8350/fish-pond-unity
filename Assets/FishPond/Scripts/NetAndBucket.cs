using System.Collections.Generic;
using UnityEngine;

namespace FishPond
{
    /// <summary>
    /// 플레이어의 뜰채. 실제 장비에서는 카메라가 찾은 IR LED 위치로 움직이고,
    /// 테스트할 때는 키보드·마우스·터치로 움직입니다.
    /// 비주얼은 반지름 1로 만들고 루트를 NETR배로 스케일합니다.
    /// </summary>
    public class Net
    {
        public readonly int id;
        public readonly Color color;
        public readonly GameObject go;

        public Vector2 pos, target, vel;
        public float speed;
        public bool pointer, external;
        public readonly List<Creature> load = new List<Creature>();
        public float fastT, prog, stunT, rippleT;

        readonly Transform handlePivot;
        readonly SpriteRenderer rim, fill;
        readonly LineRenderer arc;
        readonly FloorText fullText;
        readonly Vector3[] arcBuf = new Vector3[41];

        public Net(int id, Color color, Transform parent)
        {
            this.id = id;
            this.color = color;
            go = new GameObject("Net P" + (id + 1));
            go.transform.SetParent(parent, false);

            Sprite("shadow", go.transform, Art.Circle, new Color(0f, 0f, 0f, 0.28f), 47, new Vector2(0.12f, -0.18f), Vector2.one * 2f);
            fill = Sprite("mesh", go.transform, Art.NetMesh, Color.white, 50, Vector2.zero, Vector2.one * 2.1f);

            handlePivot = new GameObject("handle").transform;
            handlePivot.SetParent(go.transform, false);
            Sprite("stick", handlePivot, Art.Square, new Color(0.79f, 0.66f, 0.47f), 35, new Vector2(1.65f, 0f), new Vector2(1.3f, 0.15f));
            Sprite("grip", handlePivot, Art.Square, color, 36, new Vector2(2.15f, 0f), new Vector2(0.6f, 0.22f));

            rim = Sprite("rim", go.transform, Art.Ring(0.12f), color, 60, Vector2.zero, Vector2.one * 2.04f);

            var g = new GameObject("progress");
            g.transform.SetParent(go.transform, false);
            arc = g.AddComponent<LineRenderer>();
            arc.useWorldSpace = false;
            arc.sharedMaterial = Art.Unlit;
            arc.alignment = LineAlignment.TransformZ;
            arc.numCapVertices = 2;
            arc.startColor = arc.endColor = Color.white;
            arc.sortingOrder = 62;
            arc.positionCount = 0;

            fullText = new FloorText(go.transform, "full", 63, Color.white, 0.32f);
            fullText.Text = "가득!";
            fullText.Visible = false;
        }

        static SpriteRenderer Sprite(string name, Transform parent, Sprite sp, Color c, int order, Vector2 pos, Vector2 scale)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.color = c;
            sr.sortingOrder = order;
            return sr;
        }

        public void Render(PondGame g, float time)
        {
            float r = g.NETR;
            go.transform.localPosition = new Vector3(pos.x, pos.y, -1f);
            go.transform.localScale = new Vector3(r, r, 1f);

            var anchor = g.HandleAnchor(id);
            var toAnchor = anchor - pos;
            float ha = toAnchor.magnitude < r * 1.5f ? Mathf.Atan2(anchor.y, anchor.x) : Mathf.Atan2(toAnchor.y, toAnchor.x);
            handlePivot.localRotation = Quaternion.Euler(0, 0, ha * Mathf.Rad2Deg);
            bool showHandle = g.settings.showHandles && !external;
            if (handlePivot.gameObject.activeSelf != showHandle) handlePivot.gameObject.SetActive(showHandle);

            bool flicker = stunT > 0f && Mathf.FloorToInt(time * 12f) % 2 == 0;
            rim.color = flicker ? new Color(0.6f, 0.6f, 0.7f) : color;
            fill.color = stunT > 0f ? new Color(0.75f, 0.65f, 1f, 0.8f) : Color.white;

            if (prog > 0f)
            {
                int n = Mathf.Max(2, Mathf.RoundToInt(Mathf.Clamp01(prog) * 40f) + 1);
                arc.positionCount = n;
                for (int i = 0; i < n; i++)
                {
                    float a = Mathf.PI * 0.5f - Mathf.PI * 2f * Mathf.Clamp01(prog) * i / 40f;
                    arcBuf[i] = new Vector3(Mathf.Cos(a) * 1.2f, Mathf.Sin(a) * 1.2f, 0f);
                }
                arc.SetPositions(arcBuf);
                arc.widthMultiplier = Mathf.Max(0.03f, r * 0.08f);
            }
            else arc.positionCount = 0;

            bool full = load.Count >= PondGame.CAP;
            fullText.Visible = full;
            if (full)
            {
                var o = -new Vector2(Mathf.Cos(ha), Mathf.Sin(ha)) * 1.45f;
                fullText.Place(o, g.PlayerTextRot(id), -0.1f);
            }
        }
    }

    /// <summary>플레이어의 낚시통. 비주얼은 반지름 1로 만들고 루트를 radius배로 스케일합니다.</summary>
    public class Bucket
    {
        public readonly int id;
        public Vector2 pos;
        public float radius = 1f;
        public float textRot;

        readonly GameObject go;
        readonly Transform textRoot;
        readonly FloorText score, label, rank;

        public Bucket(int id, Color color, Transform parent)
        {
            this.id = id;
            go = new GameObject("Bucket P" + (id + 1));
            go.transform.SetParent(parent, false);

            Make("halo", Art.SoftCircle, new Color(color.r, color.g, color.b, 0.3f), 39, 2.9f);
            Make("glow", Art.Ring(0.1f), color, 40, 2.18f);
            Make("water", Art.Circle, new Color(0.07f, 0.33f, 0.4f), 41, 1.86f);
            Make("rim", Art.Ring(0.14f), new Color(0.48f, 0.34f, 0.21f), 44, 2.0f);
            Make("rimLine", Art.Ring(0.03f), new Color(0.66f, 0.49f, 0.32f), 45, 1.88f);

            textRoot = new GameObject("texts").transform;
            textRoot.SetParent(go.transform, false);
            score = new FloorText(textRoot, "score", 46, Color.white, 0.62f);
            score.Place(new Vector2(0f, 0.08f), 0f, -0.05f);
            label = new FloorText(textRoot, "label", 46, color, 0.2f);
            label.Place(new Vector2(0f, -0.45f), 0f, -0.05f);
            rank = new FloorText(textRoot, "rank", 46, Color.white, 0.36f);
            rank.Place(new Vector2(0f, 1.5f), 0f, -0.05f);
            rank.Visible = false;
        }

        void Make(string name, Sprite sp, Color c, int order, float scale)
        {
            var g = new GameObject(name);
            g.transform.SetParent(go.transform, false);
            g.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.color = c;
            sr.sortingOrder = order;
        }

        public void Layout(Vector2 p, float r, float rotDeg)
        {
            pos = p; radius = r; textRot = rotDeg;
            go.transform.localPosition = new Vector3(p.x, p.y, -0.6f);
            go.transform.localScale = new Vector3(r, r, 1f);
            textRoot.localRotation = Quaternion.Euler(0, 0, rotDeg);
        }

        public void SetScore(int points, int count)
        {
            score.Text = points.ToString();
            label.Text = PondGame.PlayerNames[id] + " · " + count + "마리";
        }

        public void SetRank(string text, Color c)
        {
            bool on = !string.IsNullOrEmpty(text);
            rank.Visible = on;
            if (on) { rank.Text = text; rank.Color = c; }
        }
    }
}
