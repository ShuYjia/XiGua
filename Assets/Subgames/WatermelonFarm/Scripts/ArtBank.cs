using System.Collections.Generic;
using UnityEngine;

namespace WatermelonFarm
{
    // AI 生成的手绘素材位于 Assets/Resources/Art/。
    // 任何一张缺图都会自动回退到 SpriteFactory 的程序化绘制，工程始终可运行。
    public static class ArtBank
    {
        const string PREFIX = "Art/";
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string name)
        {
            Sprite s;
            if (cache.TryGetValue(name, out s)) return s;

            // 优先用已导入为 Sprite 的资源；若导入设置尚未生效，退而用 Texture2D 现建
            s = Resources.Load<Sprite>(PREFIX + name);
            if (s == null)
            {
                Texture2D t = Resources.Load<Texture2D>(PREFIX + name);
                if (t != null)
                {
                    s = Sprite.Create(t, new Rect(0, 0, t.width, t.height),
                        new Vector2(0.5f, 0.5f), 100f);
                    if (s != null) s.name = name;
                }
            }
            cache[name] = s;   // null 也缓存，避免每帧重复查询 Resources
            return s;
        }

        public static bool Has(string name) { return Get(name) != null; }

        public static Sprite Background { get { return Get("bg_farm"); } }

        public static Sprite Plant(int stage, bool dead)
        {
            return Get(dead ? "plant_dead" : "plant" + Mathf.Clamp(stage, 0, 7));
        }

        public static Sprite Soil(bool locked)
        {
            return Get(locked ? "soil_locked" : "soil");
        }

        // 成熟瓜按品种取图：京欣1号 / 航兴1号 / 京秀 各有一张对应真实品种特征的插画。
        // 缺图时返回 null，由 SpriteFactory 回退到 plant7 程序化绘制。
        public static Sprite Ripe(string vid)
        {
            if (string.IsNullOrEmpty(vid)) return null;
            return Get("melon_" + vid);
        }

        // 图鉴收藏卡用的网络实物资料图（已抠成圆角透明 PNG）。
        // 缺图时返回 null，UI 会回退到品种插画 / plant7。
        public static Sprite Photo(string vid)
        {
            if (string.IsNullOrEmpty(vid)) return null;
            return Get("photo_" + vid);
        }
    }
}
