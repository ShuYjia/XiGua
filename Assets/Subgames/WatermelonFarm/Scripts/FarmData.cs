using System;
using System.Collections.Generic;
using UnityEngine;

namespace WatermelonFarm
{
    // ── 品种（庞各庄代表品种，数值与网页版完全一致） ──
    public sealed class Variety
    {
        public string id;
        public string name;
        public int seed;        // 种子价格
        public int basePrice;   // 售价基数
        public float dur;       // 生育期倍率
        public float tw;        // 目标单瓜重 kg
        public float brix;      // 糖度潜力
        public string desc;     // 简短描述
        public string file;     // 品种档案
        public string story;    // 它与庞各庄
        public string tips;     // 挑瓜小贴士

        public Variety(string id, string name, int seed, int basePrice, float dur, float tw, float brix,
            string desc, string file, string story, string tips)
        {
            this.id = id; this.name = name; this.seed = seed; this.basePrice = basePrice;
            this.dur = dur; this.tw = tw; this.brix = brix;
            this.desc = desc; this.file = file; this.story = story; this.tips = tips;
        }
    }

    // ── 生长阶段 ──
    public sealed class Stage
    {
        public string name;
        public int dur;   // 该阶段所需 tick 数（1 tick = 1 秒 × 倍速）
        public int size;  // 网页版 emoji 字号，这里换算为像素大小
        public Stage(string name, int dur, int size) { this.name = name; this.dur = dur; this.size = size; }
    }

    public static class FarmData
    {
        public const int FERT_PRICE = 20;
        public const int FERT_BONUS = 25;
        public const int UPG_PRICE = 300;
        public const int AW_PRICE = 2000;
        public const int AC_PRICE = 1500;   // 自动除草除虫系统
        public const int NPL = 9;
        public const float RIPE_ST = 7f;
        public const float DEAD_ST = 7.5f;

        // 田间告警生死线：有草 / 有虫 / 缺水任一持续 DANGER_SEC 秒未处理，瓜苗即枯萎
        // 计时用真实时间（不受 1x / 2x 倍速影响），徽章上的倒计时与此处共用
        public const float DANGER_SEC = 10f;
        public const float DANGER_WATER = 30f;  // 与「缺水」徽章阈值一致
        public const float DANGER_WARN = 5f;    // 半程日志告警

        // 正式版倍速档位（唯一数据源）：只保留 1x / 2x，界面与逻辑共用
        public static readonly float[] SPEEDS = { 1f, 2f };
        public static readonly string[] SPEED_LABELS = { "1x", "2x" };

        public static float NormalizeSpeed(float s) { return s > 1.01f ? 2f : 1f; }

        public static readonly Variety[] V =
        {
            new Variety("jx", "京欣1号", 20, 48, 1.0f, 4.5f, 11.5f,
                "经典中熟 · 花皮桃红瓤 · 皮薄而韧耐储运",
                "中熟 · 全生育期约88天 · 单瓜4—5公斤 · 中心糖度11度左右 · 花皮、桃红瓤",
                "「京」代表北京，「欣」取欣欣向荣之意。皮薄而韧、品质稳定，上世纪八九十年代起长期是大兴瓜区主栽品种；2007年「大兴西瓜」获批国家地理标志产品，京欣系正是招牌。",
                "挑京欣看三点：墨绿蛇纹清晰展开、瓜脐凹陷、卷须枯黄——熟瓜敲起来「嘭嘭」发闷。"),
            new Variety("hx", "航兴1号", 45, 88, 1.3f, 5.0f, 12.5f,
                "航天育种 · 高糖脆甜 · 生长慢但价值高",
                "中晚熟 · 全生育期约92天 · 单瓜5公斤上下 · 中心糖度12度以上 · 红瓤脆甜",
                "种子曾随返回式航天器遨游太空，在微重力与宇宙辐射下诱变，回地面经多代选育稳定成型。「航天育种+瓜乡」，庞各庄瓜田里长出了「太空西瓜」。",
                "航天育种不是「天上长瓜」：太空只负责诱发变异，选育仍靠地面一代代种瓜人的眼睛与耐心。"),
            new Variety("jxiu", "京秀", 30, 66, 0.72f, 1.8f, 12.8f,
                "早熟礼品瓜 · 长得最快 · 皮薄怕裂",
                "早熟 · 全生育期约78天 · 单瓜1.5—2公斤 · 中心糖度12—13度 · 红瓤细脆",
                "庞各庄调整种植结构的「轻骑兵」：个小、早熟、糖度高，一只手能托起，最适合瓜节观光采摘与精品礼盒。皮极薄，运输要用泡沫套袋。",
                "小型瓜皮薄，膨瓜水要少量多次：临近成熟突然大水，极易「啪」地裂开。")
        };

        public static readonly Stage[] ST =
        {
            new Stage("种子", 8, 15),
            new Stage("发芽", 20, 20),
            new Stage("幼苗", 30, 26),
            new Stage("伸蔓", 40, 32),
            new Stage("开花", 25, 34),
            new Stage("坐果", 35, 28),
            new Stage("转熟", 30, 36),
            new Stage("成熟", 0, 46)
        };

        // 各阶段每秒水分消耗
        public static readonly float[] DECAY = { 0.5f, 0.5f, 0.7f, 0.9f, 1.0f, 1.4f, 0.9f, 0.4f };

        public static readonly string[] FACTS =
        {
            "庞各庄位于永定河冲积平原，沙壤土透气导热快，春天地温回升早，是西瓜的天然「床垫」。",
            "土壤富含硼、锰、镁、钾，速效钾150—200 mg/kg——钾是瓜变甜的关键元素。",
            "生长季昼夜温差10℃以上：白天光合攒糖，晚上低温少耗——糖分就是这么攒下的。",
            "「大兴西瓜」2007年获国家地理标志保护，核心保护区在南李渠、东义堂、北顿垡一带。",
            "西瓜雌雄同株异花：雌花下面带小「瓜妞」，授粉窗口只有开花当天上午7:30—10:30。",
            "三蔓整枝：留主蔓+2条壮侧蔓，养分集中，瓜才周正。",
            "一株一瓜是高产规程：第2—3朵雌花结的瓜发育最充分。",
            "采前一周控水，糖度可提升1—2度——但临近成熟突然大水会把瓜「撑裂」。",
            "判断成熟「三看一听」：卷须枯黄、纹路清晰、瓜脐凹陷、拍声发闷。",
            "航兴系列西瓜种子上过太空：空间诱变+地面选育=航天育种。"
        };

        public static Variety GetVariety(string id)
        {
            for (int i = 0; i < V.Length; i++) if (V[i].id == id) return V[i];
            return null;
        }

        public static int TotalGrowSeconds(Variety v, bool upgraded)
        {
            int sum = 0;
            for (int i = 0; i < ST.Length; i++) sum += ST[i].dur;
            return Mathf.RoundToInt(sum * v.dur / (upgraded ? 3f : 1f));
        }

        public static string TierName(int q)
        {
            if (q >= 85) return "瓜王金卡";
            if (q >= 70) return "金牌卡";
            if (q >= 55) return "银牌卡";
            return "铜牌卡";
        }

        public static string TierStars(int q)
        {
            if (q >= 85) return "★★★★★";
            if (q >= 70) return "★★★★☆";
            if (q >= 55) return "★★★☆☆";
            return "★★☆☆☆";
        }

        public static string TierHex(int q)
        {
            if (q >= 85) return "#B45309";
            if (q >= 70) return "#9F1239";
            if (q >= 55) return "#475569";
            return "#57534E";
        }
    }

    // ══════════ 存档数据结构（对应网页版 localStorage 中的 P） ══════════
    [Serializable]
    public sealed class PlotData
    {
        public int i;
        public string v = "";        // 品种 id，空 = 未种植
        public float st;             // 阶段，7 = 成熟，7.5 = 枯萎
        public float prog;
        public float water;
        public bool weed;
        public int wAge;
        public string pest = "";     // 空 = 无虫害
        public int pAge;
        public bool pol;             // 是否已授粉
        public int thin = -1;        // -1 = 未选择, 0 = 否, 1 = 是
        public bool askThin;
        public bool askDry;
        public bool dry;             // 采前控水
        public bool crack;
        public bool ferted;
        public bool upgraded;
        public float care;
        public float stress;
        public int ticks;
        public int wilt;
        public float danger;         // 田间告警已持续的真实秒数（0 = 安全）
        public string dangerCause = "";  // 进入告警时的主要原因，用于遗憾提示文案

        public static PlotData NewPlot(int i)
        {
            PlotData p = new PlotData();
            p.i = i; p.v = ""; p.st = 0; p.prog = 0; p.water = 55;
            p.weed = false; p.wAge = 0; p.pest = ""; p.pAge = 0;
            p.pol = false; p.thin = -1; p.askThin = false; p.askDry = false;
            p.dry = false; p.care = 60; p.stress = 0; p.ticks = 0;
            p.wilt = 0; p.crack = false; p.ferted = false; p.upgraded = false;
            p.danger = 0f; p.dangerCause = "";
            return p;
        }
    }

    [Serializable] public sealed class WarehouseItem
    {
        public string v; public int q; public float wt; public float brix; public int coins;
    }

    [Serializable] public sealed class CardEntry
    {
        public string key; public int q; public string tier; public string brix; public string wt;
    }

    [Serializable]
    public sealed class SaveData
    {
        public int coins = 100;
        public float exp = 0;
        public int lv = 1;
        public int plotsOwned = 4;
        public List<PlotData> plots = new List<PlotData>();
        public List<CardEntry> cards = new List<CardEntry>();
        public List<WarehouseItem> wh = new List<WarehouseItem>();
        public List<string> tips = new List<string>();
        public string tool = "water";
        public float speed = 1;
        public int fert = 0;
        public bool autoWater = false;
        public bool autoClean = false;   // 自动除草除虫系统（解锁「一键除草除虫」）
        public string t = "0";   // 上次保存的 Unix 毫秒（字符串，兼容 JsonUtility）

        public static SaveData NewGame()
        {
            SaveData p = new SaveData();
            for (int i = 0; i < FarmData.NPL; i++) p.plots.Add(PlotData.NewPlot(i));
            return p;
        }

        public CardEntry GetCard(string id)
        {
            for (int i = 0; i < cards.Count; i++) if (cards[i].key == id) return cards[i];
            return null;
        }

        public bool HasTip(string key) { return tips.Contains(key); }
    }
}
