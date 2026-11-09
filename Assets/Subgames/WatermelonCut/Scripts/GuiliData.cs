using System;
using System.Collections.Generic;
using UnityEngine;

/* ============================================================
 * GuiliData.cs
 * 《瓜里有数 · 庞各庄瓜事》 数据层
 * 与网页 js/catalog.js + js/atelier-game.js + index.html 一一对应：
 *   - 九条内部档案 Profiles、随机洗牌 assignProfiles
 *   - 五项观察、评分规则 Rules.score
 *   - 四类切面结果 outcomes
 *   - 教学四步 steps
 *   - 全部中文文案（主标题、阶段、按钮、科普正文、结果正文、档案数据）
 *
 * 本轮修订（用户要求：瓜底换回最开始、和网页一样的那版）：
 *   ① 西瓜模型与瓜皮贴图回退到网页 js/rind-textures.js 的形态：
 *      着地斑直接画在球面贴图 UV 底部（那块圆圆的白斑），
 *      不再使用独立球冠网格 / 整幅放射状瓜底视图；
 *   ② 那块白色圆斑扩展为三色：白色 / 奶油黄 / 橙黄，肉眼明显可分；
 *   ③ 奶油黄、橙黄 → 成熟适口 ripe；白色 → 其余三类结果，
 *      颜色、判读文案、切瓜结果、评分严格对应；
 *   ④ 编号 01~09 固定 + 每轮随机洗牌西瓜、拍一拍四档音、切瓜动画、
 *      首页与布局等其余已确认的内容全部保留不变。
 * ============================================================ */

public enum CutType { Ripe, Raw, Damaged, Ordinary }

/* 瓜底着地斑三色：由网页最初版「瓜底一块白色圆斑」直接扩展而来。
   三色共用同一块不规则圆形地斑，只是颜色不同：
     白色   White  —— 发白、没有黄晕 → 成熟度一般 / 不足 / 有损伤 → ordinary/raw/damaged
     奶油黄 Cream  —— 蜡质奶黄、黄晕饱满 → 成熟度高 · 口感最佳 → ripe (35 分)
     橙黄   Orange —— 暖橙黄、黄晕足   → 成熟度高 · 香甜多汁 → ripe (35 分)
   白色档内部再靠瓜皮擦伤、瓜柄、轻叩声区分三类非好瓜结果。 */
public enum SpotGrade { White, Cream, Orange }

/* 拍一拍四档：三档严格取自「04 拍一拍」科普图，
   另加一档「略闷」作为清脆↔沉闷的过渡，使四类切面结果各有唯一音色。
     清脆 crisp  —— 咚咚声清脆 · 成熟度好 · 水分充足   → ripe
     略闷 mellow —— 清中带闷   · 成熟度一般 · 风味偏淡 → ordinary
     沉闷 dull   —— 声音低沉   · 可能过熟 · 或内部空心 → damaged
     浑浊 muddy  —— 发闷发浑   · 可能未熟 · 或水分不足 → raw */
public enum KnockGrade { Crisp, Mellow, Dull, Muddy }

[Serializable]
public class MelonRecord
{
    public string variety;
    public string field;
    public string sowing;
    public string harvest;
    public string sugar;
    public string note;
}

[Serializable]
public class MelonProfile
{
    public string type;       // ripe / raw / damaged / ordinary
    public string stem;       // 瓜柄描述
    public string spot;       // 着地斑描述
    public string appearance; // 表皮描述
    public string grade;      // white / cream / orange
    public int seed;          // 内部序号
}

[Serializable]
public class MelonInfo
{
    public string id;            // "01" .. "09" 或 "T"，编号固定在位置
    public string label;         // 第 01 号瓜 / 示范瓜
    public string cut;           // 切面预设
    public string knock;         // 音色 crisp/mellow/dull/muddy
    public string appearance;
    public string spot;
    public string stem;
    public MelonRecord record;
    public bool damaged;
    public int textureSeed;
    public bool freckles;
    public bool scuff;
    public bool wrinkles;
    public int profileSeed;
    public int slot;             // 桌面 3x3 槽位（固定，不再洗牌）
    public SpotGrade spotGrade;  // 瓜底圆斑颜色（决定瓜皮贴图）
    public int spotVariant;      // 每轮随机的圆斑形状/方位/细斑参数
    public bool[] observed = new bool[5];
    public Vector3 home;

    public bool Complete()
    {
        for (int i = 0; i < 5; i++) if (!observed[i]) return false;
        return true;
    }
    public int ObservedCount()
    {
        int n = 0;
        for (int i = 0; i < 5; i++) if (observed[i]) n++;
        return n;
    }
    public bool AnyObserved()
    {
        for (int i = 0; i < 5; i++) if (observed[i]) return true;
        return false;
    }
}

public static class GuiliData
{
    /* ---------- 网页 CSS 配色 / 字体 ---------- */
    public static Color Paper  = Hex("f4efdc");
    public static Color Ink    = Hex("333b2c");
    public static Color Green  = Hex("405b42");
    public static Color Muted  = Hex("8a8b72");
    public static Color Line   = Hex("bab398");
    public static Color Red    = Hex("9f4b3b");
    public static Color Cream  = Hex("eee8d5");
    public static Color DeepGreen = Hex("2d4532");
    public static Color Wood   = Hex("e5dfcd");
    public static Color KaiInk = Hex("333b2c");

    public static Color Hex(string h)
    {
        Color c;
        ColorUtility.TryParseHtmlString("#" + h, out c);
        return c;
    }

    /* ---------- 五项观察 ---------- */
    public static readonly string[] Keys = { "skin", "spot", "stem", "sound", "record" };
    public static readonly string[] KeyNames = { "观瓜皮", "看底部", "辨瓜柄", "轻叩听音", "查档案" };
    public static readonly string[] ReasonNames = { "观瓜皮", "看底部", "辨瓜柄", "轻叩听音", "查档案", "深纹一定更甜" };
    public static readonly string[] ReasonKeys = { "skin", "spot", "stem", "sound", "record", "stripe" };

    /* ---------- 阶段导航（header） ---------- */
    public static readonly string[] StageNames = { "识瓜", "观九瓜", "独立选", "切瓜", "瓜笺" };
    public static readonly string[] StageNumbers = { "一", "二", "三", "四", "五" };

    /* ---------- 首页 ---------- */
    public const string Brand = "瓜里有数";
    public const string BrandSub = "庞各庄 · 瓜事";
    public const string HomeEyebrow = "夏日瓜事 · 一卷识瓜";
    /* CSS h1 letter-spacing:18px；uGUI Text 无字距属性，用半角空格近似 */
    public const string HomeTitleA = "瓜 里";
    public const string HomeTitleB = "有 数";
    public const string HomeLead = "循纹观色，轻叩听音。\n在庞各庄的瓜摊，学会有依据地挑一颗好瓜。";
    public const string HomeStart = "展卷学挑瓜  →";
    public const string HomePractice = "直接入瓜摊";
    public const string HomeAbout = "认识庞各庄　↗";
    public const string HomeSmall = "挑一颗，细细看，再切开验证你的眼光";
    public const string HomeSeal = "瓜乡\n记事";

    /* ---------- 教学四步 ---------- */
    public static readonly string[] StepKeys = { "skin", "spot", "stem", "sound" };
    public static readonly string[] StepTitles = { "观皮识相", "翻底寻斑", "辨柄识鲜", "轻叩听音" };
    public static readonly string[] StepHints = {
        "轻轻拖动西瓜，看看四周的瓜皮，有没有破损或不寻常的斑块。",
        "点一下“看底部”，把瓜翻过来，看贴地那块圆斑是白色、奶油黄还是橙黄。",
        "点一下“辨瓜柄”，凑近看看瓜柄的颜色、弯曲和干湿变化。",
        "点一下“轻叩听音”，听听是清脆、略闷、沉闷还是浑浊，再对照瓜底看看。"
    };
    public static readonly string[] StepKnowledge = {
        "纹路深浅并不直接代表甜度。先留意裂口、破损和异常色斑。",
        "瓜底贴地的一侧长着一块圆圆的着地斑。这块斑若是奶油黄或橙黄色、黄晕饱满，往往成熟度高、口感最佳、香甜多汁；"
            + "若是发白、没有黄晕，多半成熟度一般、风味偏淡，或生长时间短还不够甜，也可能伴随擦伤软斑。"
            + "每颗瓜的斑形、大小与方位都不同，要翻过来看才算数；别只认号码，认颜色。",
        "瓜柄连接植株，可能鲜绿、半干或干褐。干枯也可能来自存放，不能等同于甜。",
        "轻叩瓜身，听的是共鸣：声音清脆、咚咚带回味，通常成熟度好、水分充足；声音低沉沉闷，"
            + "可能过熟或内部空心；发闷发浑，则可能未熟或水分不足；介于清脆与沉闷之间的略闷声，多为成熟度一般。"
            + "大小、品种和敲击位置也会改变音色，请和瓜底、瓜柄一起判断。"
    };
    public const string ChapterTutorial = "卷一 · 识瓜";
    public const string TutorialKnowledge = "先跟着这颗示范瓜练练手，等会儿就由你来挑。";
    public const string JournalFoot = "瓜柄与花痕，记得分开看\n多找几条线索，挑瓜更有把握";
    public const string KnowledgeLabel = "瓜农手记";

    /* ---------- 练习 / 观察 ---------- */
    public const string ChapterPractice = "卷二 · 九瓜辨识";
    public const string PracticeTask = "九瓜待辨";
    public const string PracticeHint = "点一颗瓜，拿近看看。可以多挑几颗比较，找到心仪的就选它，不必把整篮都看完。";
    public const string PracticeKnowledge = "挑瓜不着急：看看瓜皮、翻翻底部，再轻叩听一听。把几条线索放在一起，心里就更有数。";
    public const string PracticeCaption = "拖动看看瓜摊 · 点一颗拿近看 · 滚轮或双击放大";
    public const string ObserveHint = "拖动瓜身，换个方向看看；滚轮或双击可以放大。翻到底部后，也能继续转着细看。";
    public const string ObserveKnowledgeTail = "。想看底部、辨瓜柄或听声音，就试试右边的工具；看中了，随时可以选它。";
    public const string CaptionBody = "底部朝上，转一转细看 · 滚轮可放大";
    public const string CaptionSkin = "拖动转瓜 · 滚轮放大 · 双击放大或还原";
    public const string CaptionDefault = "拖动转瓜，看看另一面 · 滚轮或双击可放大";
    public const string NextWithActive = "选这颗 · 留下依据 →";
    public const string NextWithoutActive = "我选好了 · 去选择 →";

    /* ---------- 工具动作后的正文 / 视角提示 ---------- */
    public const string SkinTail = "。再转一转，别漏掉背面的细节。";
    public const string SpotTail = "。着地斑长在贴地的瓜底一侧，正中的小圆痕是花痕（瓜脐），和另一端的瓜柄可不是一回事。";

    /* 「看底部」后的判读正文：与三色圆斑、瓜皮贴图、切瓜结果、评分严格一致。 */
    public static string SpotReading(MelonInfo m)
    {
        if (m == null) return "";
        switch (m.spotGrade)
        {
            case SpotGrade.Cream:
                return "瓜底是一块蜡质的奶油黄圆斑，黄晕饱满，通常成熟度高、口感最佳" + SpotTail;
            case SpotGrade.Orange:
                return "瓜底是一块橙黄色的圆斑，颜色暖、黄晕足，往往成熟度高、香甜多汁" + SpotTail;
            default:
                return "瓜底那块圆斑发白、没有黄晕，成熟度一般或还不够甜；若斑旁还伴着擦伤软斑，更要留意" + SpotTail;
        }
    }
    public const string CaptionAfterSpot = "底部朝上，慢慢转着看 · 双击可放大";
    public const string CaptionAfterSpotBack = "瓜已翻回来了，可以接着看瓜皮和瓜柄";
    public const string StemTail = "。瓜柄也会随采收和存放发生变化，记得再结合其他线索判断。";
    public const string CaptionStem = "凑近看瓜柄 · 留意颜色和干湿变化";

    /* ---------- 拍一拍（04 科普图） ---------- */
    public static readonly string[] KnockKeys = { "crisp", "mellow", "dull", "muddy" };
    public static readonly string[] KnockTitles = { "声音清脆", "声音略闷", "声音沉闷", "声音浑浊" };
    /* 三行说明：前两行严格取自科普图，第三行是对应的成熟度判断 */
    public static readonly string[,] KnockCards = {
        { "声音清脆", "咚咚声清脆", "成熟度好 · 水分充足" },
        { "声音略闷", "清中带闷",   "成熟度一般 · 风味偏淡" },
        { "声音沉闷", "声音低沉",   "可能过熟 · 或内部空心" },
        { "声音浑浊", "发闷发浑",   "可能未熟 · 或水分不足" }
    };
    public const string SoundKnowledge = "同一篮里的瓜，轻叩声各不相同：清脆、略闷、沉闷、浑浊，分别对应成熟度好、"
        + "成熟度一般、可能过熟或内部空心、可能未熟或水分不足。记住这颗瓜的声音，再听听别的瓜。"
        + "瓜的大小和品种也会影响音色，别只凭一次轻叩就下结论。";
    public const string SoundWaveLabel = "侧耳听瓜";
    public const string KnockTail = "声音只是线索之一，再结合瓜底与瓜柄一起判断。";

    public static int KnockIndex(string key)
    {
        for (int i = 0; i < KnockKeys.Length; i++) if (KnockKeys[i] == key) return i;
        return 1;
    }
    public static string KnockTitle(string key) { return KnockTitles[KnockIndex(key)]; }
    /* 切面预设 → 音色的唯一映射；声音与切瓜结果严格对应 */
    public static string KnockKey(string cut)
    {
        if (cut == "ripe") return "crisp";
        if (cut == "ordinary") return "mellow";
        if (cut == "damaged") return "dull";
        return "muddy";                 // raw
    }
    public static string KnockReading(MelonInfo m)
    {
        if (m == null) return SoundKnowledge;
        string body;
        switch (m.knock)
        {
            case "crisp": body = "这一声清脆利落，咚咚带回味，多半成熟度好、水分充足——是好瓜的线索之一"; break;
            case "mellow": body = "这一声清中带闷，余音不算透亮，成熟度一般、风味偏淡的可能性更大"; break;
            case "dull": body = "这一声低沉发闷，几乎没有余音，可能过熟或内部空心，先留意有没有软斑和伤口"; break;
            default: body = "这一声发闷发浑，散得又快，可能还未熟或水分不足，别急着选它"; break;
        }
        return body + "。" + KnockTail;
    }

    public const string RecordFoot = "这些田间小记聊作参考，瓜未剖开，糖度难断；你的眼光，还要靠眼前的细看。";
    public const string ArchiveTitleTail = " · 生长档案";

    /* ---------- 视角工具栏 ---------- */
    public const string ViewOrbitOff = "拖动看四周";
    public const string ViewOrbitOn = "拖动转西瓜";
    public const string ViewReset = "回到原视角";
    public const string ViewFollowOn = "镜头轻随目光";
    public const string ViewFollowOff = "镜头稳在原处";
    public const string ViewOrbitTip = "此时拖动，移的是镜头；再点一下，又回到转瓜";
    public const string ViewResetTip = "回到最初的角度与远近";
    public const string ViewFollowTip = "让镜头随目光轻移；不惯，再点便收起";

    /* ---------- 四类切面结果（正文回扣三色圆斑与拍瓜声） ---------- */
    public static readonly string[] OutcomeKeys = { "ripe", "raw", "damaged", "ordinary" };
    public static readonly string[,] Outcomes = {
        { "成熟适口", "正当时令的一颗好瓜。切面红润、组织饱满，是今天食用的优先选择。瓜底那块奶油黄／橙黄的圆斑，加上清脆的轻叩声，正对上了这副好瓤。" },
        { "成熟不足", "瓜瓤偏浅、组织偏硬。多半是提早采了。回想一下：这颗瓜底部是发白的圆斑，轻叩时也是发闷发浑的一声。" },
        { "损伤风险", "切面有处不对劲，外皮也有破损。它底部的圆斑发白、旁边本就有擦伤，轻叩时那一声低沉沉闷，也在提醒内部可能空心。" },
        { "口感一般", "熟得还欠几分火候，瓜瓤略淡，风味不及成熟的那几颗。发白的圆斑、略闷的轻叩声，说的正是寻常口味。" }
    };
    public static int OutcomeIndex(string key)
    {
        for (int i = 0; i < OutcomeKeys.Length; i++) if (OutcomeKeys[i] == key) return i;
        return 0;
    }

    /* ---------- 瓜底三色 ←→ 切面结果的映射 ---------- */
    /* 四类结果只有「好瓜 / 非好瓜」两档与地斑颜色绑定：
       黄色两档（奶油黄、橙黄）必定是 ripe；白色必定不是 ripe。
       白色内部再由瓜皮擦伤、瓜柄、轻叩声区分 ordinary / raw / damaged，
       这与网页 catalog.js 的设计一致（地斑只有深浅两档，不是唯一线索）。 */
    public static bool IsGoodSpot(SpotGrade g) { return g == SpotGrade.Cream || g == SpotGrade.Orange; }
    public static bool GradeMatchesCut(SpotGrade g, string cut)
    {
        return IsGoodSpot(g) ? cut == "ripe" : cut != "ripe";
    }
    public static SpotGrade GradeFor(string cut)
    {
        return cut == "ripe" ? SpotGrade.Cream : SpotGrade.White;
    }
    public static SpotGrade ParseGrade(string key)
    {
        if (key == "cream") return SpotGrade.Cream;
        if (key == "orange") return SpotGrade.Orange;
        return SpotGrade.White;
    }
    public static string GradeKey(SpotGrade g)
    {
        if (g == SpotGrade.Cream) return "cream";
        if (g == SpotGrade.Orange) return "orange";
        return "white";
    }
    public static string GradeName(SpotGrade g)
    {
        if (g == SpotGrade.Cream) return "奶油黄圆斑";
        if (g == SpotGrade.Orange) return "橙黄圆斑";
        return "白色圆斑";
    }

    /* ---------- 挑战表 ---------- */
    public const string ChallengeChapter = "卷三 · 独立挑瓜";
    public const string ChallengeTitle = "这一篮，选哪颗？";
    public const string ChallengeSubtitle = "哪颗最让你心动？选中它，再说说你留意到了哪些细节。";
    public const string ChallengeCatalog = "九瓜图录";
    public const string ChallengeCatalogNote = "号牌依旧 · 瓜已换新";
    public const string ChallengeReasons = "我的依据";
    public const string ChallengeReasonsNote = "两项起记";
    public const string ChallengeEmpty = "还没选好？可以回瓜摊再看看。";
    public const string ChallengeBack = "回到瓜摊";
    public const string ChallengeSubmit = "确认选择 · 切瓜求证 →";
    public const string WarnNoChoice = "先选一颗你看中的瓜，再来聊聊为什么选它。";
    public const string WarnFewReasons = "哪些细节让你看中了它？记下两三处再说吧。";

    /* ---------- 切瓜 ---------- */
    public const string CutChapter = "卷四 · 切瓜求证";
    public const string CutCaptionBefore = "切开看看，你刚才的判断准不准？";
    public const string CutCaptionAfter = "看看瓜瓤，再回想刚才的观察";
    public const string CutEvidenceStripe = "深色纹路不等于更甜，下次记得把其他线索也一起考虑。";
    public const string CutEvidenceNormal = "回想刚才看到的瓜底、听到的轻叩声，再对照切面看看。";
    public const string CutButton = "收下这页瓜笺 →";

    /* ---------- 成绩单 ---------- */
    public const string ResultChapter = "卷五 · 瓜乡结业笺";
    public const string ResultTitle = "今日挑瓜，有据可循";
    public const string ResultHome = "合卷归首页";
    public const string ResultRetry = "换一篮 · 重新挑瓜 →";
    public const string RecapA = "看完整，察底斑，辨瓜柄，听共鸣。";
    public const string RecapB = "不把任何一种线索，当作好瓜的保证。";
    public static readonly string[] ScoreNames = { "细看之功", "挑瓜之准", "依据之实" };
    public static readonly int[] ScoreMax = { 45, 35, 20 };

    /* ---------- 弹窗 ---------- */
    public const string ModalEyebrow = "瓜乡手记";
    public const string ModalDone = "记住了 →";
    public const string AboutTitle = "庞各庄 · 瓜乡记事";
    public const string AboutBody = "庞各庄位于北京市大兴区，盛夏时节瓜田铺展，西瓜不仅是这里颇具代表性的农产品，也凝结着一方水土的种植经验。挑瓜并不是寻找某个万能口诀，而是在观察瓜皮、着地斑与瓜柄，比较轻叩声和生长记录的过程中，把多条线索放在一起判断。希望这卷瓜事带你认识庞各庄的瓜乡风物，也学会更从容、更有依据地挑选一颗西瓜。";
    public const string SettingsTitle = "听音与把玩";
    public const string SettingsControls = "在瓜摊上拖动，就能换个角度看看。拿起一颗瓜后，拖动可以转瓜；轻点“拖动看四周”，移的便是镜头。滚轮可以拉近、拉远，双击放大，再双击还原；“回到原视角”也能恢复远近。翻到底部后，依然可以转着细看。";
    public const string SettingsAudio = "轻叩时留意四种共鸣：清脆（成熟度好、水分充足）、略闷（成熟度一般）、沉闷（可能过熟或内部空心）、浑浊（可能未熟或水分不足）。这里的几声聊作比照，真瓜的声响，还随大小、品种与手上轻重而变。翻到瓜底时，记得看那块圆斑是白色、奶油黄还是橙黄。";

    /* ---------- 示范瓜（教学） ---------- */
    public static MelonInfo TutorialMelon()
    {
        MelonInfo m = new MelonInfo();
        m.id = "T";
        m.label = "示范瓜";
        m.cut = "ripe";
        m.knock = "crisp";
        m.appearance = "外观完整，无明显破损";
        m.spot = "奶油黄圆斑";
        m.stem = "瓜柄饱满卷曲，状态良好";
        m.damaged = false;
        m.spotGrade = SpotGrade.Cream;
        m.spotVariant = 20260618;
        m.textureSeed = 1701;
        m.freckles = false;
        m.scuff = false;
        m.wrinkles = false;
        m.record = new MelonRecord {
            variety = "京欣8号",
            field = "庞各庄瓜田 · 三号棚",
            sowing = "3月12日",
            harvest = "6月18日（足期采收）",
            sugar = "中心糖度 11.8°",
            note = "看这长势，正是采收的好时候。"
        };
        return m;
    }

    /* ---------- 九条内部档案（catalog.js Profiles） ----------
       第 6 列是瓜底圆斑颜色；ripe 三颗分到奶油黄 / 橙黄两档，
       其余六颗都是白色。三色与切瓜结果由 GradeMatchesCut 严格对应。 */
    /* 九颗瓜的地块名：替代原先「示范地块 3」这类数字编号，
       让生长档案读起来像瓜田里的记载，随机对应关系不变。 */
    static readonly string[] FieldNames = { "东畦", "西畦", "南垄", "北垄", "沙地",
                                           "河滩", "老圃", "新棚", "瓜田" };

    static readonly string[,] RawProfiles = {
        { "ripe",     "鲜绿弯柄", "奶油黄圆斑",   "表皮完整、触感结实",   "0", "cream" },
        { "raw",      "青绿直柄", "白色圆斑",     "表皮完整、触感较硬",   "1", "white" },
        { "damaged",  "干褐瓜柄", "白色圆斑伴擦伤", "可见裂损与棕色软斑", "2", "white" },
        { "ordinary", "半干弯柄", "白色圆斑",     "瓜皮完整、无明显损伤", "3", "white" },
        { "ripe",     "半干弯柄", "橙黄圆斑",     "表皮完整、触感结实",   "4", "orange" },
        { "raw",      "鲜绿直柄", "白色圆斑",     "表皮完整、触感较硬",   "5", "white" },
        { "ordinary", "鲜绿弯柄", "白色圆斑",     "瓜皮完整、无明显损伤", "6", "white" },
        { "damaged",  "半干瓜柄", "白色圆斑伴擦伤", "可见裂损与棕色软斑", "7", "white" },
        { "ripe",     "干褐弯柄", "奶油黄圆斑",   "表皮完整、触感结实",   "8", "cream" }
    };

    public static List<MelonProfile> Profiles()
    {
        List<MelonProfile> list = new List<MelonProfile>();
        for (int i = 0; i < 9; i++)
        {
            MelonProfile p = new MelonProfile();
            p.type = RawProfiles[i, 0];
            p.stem = RawProfiles[i, 1];
            p.spot = RawProfiles[i, 2];
            p.appearance = RawProfiles[i, 3];
            p.seed = int.Parse(RawProfiles[i, 4]);
            p.grade = RawProfiles[i, 5];
            list.Add(p);
        }
        return list;
    }

    /* Fisher-Yates，与 Rules.shuffle 一致 */
    public static void Shuffle<T>(IList<T> a, System.Random rnd)
    {
        for (int i = a.Count - 1; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            T t = a[i]; a[i] = a[j]; a[j] = t;
        }
    }

    /* 对应 assignProfiles，并按用户要求调整随机方式：
       ① 编号 01~09 与 3x3 位置永久固定（slots 恒为 0..8）；
       ② 每轮只洗牌「九颗西瓜」——档案、圆斑颜色、音色、纹理种子、
          斑形/方位参数、雀斑/擦痕、半径都重新随机；
       ③ 因此同一个号码、同一个位置，每轮都是完全不同的一颗瓜，
          记号码和记位置都无效，只能现场翻底、轻叩、看瓜柄来学挑瓜。
       签名涵盖 编号→颜色→切面→音色→槽位→斑形，保证相邻两篮绝不重复。 */
    public static string AssignProfiles(List<MelonInfo> melons, int[] slots, System.Random rnd, out string signature)
    {
        List<MelonProfile> profiles = Profiles();
        Shuffle(profiles, rnd);
        string sig = "";
        for (int i = 0; i < 9; i++)
        {
            MelonProfile p = profiles[i];
            string id = (i + 1).ToString("00");     /* 编号固定在位置，不洗牌 */
            MelonInfo m = new MelonInfo();
            m.id = id;
            m.label = "第 " + id + " 号瓜";
            m.cut = p.type;
            m.knock = KnockKey(p.type);
            m.damaged = p.type == "damaged";
            m.appearance = p.appearance;
            m.spot = p.spot;
            m.stem = p.stem;
            m.profileSeed = p.seed;
            m.spotGrade = ParseGrade(p.grade);
            m.spotVariant = rnd.Next(0, int.MaxValue);
            m.textureSeed = rnd.Next(1, int.MaxValue);
            m.freckles = rnd.NextDouble() < .55;
            m.scuff = rnd.NextDouble() < .30;
            m.wrinkles = m.damaged && rnd.NextDouble() < .70;
            m.slot = slots[i];
            m.home = new Vector3((m.slot % 3 - 1) * 1.8f, 1f, (m.slot / 3 - 1) * 1.65f);
            m.record = new MelonRecord {
                variety = (p.seed % 2 == 1) ? "京欣型圆瓜" : "小型圆瓜",
                field = "庞各庄 · " + FieldNames[p.seed],
                sowing = "春季定植",
                harvest = p.type == "raw" ? "提早采下，贴地日短" : "应时采收",
                sugar = "尚未测糖",
                note = p.type == "damaged" ? "路上磕碰过，留意表皮的伤。" : "小记聊作旁证，还要看瓜皮、底部、瓜柄与声响。"
            };
            melons.Add(m);
            sig += (i == 0 ? "" : ",") + id + ":" + p.grade + ":" + p.type + ":" +
                   m.knock + ":" + m.slot + ":" + (m.spotVariant % 9973);
        }
        signature = sig;
        return sig;
    }

    /* 对应 Rules.score */
    public static int[] Score(MelonInfo m, List<string> reasons)
    {
        int valid = 0;
        bool stripe = false;
        for (int i = 0; i < reasons.Count; i++)
        {
            string r = reasons[i];
            if (r == "stripe") { stripe = true; continue; }
            for (int k = 0; k < Keys.Length; k++) if (Keys[k] == r) { valid++; break; }
        }
        int observation = m.ObservedCount() * 9;
        int selection = m.cut == "ripe" ? 35 : m.cut == "ordinary" ? 22 : 0;
        int evidence = Mathf.Clamp(valid * 5 - (stripe ? 10 : 0), 0, 20);
        return new int[] { observation, selection, evidence };
    }

    public static string Rank(int total)
    {
        return total >= 85 ? "识瓜有道" : total >= 60 ? "初窥门径" : "再观一篮";
    }

    /* 结果正文（5 版润色版） */
    public static string ResultMessage(List<MelonInfo> melons, MelonInfo chosen)
    {
        int seen = 0;
        for (int i = 0; i < melons.Count; i++) if (melons[i].AnyObserved()) seen++;
        int idx = OutcomeIndex(chosen.cut);
        return "九颗瓜里细看了 " + seen + " 颗，末了相中 " + chosen.id + " 号。剖开一瞧：" + Outcomes[idx, 0] + "。";
    }

    public static string LedgerText(int completed, int round)
    {
        return "细看 " + completed + " 颗 · 第 " + round + " 篮";
    }

    public static string SelectionSummary(string chosen)
    {
        return chosen == null ? "还没选好？可以回瓜摊再看看。" : "相中了第 " + chosen + " 号瓜";
    }

    public static string SelectionSummaryHint(string chosen)
    {
        return "相中了第 " + chosen + " 号瓜 · 哪几处让你动了心？";
    }
}
