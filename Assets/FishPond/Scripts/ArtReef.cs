using System.Collections.Generic;
using UnityEngine;

namespace FishPond
{
    /// <summary>
    /// 새 생물(쏠배감펭 지느러미·가오리·바다거북)과 산호초 장식 그래픽.
    /// 산호류는 흰색 명암으로 만들고 SpriteRenderer 색으로 물들입니다.
    /// </summary>
    public static partial class Art
    {
        public static Sprite EdgeShade
        {
            get
            {
                return Make("edgeshade", 256, 256, Center, (x, y) =>
                {
                    float r = Vector2.Distance(new Vector2(x, y), new Vector2(128, 128)) / 127f;
                    float t = S((r - 0.93f) / 0.07f);
                    return new Color(0f, 0.02f, 0.04f, t * t * (3f - 2f * t) * (r > 1f ? 0f : 0.85f));
                });
            }
        }


    }
}
