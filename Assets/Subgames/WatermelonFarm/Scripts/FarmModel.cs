using System;
using System.Collections.Generic;
using UnityEngine;

namespace WatermelonFarm
{
    // ══════════ 弹窗描述（与网页版 M._show 对应） ══════════
    public sealed class ModalButton
    {
        public string label; public bool primary; public Action onClick;
        public ModalButton(string label, bool primary, Action onClick)
        { this.label = label; this.primary = primary; this.onClick = onClick; }
    }

    public sealed class ModalRow
    {
        public string text; public string button; public Action onClick;
        // 商城「醒目购买卡」专用字段；普通弹窗（仓库等）不使用
        public string title = "";    // 商品名（大字）
        public string desc = "";     // 一句话说明：作用 / 买完怎么用
        public string price = "";    // 价格大字
        public string tag = "";      // 效果徽章（解锁了什么）
        public bool owned = false;   // 已拥有
        public bool afford = true;   // 当前西瓜币是否够
        public ModalRow(string text, string button, Action onClick)
        { this.text = text; this.button = button; this.onClick = onClick; }
        public ModalRow(string text, string button, Action onClick,
            string title, string desc, string price, string tag, bool owned, bool afford)
            : this(text, button, onClick)
        {
            this.title = title; this.desc = desc; this.price = price;
            this.tag = tag; this.owned = owned; this.afford = afford;
        }
    }

    public sealed class ModalOption
    {
        public string title; public string sub; public Action onClick;
        public ModalOption(string title, string sub, Action onClick)
        { this.title = title; this.sub = sub; this.onClick = onClick; }
    }

    // 图鉴收藏卡：结构化字段，UI 分区排版，避免长文本挤在一块导致溢出
    public sealed class BookCardInfo
    {
        public string name;
        public string vid;              // 品种 id：对应插画与网络实物照片
        public bool got;
        public int no;                  // 收藏编号 01 / 02 / 03
        public string desc = "";        // 品种一句话
        public string file = "";        // 品种档案（短）
        public string tier = "";        // 品级
        public string stars = "";       // 星级
        public string tierHex = "#B45309";
        public string quality = "--";   // 最佳品质分
        public string brix = "--";      // 糖度
        public string wt = "--";        // 单瓜重
        public string coins = "--";     // 最佳售价
    }

    public sealed class CardResult
    {
        public string tierHex; public string tierName; public string stars;
        public int q; public string brix; public string wt; public int coins;
    }

    public sealed class ModalSpec
    {
        public string title = "";
        public string body = "";
        public string tip = "";
        public string artVid = "";       // 非空则在正文右侧展示该品种的成熟瓜插画（收获科普卡）
        public string photoVid = "";     // 非空则在收获卡右侧附上该品种的网络实物照片
        public bool shopMode = false;    // 商城：启用醒目购买卡片，并把提示放在购买区下方
        public string shopHeader = "";   // 购买分区主标题（写明购买方式）
        public string shopSub = "";      // 购买分区副标题（不在本页下单的购买方式）
        public Color bodyBack = Color.white;
        public Color bodyText = new Color(0.22f, 0.27f, 0.33f, 1f);
        public List<ModalRow> rows = new List<ModalRow>();
        public List<ModalOption> options = new List<ModalOption>();
        public List<ModalButton> buttons = new List<ModalButton>();
        public List<BookCardInfo> book = new List<BookCardInfo>();
    }

    public interface IFarmView
    {
        void Render();
        void AddLog(string type, string text);
        void ShowModal(ModalSpec spec);
        void CloseTopModal();
        // 获得西瓜币后的轻量提示：点击任意处或 2 秒后自动消失
        void ShowShardToast(int gained, int total);
        // 瓜苗因告警超时枯萎后的遗憾提示：点击任意处或 4 秒后自动消失
        void ShowWiltToast(string variety, string cause);
    }

    // ══════════ 游戏逻辑：网页版 M / tickOnce / advStage / harvest 等的 1:1 移植 ══════════
    public sealed class FarmModel
    {
        public const string SAVE_KEY = "pgz_farm_unity_v1";

        public IFarmView view;
        public SaveData P;
        public float acc;

        // 统一「西瓜币」余额：唯一数据源；SaveData.coins 仅作为旧存档兼容字段保留。
        int Balance { get { return WatermelonCurrencyWallet.GetBalance(); } }

        bool awTip = false;
        bool awDryTip = false;

        static long NowMs()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }

        // JS 的 Math.round（正数时等同于 floor(x+0.5)）
        static int JsRound(float x) { return Mathf.FloorToInt(x + 0.5f); }
        public static float Clamp(float x, float a, float b) { return Mathf.Clamp(x, a, b); }

        public void Init()
        {
            P = null;
            try
            {
                string s = PlayerPrefs.GetString(SAVE_KEY, "");
                if (!string.IsNullOrEmpty(s)) P = JsonUtility.FromJson<SaveData>(s);
            }
            catch (Exception e) { Debug.LogWarning("存档读取失败：" + e.Message); }

            if (P == null)
            {
                P = SaveData.NewGame();
                Log("sys", "欢迎来到瓜田农场！初始4块耕地；灰色地块花西瓜币开垦，地块右上角可花300金升级（生长×3）。");
                Log("tip", "商城有：化肥（加速+提价）、自动浇水（解锁一键浇水）、自动除草除虫（解锁一键除草除虫）。工具栏可切换 1x / 2x 倍速；地块出现草/虫/缺水徽章时，10 秒内要处理掉，否则瓜苗会枯萎。");
            }
            else
            {
                Normalize();
                OfflineProgress();
            }
            Changed();
        }

        void Normalize()
        {
            if (P.plots == null) P.plots = new List<PlotData>();
            if (P.cards == null) P.cards = new List<CardEntry>();
            if (P.wh == null) P.wh = new List<WarehouseItem>();
            if (P.tips == null) P.tips = new List<string>();
            if (P.plotsOwned < 4) P.plotsOwned = 4;
            if (P.plotsOwned > FarmData.NPL) P.plotsOwned = FarmData.NPL;
            // 旧存档里可能有 1.5x / 3x，正式版统一收敛为 1x / 2x
            P.speed = FarmData.NormalizeSpeed(P.speed);
            if (P.lv < 1) P.lv = 1;
            if (string.IsNullOrEmpty(P.tool)) P.tool = "water";
            while (P.plots.Count < FarmData.NPL) P.plots.Add(PlotData.NewPlot(P.plots.Count));
            for (int i = 0; i < P.plots.Count; i++)
            {
                if (P.plots[i] == null) P.plots[i] = PlotData.NewPlot(i);
                if (P.plots[i].v == null) P.plots[i].v = "";
                if (P.plots[i].pest == null) P.plots[i].pest = "";
                if (P.plots[i].dangerCause == null) P.plots[i].dangerCause = "";
                if (P.plots[i].thin < -1 || P.plots[i].thin > 1) P.plots[i].thin = -1;
                // 离线期间不累计「告警未处理」时间，重新进入游戏从 0 开始计时，
                // 避免长时间关掉游戏后一回来整片瓜田瞬间枯死
                P.plots[i].danger = 0f;
                P.plots[i].dangerCause = "";
            }
        }

        // 离线生长：与网页版 load() 一致，最多结算 8 小时
        void OfflineProgress()
        {
            long saved;
            if (!long.TryParse(P.t, out saved) || saved <= 0) return;
            float dt = Mathf.Min((float)(NowMs() - saved) / 1000f, 8f * 3600f);
            if (dt <= 0) return;

            for (int k = 0; k < P.plots.Count; k++)
            {
                PlotData pl = P.plots[k];
                if (string.IsNullOrEmpty(pl.v)) continue;
                Variety v = FarmData.GetVariety(pl.v);
                if (v == null) continue;
                float left = dt;
                int guard = 0;
                while (left > 0 && pl.st < FarmData.RIPE_ST && guard++ < 10000)
                {
                    float sd = FarmData.ST[(int)pl.st].dur * v.dur;
                    float spd = (pl.weed ? 0.55f : 1f) * (pl.upgraded ? 3f : 1f);
                    float need = (sd - pl.prog) / spd;
                    float use = Mathf.Min(left, Mathf.Max(need, 0f));
                    pl.prog += use * spd;
                    pl.water = Clamp(pl.water - use * FarmData.DECAY[(int)pl.st], 8f, 100f);
                    left -= use;
                    if (pl.prog >= sd - 0.0001f) AdvanceStage(pl, true);
                    else break;
                }
                if (UnityEngine.Random.value < 0.5f) pl.weed = true;
                if (UnityEngine.Random.value < 0.35f) pl.pest = UnityEngine.Random.value < 0.5f ? "蚜虫" : "红蜘蛛";
                if (pl.st == FarmData.RIPE_ST) pl.prog = 0;
            }
        }

        public void Save(bool flush = false)
        {
            try
            {
                P.t = NowMs().ToString();
                PlayerPrefs.SetString(SAVE_KEY, JsonUtility.ToJson(P));
                if (flush) PlayerPrefs.Save();
            }
            catch (Exception e) { Debug.LogWarning("存档写入失败：" + e.Message); }
        }

        public void Changed()
        {
            // 玩家做了任一操作后，立刻解除已处理告警的倒计时（不等下一帧）
            ResetDanger();
            // 化肥用尽时自动切回水壶（对应网页版 render() 中的工具回退）
            if (P.fert <= 0 && P.tool == "fert") P.tool = "water";
            view.Render();
            Save();
        }
        void RenderOnly() { view.Render(); }
        public void Log(string type, string text) { view.AddLog(type, text); }

        // ══════════ 倍速 ══════════
        public void SetSpeed(float s)
        {
            float v = FarmData.NormalizeSpeed(s);   // 只接受 1x / 2x，任何旧调用都被归一化
            if (Mathf.Abs(P.speed - v) < 0.01f) return;
            P.speed = v;
            Log("sys", "游戏倍速切换为 " + v + "x");
            Changed();
        }

        // ══════════ 每秒一 tick 的主循环 ══════════
        public void TickOnce()
        {
            for (int k = 0; k < P.plots.Count; k++)
            {
                PlotData pl = P.plots[k];
                if (string.IsNullOrEmpty(pl.v) || pl.st >= FarmData.RIPE_ST) continue;
                Variety v = FarmData.GetVariety(pl.v);
                if (v == null) continue;
                pl.ticks++;

                // 自动浇水
                if (P.autoWater)
                {
                    if (pl.dry)
                    {
                        if (!awDryTip)
                        {
                            awDryTip = true;
                            Log("sys", "自动浇水检测到「采前控水」地块，已智能跳过（防止裂果稀释糖分）。");
                        }
                    }
                    else if (pl.water < 32)
                    {
                        pl.water = Clamp(pl.water + 35, 0, 100);
                        if (!awTip)
                        {
                            awTip = true;
                            Log("sys", "自动浇水系统工作中：湿度低于32%自动补水，无需手动操作。");
                        }
                    }
                }

                float spd = (pl.weed ? 0.55f : 1f) * (string.IsNullOrEmpty(pl.pest) ? 1f : 0.6f)
                          * (pl.water <= 8 ? 0f : 1f) * (pl.upgraded ? 3f : 1f);
                pl.prog += spd;

                float dc = FarmData.DECAY[(int)pl.st] * (pl.dry ? 1.6f : 1f);
                pl.water = Clamp(pl.water - dc, 0, 100);
                if (pl.water <= 0)
                {
                    pl.wilt++;
                    pl.care = Mathf.Max(0, pl.care - 0.18f);
                    if (pl.wilt > 28)
                    {
                        pl.st = FarmData.DEAD_ST;
                        Log("bad", "一株" + v.name + "因长期缺水枯萎了……点击清理后可补种。（浇水要「见干见湿」）");
                        continue;
                    }
                }
                else pl.wilt = 0;

                if (pl.water < 25 || pl.water > 88) pl.stress++;
                if (!pl.weed && UnityEngine.Random.value < 0.006f) pl.weed = true;
                if (string.IsNullOrEmpty(pl.pest) &&
                    UnityEngine.Random.value < (pl.water > 85 ? 0.012f : 0.005f))
                    pl.pest = UnityEngine.Random.value < 0.5f ? "蚜虫" : "红蜘蛛";

                pl.wAge = pl.weed ? pl.wAge + 1 : 0;
                pl.pAge = !string.IsNullOrEmpty(pl.pest) ? pl.pAge + 1 : 0;

                float sd = FarmData.ST[(int)pl.st].dur * v.dur;
                if (pl.prog >= sd) AdvanceStage(pl, false);

                if (pl.st >= 6 && pl.water > 92 && UnityEngine.Random.value < 0.02f && !pl.crack)
                {
                    pl.crack = true;
                    pl.care -= 22;
                    Log("bad", "裂瓜！临近成熟还在大水漫灌——这就是「采前控水」的道理。");
                }
            }
        }

        // ══════════ 田间告警 10 秒生死线（真实时间，不受 1x / 2x 倍速影响） ══════════
        // 判定条件与 UI 的「有草 / 蚜虫·红蜘蛛 / 缺水」徽章完全同源
        public static string DangerCause(PlotData pl)
        {
            if (pl == null || string.IsNullOrEmpty(pl.v) || pl.st >= FarmData.RIPE_ST) return "";
            if (pl.water < FarmData.DANGER_WATER) return "缺水";
            if (pl.weed) return "有草";
            if (!string.IsNullOrEmpty(pl.pest)) return pl.pest;
            return "";
        }

        // 把当前全部告警拼成一句，用于遗憾提示与半程日志
        public static string DangerText(PlotData pl)
        {
            List<string> a = new List<string>();
            if (pl.water < FarmData.DANGER_WATER) a.Add("缺水");
            if (pl.weed) a.Add("有草");
            if (!string.IsNullOrEmpty(pl.pest)) a.Add(pl.pest + "虫害");
            return a.Count == 0 ? "田间告警" : string.Join("、", a.ToArray());
        }

        // 玩家做了任一操作后，立即解除已处理告警的倒计时，不等下一帧
        void ResetDanger()
        {
            if (P == null || P.plots == null) return;
            for (int i = 0; i < P.plots.Count; i++)
            {
                PlotData pl = P.plots[i];
                if (pl == null || pl.danger <= 0f) continue;
                if (string.IsNullOrEmpty(DangerCause(pl)))
                {
                    pl.danger = 0f;
                    pl.dangerCause = "";
                }
            }
        }

        // 由 FarmGame.Update 每帧以 unscaledDeltaTime 驱动
        public void TickDanger(float dt)
        {
            if (P == null || P.plots == null || dt <= 0f) return;
            bool changed = false;
            int wiltCount = 0;
            string lastName = "", lastCause = "";
            for (int i = 0; i < P.plots.Count; i++)
            {
                PlotData pl = P.plots[i];
                if (pl == null) continue;
                // 空地 / 已成熟 / 已枯萎都不参与倒计时
                if (string.IsNullOrEmpty(pl.v) || pl.st >= FarmData.RIPE_ST)
                {
                    if (pl.danger > 0f) { pl.danger = 0f; pl.dangerCause = ""; changed = true; }
                    continue;
                }
                string cause = DangerCause(pl);
                if (string.IsNullOrEmpty(cause))
                {
                    if (pl.danger > 0f) { pl.danger = 0f; pl.dangerCause = ""; changed = true; }
                    continue;
                }
                float before = pl.danger;
                if (before <= 0f) pl.dangerCause = DangerText(pl);
                pl.danger = Mathf.Min(FarmData.DANGER_SEC, before + dt);
                if (before < FarmData.DANGER_WARN && pl.danger >= FarmData.DANGER_WARN)
                    Log("warn", "第" + (pl.i + 1) + "号地" + DangerText(pl) + "已持续 " +
                        FarmData.DANGER_WARN.ToString("F0") + " 秒！只剩 " +
                        (FarmData.DANGER_SEC - FarmData.DANGER_WARN).ToString("F0") +
                        " 秒，快用对应工具处理，否则瓜苗会枯死。");
                if (pl.danger >= FarmData.DANGER_SEC - 0.0001f)
                {
                    Variety v = FarmData.GetVariety(pl.v);
                    lastName = v != null ? v.name : "西瓜";
                    lastCause = string.IsNullOrEmpty(pl.dangerCause) ? "田间告警" : pl.dangerCause;
                    pl.st = FarmData.DEAD_ST;
                    pl.prog = 0f;
                    pl.danger = 0f;
                    pl.dangerCause = "";
                    Log("bad", "第" + (pl.i + 1) + "号地的" + lastName + "因" + lastCause +
                        "持续 " + FarmData.DANGER_SEC.ToString("F0") +
                        " 秒未处理而枯萎了……点击「清理枯苗」后可补种。");
                    wiltCount++;
                    changed = true;
                    continue;
                }
                // 徽章上的倒计时按整秒变化时才重绘
                if (Mathf.CeilToInt(FarmData.DANGER_SEC - before) !=
                    Mathf.CeilToInt(FarmData.DANGER_SEC - pl.danger)) changed = true;
            }
            if (changed) { view.Render(); Save(); }
            // 多株同时枯萎合并成一条遗憾提示，避免连续弹窗互相覆盖
            if (wiltCount == 1) view.ShowWiltToast(lastName, lastCause);
            else if (wiltCount > 1) view.ShowWiltToast(wiltCount + " 株西瓜", lastCause);
        }

        public void AdvanceStage(PlotData pl, bool offline)
        {
            pl.prog = 0;
            if (pl.st == 4f)
            {
                if (!pl.pol)
                {
                    if (offline || UnityEngine.Random.value < 0.35f)
                    {
                        pl.pol = true; pl.care -= 4; pl.st = 5;
                        if (!offline) Log("warn", "幸好有蜜蜂偶然访花，第" + (pl.i + 1) + "号地的花勉强坐住了（坐果率不高）。");
                    }
                    else
                    {
                        pl.st = 3; pl.care -= 8;
                        if (!offline) Log("bad", "开花没授粉→「化瓜」，退回伸蔓期重新来。原理：雌花只开一天上午，错过就脱落。");
                        return;
                    }
                }
                else pl.st = 5;
                if (!pl.askThin && !offline) { pl.askThin = true; ThinDialog(pl); return; }
            }
            else if (pl.st == 5f)
            {
                if (pl.thin < 0) { pl.thin = 0; pl.care -= 6; }
                pl.st = 6;
                if (!pl.askDry && !offline) { pl.askDry = true; DryDialog(pl); return; }
            }
            else if (pl.st == 6f)
            {
                pl.st = 7; pl.prog = 0;
                if (!offline)
                {
                    Variety vv = FarmData.GetVariety(pl.v);
                    Log("sys", "第" + (pl.i + 1) + "号地的" + (vv != null ? vv.name : "西瓜") + "成熟了！点击收获。");
                }
            }
            else pl.st += 1;
        }

        // ══════════ 玩家操作 ══════════
        public void SetTool(string t) { P.tool = t; Changed(); }
        public void SetToolQuick(string t) { P.tool = t; RenderOnly(); }

        public void ClickPlot(int i)
        {
            PlotData pl = P.plots[i];
            if (i >= P.plotsOwned) { BuyPlotDialog(i); return; }
            if (string.IsNullOrEmpty(pl.v)) { SeedsDialog(i); return; }
            if (pl.st == FarmData.DEAD_ST)
            {
                bool up = pl.upgraded;
                PlotData np = PlotData.NewPlot(i); np.upgraded = up;
                P.plots[i] = np;
                Changed(); return;
            }
            if (pl.st == FarmData.RIPE_ST) { Harvest(P.plots[i]); return; }

            if (P.tool == "water") DoWater(pl);
            else if (P.tool == "fert") FertApply(pl);
            else if (P.tool == "hoe")
            {
                if (pl.weed)
                {
                    pl.weed = false;
                    if (pl.wAge > 40) { pl.care -= 4; Log("warn", "草除晚了，已与瓜苗争水争肥。"); }
                    else { pl.care = Clamp(pl.care + 3, 0, 100); Log("tip", "除草完成。原理：杂草争水争肥还招虫。"); }
                }
                else Log("sys", "这块地没有草。");
            }
            else if (P.tool == "spray")
            {
                if (!string.IsNullOrEmpty(pl.pest))
                {
                    pl.pest = "";
                    if (pl.pAge > 30) { pl.care -= 5; Log("warn", "虫害拖久了，叶片已被啃食。"); }
                    else { pl.care = Clamp(pl.care + 4, 0, 100); Log("tip", "虫害解除。庞各庄推行绿色防控：黄板诱蚜、生物农药。"); }
                }
                else Log("sys", "这块地没有虫。");
            }
            Changed();
        }

        // 施肥：立即进入下一阶段，售价 +25，每株限 1 次
        public void FertApply(PlotData pl)
        {
            if (P.fert <= 0)
            {
                Log("sys", "没有化肥了，去商城购买（" + FarmData.FERT_PRICE + "金/袋）。");
                SetToolQuick("water"); RenderOnly(); return;
            }
            if (pl.ferted)
            {
                Log("warn", "这株瓜已经施过化肥了——一株只能施一次。（施肥过多反而烧根，真实种植同理）");
                return;
            }
            P.fert--;
            pl.ferted = true;
            Variety v = FarmData.GetVariety(pl.v);
            string from = FarmData.ST[(int)pl.st].name;
            if (pl.st == 4f && !pl.pol) pl.pol = true;
            if (pl.st == 4f) { pl.st = 5; if (!pl.askThin) { pl.askThin = true; ThinDialog(pl); } }
            else if (pl.st == 5f) { pl.st = 6; if (!pl.askDry) { pl.askDry = true; DryDialog(pl); } }
            else if (pl.st < 7)
            {
                pl.st++;
                if (pl.st == 7) Log("sys", "第" + (pl.i + 1) + "号地的" + v.name + "提前成熟了！");
            }
            Log("tip", "化肥奏效！第" + (pl.i + 1) + "号地" + v.name + "从「" + from +
                "」冲刺到「" + FarmData.ST[(int)pl.st].name + "」。（收获时售价+" + FarmData.FERT_BONUS + "金）");
            if (P.fert == 0) P.tool = "water";
            Changed();
        }

        public void DoWater(PlotData pl)
        {
            if (pl.dry)
            {
                pl.water = Clamp(pl.water + 35, 0, 100); pl.care -= 3;
                Log("warn", "已开启「采前控水」还浇水：糖分被稀释，裂果风险上升。");
                Changed(); return;
            }
            pl.water = Clamp(pl.water + 35, 0, 100);
            if (pl.water > 88 && pl.st >= 6) Log("warn", "水浇太足了！转熟期过湿易裂果。");
            else Log("sys", "浇水完成。需水规律：苗期少、膨瓜期足、成熟期控。");
            Changed();
        }

        // ══════════ 收获：品质 / 重量 / 糖度 / 售价 / 卡片 ══════════
        public void Harvest(PlotData pl)
        {
            Variety v = FarmData.GetVariety(pl.v);
            if (v == null) return;
            float q = pl.care - (pl.stress / Mathf.Max(1, pl.ticks)) * 30f
                    + (pl.dry ? 6 : 0) + (pl.thin == 1 ? 2 : 0);
            if (!pl.pol) q -= 5;
            if (pl.crack) q -= 20;
            int qi = Mathf.Clamp(JsRound(q), 0, 100);
            float wt = v.tw * (0.55f + qi / 100f * 0.55f) * (pl.crack ? 0.7f : 1f);
            float brix = Clamp(v.brix + (qi - 60) / 40f * 1.8f + (pl.dry ? 0.8f : 0f), 7f, 14.5f);
            int coins = JsRound(v.basePrice * (0.5f + qi / 100f)) + (pl.ferted ? FarmData.FERT_BONUS : 0);
            int exp = JsRound(8f + qi / 12f);
            string tierName = FarmData.TierName(qi);

            WarehouseItem w = new WarehouseItem();
            w.v = pl.v; w.q = qi; w.wt = (float)Math.Round(wt, 2);
            w.brix = (float)Math.Round(brix, 1); w.coins = coins;
            P.wh.Add(w);
            GainExp(exp);

            CardEntry old = P.GetCard(v.id);
            if (old == null || qi > old.q)
            {
                if (old != null) P.cards.Remove(old);
                CardEntry ce = new CardEntry();
                ce.key = v.id; ce.q = qi; ce.tier = tierName;
                ce.brix = brix.ToString("0.0"); ce.wt = wt.ToString("0.00");
                P.cards.Add(ce);
                Log("sys", "图鉴更新：" + v.name + " · " + tierName);
            }
            Log("tip", "收获" + v.name + "：品质" + qi + " · " + wt.ToString("0.00") + "kg · 糖度约" +
                brix.ToString("0.0") + "° → " + tierName + "（入仓库）" +
                (pl.ferted ? " · 含化肥加成+" + FarmData.FERT_BONUS + "金" : ""));

            CardResult r = new CardResult();
            r.tierHex = FarmData.TierHex(qi); r.tierName = tierName; r.stars = FarmData.TierStars(qi);
            r.q = qi; r.brix = brix.ToString("0.0"); r.wt = wt.ToString("0.00"); r.coins = coins;
            ShowCard(v, r);

            bool up = pl.upgraded;
            PlotData np = PlotData.NewPlot(pl.i); np.upgraded = up;
            P.plots[pl.i] = np;
            Changed();
        }

        public void GainExp(int n)
        {
            P.exp += n;
            int need = 30 + P.lv * 30;
            while (P.exp >= need)
            {
                P.exp -= need; P.lv++;
                int bonus = 20 + P.lv * 10;
                WatermelonCurrencyWallet.Credit(bonus, "farm_level_bonus");
                Log("sys", "升级！Lv." + P.lv + "（奖励" + bonus + "金。注意：等级越高，买新地越贵+10/级）");
                need = 30 + P.lv * 30;
            }
            RenderOnly();
        }

        public int PlotPrice()
        {
            int bought = P.plotsOwned - 4;
            return 100 + 50 * (bought + 1) + 10 * (P.lv - 1);
        }

        // ══════════ 弹窗：商城 / 买地 / 升级 / 种子 ══════════
        void Msg(string title, string body)
        {
            ModalSpec m = new ModalSpec();
            m.title = title; m.body = body;
            m.buttons.Add(new ModalButton("知道啦", true, null));
            view.ShowModal(m);
        }

        // 未购买自动化系统时点击一键按钮：明确告知去哪购买，并给出直达商城的按钮
        void LockedPrompt(string title, string system, int price, string feature)
        {
            ModalSpec m = new ModalSpec();
            m.title = title;
            m.body = "<b>请前往商城购买" + system + "</b>\n\n" +
                "「" + feature + "」目前未解锁，购买后按钮会由灰色变为可用状态。\n\n" +
                system + "售价 <b>" + price + " 金</b>，一次购买永久生效（当前余额 " + Balance + " 金）。";
            m.tip = "点击下方「前往商城」即可直接打开瓜田商城，找到对应的系统购买。";
            m.buttons.Add(new ModalButton("前往商城", true, ShopDialog));
            m.buttons.Add(new ModalButton("知道啦", false, null));
            view.ShowModal(m);
        }

        public void ShopDialog()
        {
            ModalSpec m = new ModalSpec();
            m.title = "瓜田商城 · 余额 " + Balance + " 金";
            m.shopMode = true;
            m.shopHeader = "购买方式 · 点右侧绿色「立即购买」按钮";
            m.shopSub = "土地升级：点地块右上角「升级」按钮（" + FarmData.UPG_PRICE + "金/块，生长速度 ×3）";
            m.body =
                "<b>本页三件商品：</b>化肥 " + FarmData.FERT_PRICE + "金/袋 · 自动浇水 " +
                FarmData.AW_PRICE + "金 · 自动除草除虫 " + FarmData.AC_PRICE + "金。" +
                "下方卡片标好价格与解锁效果，<b>点右侧绿色按钮直接买</b>。\n\n" +
                "西瓜币不足时按钮显示「西瓜币不足」；自动化系统<b>一次购买，永久生效</b>。" +
                "已升级 " + UpgradedCount() + " 块地。";
            // 商城的补充说明由 UI 放到「购买区下方」，不再压在购买按钮上面
            m.tip = "买种子点空地块选种；买新地点击灰色未开垦地块。未购买自动化系统前，" +
                "对应的一键按钮为灰色，点击会提示前往商城购买。";
            int coins = Balance;
            m.rows.Add(new ModalRow(
                "库存：" + P.fert + " 袋", "买 1 袋化肥 " + FarmData.FERT_PRICE + "金", BuyFert,
                "高效化肥", "选中「施肥」工具后点击地块：立即推进一个生长阶段，收获售价 +" +
                    FarmData.FERT_BONUS + "金",
                FarmData.FERT_PRICE + " 金", "即时入库 · 一株限用一次", false,
                coins >= FarmData.FERT_PRICE));
            m.rows.Add(new ModalRow(
                P.autoWater ? "全农场已覆盖 · 永久生效" : "覆盖全部已解锁耕地",
                P.autoWater ? "已拥有" : "购买自动浇水 " + FarmData.AW_PRICE + "金", BuyAutoWater,
                "自动浇水系统", "湿度低于32%自动补水；采前控水地块智能跳过，防止裂果",
                FarmData.AW_PRICE + " 金", "解锁「一键浇水」", P.autoWater,
                coins >= FarmData.AW_PRICE));
            m.rows.Add(new ModalRow(
                P.autoClean ? "全场监测已开通 · 永久生效" : "一键清除杂草与虫害",
                P.autoClean ? "已拥有" : "购买自动除虫 " + FarmData.AC_PRICE + "金", BuyAutoClean,
                "自动除草除虫系统", "虫情草情监测＋机械臂清理，一键清除全场杂草与蚜虫/红蜘蛛",
                FarmData.AC_PRICE + " 金", "解锁「一键除草除虫」", P.autoClean,
                coins >= FarmData.AC_PRICE));
            m.buttons.Add(new ModalButton("关闭 · 返回农场", true, null));
            view.ShowModal(m);
        }

        int UpgradedCount()
        {
            int n = 0;
            for (int i = 0; i < P.plots.Count; i++) if (P.plots[i].upgraded) n++;
            return n;
        }

        public void BuyFert()
        {
            if (Balance < FarmData.FERT_PRICE)
            {
                Msg("西瓜币不足", "买 1 袋化肥需要 " + FarmData.FERT_PRICE + " 金，当前余额 " + Balance +
                    " 金。先去仓库卖瓜换钱吧！"); return;
            }
            if (!WatermelonCurrencyWallet.TrySpend(FarmData.FERT_PRICE, "farm_buy_fert")) return;
            P.fert++;
            Log("sys", "购入化肥×1（-" + FarmData.FERT_PRICE + "金），库存 " + P.fert +
                " 袋。选中「施肥」工具点击地块即可使用。");
            Changed(); ShopDialog();
        }

        public void BuyAutoWater()
        {
            if (P.autoWater) { Msg("已拥有", "自动浇水系统已开通，永久生效中。"); return; }
            if (Balance < FarmData.AW_PRICE)
            {
                Msg("西瓜币不足", "自动浇水系统需要 " + FarmData.AW_PRICE + " 金，当前余额 " + Balance +
                    " 金。多种瓜攒钱吧！"); return;
            }
            if (!WatermelonCurrencyWallet.TrySpend(FarmData.AW_PRICE, "farm_buy_auto_water")) return;
            P.autoWater = true;
            Log("sys", "自动浇水系统开通！全场覆盖，湿度低于32%自动补水（采前控水地块自动跳过），并解锁「一键浇水」。永久生效。");
            Changed(); ShopDialog();
        }

        public void BuyAutoClean()
        {
            if (P.autoClean) { Msg("已拥有", "自动除草除虫系统已开通，永久生效中。"); return; }
            if (Balance < FarmData.AC_PRICE)
            {
                Msg("西瓜币不足", "自动除草除虫系统需要 " + FarmData.AC_PRICE + " 金，当前余额 " + Balance +
                    " 金。多种瓜攒钱吧！"); return;
            }
            if (!WatermelonCurrencyWallet.TrySpend(FarmData.AC_PRICE, "farm_buy_auto_clean")) return;
            P.autoClean = true;
            Log("sys", "自动除草除虫系统开通！全场虫情草情监测已启用，「一键除草除虫」现已可用。永久生效。");
            Changed(); ShopDialog();
        }

        public void UpgradePlotDialog(int i)
        {
            PlotData pl = P.plots[i];
            if (i >= P.plotsOwned || pl.upgraded) return;
            if (Balance < FarmData.UPG_PRICE)
            {
                Msg("西瓜币不足", "升级这块地需要 " + FarmData.UPG_PRICE + " 金，当前余额 " + Balance +
                    " 金。\n升级后该地块生长速度×3、永久生效——攒够钱再来！"); return;
            }
            ModalSpec m = new ModalSpec();
            m.title = "升级第" + (i + 1) + "号地块";
            m.body = "花费 <b>" + FarmData.UPG_PRICE + " 金</b> 对该地块进行土壤改良+水肥一体化滴灌改造，" +
                     "<b>生长速度 ×3</b>，永久生效（当前余额 " + Balance + " 金）。";
            m.tip = "原理：水肥一体化滴灌能按需精准供给水肥，配合改土增施有机肥，作物长势显著加快——现代农业园区的主流做法。";
            int idx = i;
            m.buttons.Add(new ModalButton("花费 " + FarmData.UPG_PRICE + " 金升级", true, delegate
            {
                if (!WatermelonCurrencyWallet.TrySpend(FarmData.UPG_PRICE, "farm_upgrade_plot")) return;
                P.plots[idx].upgraded = true;
                Log("sys", "第" + (idx + 1) + "号地块升级完成！该地块生长速度×3（-" + FarmData.UPG_PRICE + "金）。");
                Changed();
            }));
            m.buttons.Add(new ModalButton("取消 · 返回农场", false, null));
            view.ShowModal(m);
        }

        public void BuyPlotDialog(int i)
        {
            int price = PlotPrice();
            if (P.plotsOwned >= FarmData.NPL)
            {
                Msg("耕地已全部开垦", "9 块地都是你的了，专心种瓜吧！"); return;
            }
            if (Balance < price)
            {
                Msg("西瓜币不足", "购买第" + (i + 1) + "号耕地需要 " + price + " 金，当前只有 " + Balance +
                    " 金。\n先收获西瓜→仓库卖钱，或继续升级拿西瓜币奖励！"); return;
            }
            ModalSpec m = new ModalSpec();
            m.title = "购买耕地 · 第" + (i + 1) + "号地";
            m.body = "开垦这块荒地需要 <b>" + price + " 金</b>（当前余额 " + Balance + " 金）。";
            m.tip = "地价公式：100 + 50×已购块数 + 10×(等级−1)";
            int buyPrice = price, idx = i;
            m.buttons.Add(new ModalButton("花费 " + price + " 金购买", true, delegate
            {
                if (!WatermelonCurrencyWallet.TrySpend(buyPrice, "farm_buy_plot")) return;
                P.plotsOwned++;
                Log("sys", "第" + (idx + 1) + "号耕地开垦成功！（-" + buyPrice + "金）现有耕地 " +
                    P.plotsOwned + "/" + FarmData.NPL);
                Changed();
            }));
            m.buttons.Add(new ModalButton("取消 · 返回农场", false, null));
            view.ShowModal(m);
        }

        public void SeedsDialog(int i)
        {
            ModalSpec m = new ModalSpec();
            m.title = "第" + (i + 1) + "号地 · 选择种子";
            m.body = "选择要种下的品种（余额 " + Balance + " 金）：";
            for (int k = 0; k < FarmData.V.Length; k++)
            {
                Variety v = FarmData.V[k];
                int total = FarmData.TotalGrowSeconds(v, false);
                int upTotal = FarmData.TotalGrowSeconds(v, true);
                string sub = v.desc + "\n生长周期约" + total + "秒" +
                    (P.plots[i].upgraded ? "（升级地×3≈" + upTotal + "秒）" : "") +
                    " · 目标单瓜" + v.tw + "kg · 糖度潜力" + v.brix + "°";
                string vid = v.id; int idx = i;
                m.options.Add(new ModalOption("「" + v.name + "」 " + v.seed + " 金", sub,
                    delegate { BuySeed(vid, idx); }));
            }
            m.tip = "京欣1号是地理标志招牌，航兴1号上过太空，京秀是采摘园宠儿。种得越好，科普卡品级越高！";
            m.buttons.Add(new ModalButton("取消 · 返回农场", false, delegate
            { Log("sys", "已返回农场，这块地仍是空的。"); RenderOnly(); }));
            view.ShowModal(m);
        }

        public void BuySeed(string vid, int i)
        {
            Variety v = FarmData.GetVariety(vid);
            if (v == null) return;
            if (Balance < v.seed)
            {
                Msg("西瓜币不足", "先去仓库卖瓜换钱，或者先种便宜的京欣1号练手。"); return;
            }
            if (!WatermelonCurrencyWallet.TrySpend(v.seed, "farm_buy_seed")) return;
            PlotData np = PlotData.NewPlot(i);
            np.v = vid; np.upgraded = P.plots[i].upgraded;
            P.plots[i] = np;
            Log("sys", "第" + (i + 1) + "号地种下" + v.name + "（-" + v.seed + "金）。");
            Changed();
            First("plant", "种瓜第一课：浸种催芽",
                "真实种瓜在播种前会用 <b>55℃温水烫种15分钟</b> 杀灭种皮病菌，再催芽播种。苗期讲究「见干见湿」，水大易徒长。");
        }

        // ══════════ 农事决策弹窗 ══════════
        public void ThinDialog(PlotData pl)
        {
            ModalSpec m = new ModalSpec();
            m.title = "疏果 · 一株留几个瓜？";
            m.body = "这株瓜坐住了好几个小瓜妞，养分只够养好一个！";
            m.tip = "原理：第2—3朵雌花结的瓜发育最充分；一株一瓜，多余的果断疏掉。";
            PlotData p = pl;
            m.buttons.Add(new ModalButton("留第2瓜（最佳）", true, delegate
            {
                p.thin = 1; p.care = Clamp(p.care + 8, 0, 100);
                Log("tip", "留下第2瓜、疏掉其余——养分集中，瓜形周正。"); Changed();
            }));
            m.buttons.Add(new ModalButton("留第1瓜（离根近）", false, delegate
            {
                p.thin = 0; p.care = Clamp(p.care + 1, 0, 100);
                Log("warn", "留了第1瓜：营养供应一般，瓜可能偏小。"); Changed();
            }));
            m.buttons.Add(new ModalButton("都不疏（贪多）", false, delegate
            {
                p.thin = 0; p.care -= 6;
                Log("bad", "没疏果！养分分散，几个瓜都长不大。"); Changed();
            }));
            view.ShowModal(m);
        }

        public void DryDialog(PlotData pl)
        {
            ModalSpec m = new ModalSpec();
            m.title = "转熟期 · 采前控水？";
            m.body = "瓜进入定个转熟期，要不要停止浇水？";
            m.tip = "原理：采收前一周停止浇水，糖度 +1~2度；此时大水漫灌易裂果。";
            PlotData p = pl;
            m.buttons.Add(new ModalButton("开启控水（推荐）", true, delegate
            {
                p.dry = true; p.care = Clamp(p.care + 6, 0, 100);
                Log("tip", "采前控水开启：糖分正在「锁」进瓜里。" +
                    (P.autoWater ? "（自动浇水将智能跳过此地块）" : "")); Changed();
            }));
            m.buttons.Add(new ModalButton("照常浇水", false, delegate
            {
                p.dry = false;
                Log("sys", "照常浇水。糖度提升会打折扣。"); Changed();
            }));
            view.ShowModal(m);
        }

        public void ShowCard(Variety v, CardResult r)
        {
            ModalSpec m = new ModalSpec();
            m.title = "收获科普卡！";
            Color c;
            ColorUtility.TryParseHtmlString(r.tierHex, out c);
            m.bodyBack = c;
            m.bodyText = Color.white;
            m.artVid = v.id;
            m.photoVid = v.id;
            m.body =
                "<b>庞各庄西瓜科普收藏卡 · " + r.tierName + "</b>\n" +
                "<b><size=30>" + v.name + "</size></b>\n" +
                "北京 · 大兴 · 庞各庄 ——「大兴西瓜」地理标志核心产区\n" +
                r.stars + "\n\n" +
                "中心糖度 <b>" + r.brix + "°</b>　单瓜重 <b>" + r.wt + "kg</b>　品质分 <b>" + r.q +
                "</b>　售价 <b>" + r.coins + "金</b>\n\n" +
                "<b>【品种档案】</b>\n" + v.file + "\n\n" +
                "<b>【它与庞各庄】</b>\n" + v.story + "\n\n" +
                "<b>【挑瓜小贴士】</b>\n" + v.tips;
            m.buttons.Add(new ModalButton("收入图鉴", true, null));
            m.buttons.Add(new ModalButton("稍后再看", false, null));
            view.ShowModal(m);
        }

        public void BookDialog()
        {
            ModalSpec m = new ModalSpec();
            m.title = "庞各庄品种图鉴（" + P.cards.Count + "/3）";
            m.body = "种出对应品种即可解锁收藏卡，记录你的最佳成绩。卡片左侧为网络公开的品种实物资料图，右侧为游戏内成绩。";
            for (int i = 0; i < FarmData.V.Length; i++)
            {
                Variety v = FarmData.V[i];
                CardEntry c = P.GetCard(v.id);
                BookCardInfo b = new BookCardInfo();
                b.no = i + 1;
                b.name = v.name;
                b.vid = v.id;
                b.got = c != null;
                b.desc = c != null ? v.desc : "未解锁 · 种出一枚即可点亮此卡";
                b.file = c != null ? v.file : "种下" + v.name + "的种子，收获时即可解锁品种档案与最佳成绩。";
                b.tier = c != null ? c.tier : "未解锁";
                b.stars = c != null ? FarmData.TierStars(c.q) : "☆☆☆☆☆";
                b.tierHex = c != null ? FarmData.TierHex(c.q) : "#94A3B8";
                b.quality = c != null ? c.q.ToString() : "--";
                b.brix = c != null ? c.brix : "--";
                b.wt = c != null ? c.wt : "--";
                b.coins = v.basePrice.ToString();
                m.book.Add(b);
            }
            m.tip = "品质分越高卡面品级越高：铜牌卡 → 银牌卡 → 金牌卡 → 瓜王金卡。实物照片来自网络公开资料，仅作科普示意。";
            m.buttons.Add(new ModalButton("关闭 · 返回农场", true, null));
            view.ShowModal(m);
        }

        public void WarehouseDialog()
        {
            if (P.wh.Count == 0)
            {
                Msg("仓库是空的", "收获的西瓜会先放进仓库，在这里卖掉换成西瓜币。"); return;
            }
            ModalSpec m = new ModalSpec();
            m.title = "仓库（" + P.wh.Count + "个瓜）";
            m.body = "当前西瓜币 " + Balance + "。全部卖出可获得 " + TotalWarehouseValue() +
                "，另有每瓜 +" + ShardWallet.PER_MELON + " 西瓜币奖励。\n" +
                "西瓜币由全项目统一钱包保存，重置农场存档不会清空。";
            for (int i = 0; i < P.wh.Count && i < 40; i++)
            {
                int idx = i;
                WarehouseItem w = P.wh[i];
                Variety v = FarmData.GetVariety(w.v);
                m.rows.Add(new ModalRow(
                    (v != null ? v.name : "西瓜") + " · 品质" + w.q + " · " + w.wt.ToString("0.00") +
                    "kg · 糖度" + w.brix.ToString("0.0") + "°",
                    "卖 " + w.coins + "金", delegate { Sell(idx); }));
            }
            m.buttons.Add(new ModalButton("全部卖出 " + TotalWarehouseValue() + "金", true, delegate
            {
                int cnt = P.wh.Count;
                int total = TotalWarehouseValue();
                int bonus = cnt * ShardWallet.PER_MELON;
                int payout = total + bonus;
                int balance = WatermelonCurrencyWallet.Credit(payout, "farm_sale");
                Log("sys", "卖出" + cnt + "个瓜，售价" + total + " + 奖励" + bonus +
                    "，西瓜币到账 +" + payout + "（当前 " + balance + "）。");
                P.wh.Clear();
                Changed();
                view.ShowShardToast(payout, balance);
            }));
            m.buttons.Add(new ModalButton("关闭 · 返回农场", false, null));
            view.ShowModal(m);
        }

        int TotalWarehouseValue()
        {
            int s = 0;
            for (int i = 0; i < P.wh.Count; i++) s += P.wh[i].coins;
            return s;
        }

        public void Sell(int i)
        {
            if (i < 0 || i >= P.wh.Count) return;
            WarehouseItem w = P.wh[i];
            int payout = w.coins + ShardWallet.PER_MELON;
            int balance = WatermelonCurrencyWallet.Credit(payout, "farm_sale");
            Variety v = FarmData.GetVariety(w.v);
            P.wh.RemoveAt(i);
            Log("sys", "卖出" + (v != null ? v.name : "西瓜") + "，售价 " + w.coins +
                " + 奖励 " + ShardWallet.PER_MELON + "，西瓜币到账 +" + payout + "（当前 " + balance + "）。");
            Changed();
            if (P.wh.Count > 0) WarehouseDialog();
            // 放在最后：提示层要盖在刷新后的仓库弹窗之上
            view.ShowShardToast(payout, balance);
        }

        public void AllWater()
        {
            if (!P.autoWater)
            {
                LockedPrompt("一键浇水未解锁", "自动浇水系统", FarmData.AW_PRICE, "一键浇水");
                return;
            }
            int n = 0;
            for (int i = 0; i < P.plots.Count; i++)
            {
                PlotData pl = P.plots[i];
                if (i < P.plotsOwned && !string.IsNullOrEmpty(pl.v) && pl.st < FarmData.RIPE_ST &&
                    !pl.dry && pl.water < 60)
                { pl.water = Clamp(pl.water + 35, 0, 100); n++; }
            }
            if (n > 0) Log("sys", "一键给" + n + "块地浇水。");
            Changed();
        }

        public void AllClean()
        {
            if (!P.autoClean)
            {
                LockedPrompt("一键除草除虫未解锁", "自动除草除虫系统", FarmData.AC_PRICE, "一键除草除虫");
                return;
            }
            int n = 0;
            for (int i = 0; i < P.plots.Count; i++)
            {
                PlotData pl = P.plots[i];
                if (i >= P.plotsOwned || string.IsNullOrEmpty(pl.v)) continue;
                if (pl.weed) { pl.weed = false; pl.care = Clamp(pl.care + 2, 0, 100); n++; }
                if (!string.IsNullOrEmpty(pl.pest)) { pl.pest = ""; pl.care = Clamp(pl.care + 2, 0, 100); n++; }
            }
            if (n > 0) Log("sys", "清理了" + n + "处草/虫。");
            Changed();
        }

        public void HelpDialog()
        {
            ModalSpec m = new ModalSpec();
            m.title = "玩法说明（QQ农场式）";
            m.body =
                "<b>目标：</b>种出高品质西瓜，收集三种庞各庄品种科普卡。\n\n" +
                "<b>基本操作：</b>点空地选种；点灰色地块买地；浇水/除草/除虫用工具点击；开花期要点「授粉」徽章；成熟点击收获→仓库卖钱。\n\n" +
                "<b>工具光标：</b>点「水壶 / 施肥 / 锄头·除草 / 除虫」后，鼠标指针会变成对应农具插画，移到地块上点击即可操作；换工具就换光标。\n\n" +
                "<b>化肥（商城" + FarmData.FERT_PRICE + "金/袋）：</b>工具栏「施肥」按键（水壶旁，有库存才显示），选中后点地块：作物<b>立即进入下一阶段</b>，收获售价<b>+" +
                FarmData.FERT_BONUS + "金</b>，每株限1次。\n\n" +
                "<b>土地升级（" + FarmData.UPG_PRICE + "金/块）：</b>点击地块右上角「升级」按钮（钱够亮起/不够变暗），升级后该地块<b>生长×3</b>、永久生效。\n\n" +
                "<b>自动浇水（商城" + FarmData.AW_PRICE + "金）：</b>一次购买永久生效，湿度低于32%自动补水，采前控水地块智能跳过；" +
                "购买前工具栏「一键浇水」为灰色，点击会提示前往商城。\n\n" +
                "<b>自动除草除虫（商城" + FarmData.AC_PRICE + "金）：</b>一次购买永久解锁「一键除草除虫」，" +
                "一键清除全场杂草与虫害并恢复田间管理分；购买前按钮为灰色，点击同样会提示前往商城。\n\n" +
                "<b>田间告警 10 秒生死线：</b>地块出现「有草 / 蚜虫·红蜘蛛 / 缺水」徽章时，徽章会显示倒计时；" +
                "<b>10 秒内</b>用「锄头·除草 / 除虫 / 水壶」处理掉全部告警即可解除，" +
                "超时未处理瓜苗会<b>枯萎</b>并弹出遗憾提示（点任意处或 4 秒后消失），清理枯苗可补种。" +
                "计时按真实时间，2x 倍速也不会缩短；买自动浇水 / 一键除草除虫可自动解除对应告警。\n\n" +
                "<b>倍速：</b>1x / 2x 随时切换；2x 下每秒推进 2 次生长计算，水分衰减也随之加快。\n\n" +
                "<b>西瓜币：</b>卖瓜获得售价 + 每瓜 <b>" + ShardWallet.PER_MELON +
                " 西瓜币</b>奖励；挑瓜评分也会发放等额西瓜币。全项目共用一份余额，<b>永久留存</b>，" +
                "「清空存档 / 重新开档」不会清空；顶栏与右侧「农场概览」可随时查看。\n\n" +
                "<b>所有弹窗</b>右上角 × / 点遮罩 / ESC 均可关闭；到账提示点屏幕任意处或等 2 秒自动消失，" +
                "瓜苗枯萎的遗憾提示点任意处或等 4 秒自动消失。";
            m.buttons.Add(new ModalButton("知道啦 · 开种！", true, null));
            view.ShowModal(m);
        }

        void First(string key, string title, string body)
        {
            if (P.HasTip(key)) return;
            P.tips.Add(key);
            Msg(title, body);
            Save();
        }

        // 左上状态徽章点击
        public void Badge(int i, string kind)
        {
            PlotData pl = P.plots[i];
            if (kind == "pol")
            {
                pl.pol = true; pl.care = Clamp(pl.care + 6, 0, 100);
                Log("tip", "人工授粉完成！（原理：摘雄花往雌花柱头轻涂，上午7:30—10:30花粉活力最高）");
            }
            else if (kind == "thin") ThinDialog(pl);
            else if (kind == "dry") DryDialog(pl);
            else if (kind == "pick") { Harvest(pl); return; }
            else if (kind == "clean") { ClickPlot(i); return; }
            else if (kind == "weed") { P.tool = "hoe"; ClickPlot(i); return; }
            else if (kind == "pest") { P.tool = "spray"; ClickPlot(i); return; }
            else if (kind == "water") { P.tool = "water"; ClickPlot(i); return; }
            Changed();
        }

        public void CloseTopModal() { view.CloseTopModal(); }
    }
}
