using System.Collections.Generic;
using UnityEngine;

/* ============================================================
 * GuiliAudio.cs —— Unity 端合成音效
 * 对应网页 js/atelier-audio.js 的阻尼模态共振 + 接触脉冲，
 * 并按用户提供的「04 拍一拍」科普图重做四档可分辨音色：
 *
 *   清脆 crisp  —— 咚咚声清脆 · 成熟度好 · 水分充足   → 成熟适口
 *   略闷 mellow —— 清中带闷   · 成熟度一般 · 风味偏淡 → 口感一般
 *   沉闷 dull   —— 声音低沉   · 可能过熟 · 或内部空心 → 损伤风险
 *   浑浊 muddy  —— 发闷发浑   · 可能未熟 · 或水分不足 → 成熟不足
 *
 * 「略闷」是清脆与沉闷之间的过渡档，使四类切面结果各有唯一音色，
 * 玩家听到什么，切瓜后就是什么结果（GuiliData.KnockKey 是唯一映射）。
 *
 * 声学差异不是简单改音量，而是同时调整：
 *   基频 / 泛音亮度 / 衰减长度 / 接触噪声 / 全段低通截止 /
 *   「咚咚」二次轻叩 / 泛音失谐（浑浊感）/ 持续噪声（发浑感）
 * 不依赖任何外部音频文件，AudioClip 由 PCM 在运行时生成并缓存。
 * ============================================================ */

public class GuiliAudio : MonoBehaviour
{
    public static GuiliAudio Instance { get; private set; }
    const int SampleRate = 22050;
    static readonly float[] Ratios = { 1f, 1.47f, 2.13f, 3.18f };

    /* 参数：基频, 衰减(s), 泛音亮度, 噪声量, 低通截止(Hz), 二次轻叩, 失谐量 */
    static readonly Dictionary<string, float[]> Profiles = new Dictionary<string, float[]> {
        { "crisp",  new float[] { 208f, .34f,  .95f, .08f, 5200f, .55f, .00f } },
        { "mellow", new float[] { 168f, .26f,  .62f, .16f, 2800f, .22f, .02f } },
        { "dull",   new float[] {  92f, .115f, .22f, .26f,  780f, .08f, .05f } },
        { "muddy",  new float[] { 132f, .21f,  .30f, .55f, 1250f, .10f, .12f } }
    };

    AudioSource source;
    readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    float volume = .65f;

    void Awake()
    {
        Instance = this;
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0;
        source.volume = volume;
    }

    /* 对应 AudioManager.unlock()：首次交互时确保音频源就绪 */
    public static void Unlock()
    {
        if (Instance == null) return;
        if (!Instance.source.enabled) Instance.source.enabled = true;
    }

    public static void Tap(string type, int seed)
    {
        if (Instance == null) return;
        Unlock();
        string key = (type ?? "mellow") + "_" + (seed % 5);
        AudioClip clip;
        if (!Instance.cache.TryGetValue(key, out clip))
        {
            clip = Instance.Build(type, seed % 5);
            Instance.cache[key] = clip;
        }
        Instance.source.PlayOneShot(clip, 1f);
    }

    /* 落刀切开：短促的「嚓」声（网页 AudioManager.cut() = tap('raw')，
       这里改为更贴合剖切动作的下扫噪声，避免与「浑浊」拍瓜声混淆）。 */
    public static void Cut()
    {
        if (Instance == null) return;
        Unlock();
        AudioClip clip;
        if (!Instance.cache.TryGetValue("cut", out clip))
        {
            clip = Instance.BuildCut();
            Instance.cache["cut"] = clip;
        }
        Instance.source.PlayOneShot(clip, 1f);
    }

    /* 网页 click()/success() 为空实现，此处保留等价接口 */
    public static void Click() { }
    public static void Success() { }

    public static void SetVolume(float v)
    {
        if (Instance == null) return;
        Instance.volume = Mathf.Clamp01(v);
        Instance.source.volume = Instance.volume;
    }

    public static float Volume()
    {
        return Instance == null ? .65f : Instance.volume;
    }

    AudioClip Build(string type, int seed)
    {
        float[] p;
        if (!Profiles.TryGetValue(type ?? "mellow", out p)) p = Profiles["mellow"];
        float baseFreq = p[0] * (.96f + seed * .018f);
        float decay = p[1], brightness = p[2], noise = p[3];
        float cutoff = p[4], second = p[5], inharm = p[6];

        float length = Mathf.Max(.30f, decay + .14f);
        int n = Mathf.CeilToInt(length * SampleRate);
        float[] data = new float[n];
        float attack = .0035f;

        /* 四个阻尼模态泛音；浑浊档让高次泛音失谐，产生「发浑」的拍频 */
        for (int i = 0; i < Ratios.Length; i++)
        {
            float ratio = Ratios[i] * (1f - inharm * i * .35f);
            float f0 = baseFreq * ratio;
            float peak = .30f / (i + 1) * (.35f + brightness * .65f) *
                         Mathf.Pow(.62f, i * (1f - brightness) * 2f);
            float dur = decay / (1f + i * .30f);
            float k = Mathf.Log(peak / .0001f) / Mathf.Max(.001f, dur);
            float phase = 0;
            for (int s = 0; s < n; s++)
            {
                float t = (float)s / SampleRate;
                float f = f0 * (1f - .05f * Mathf.Clamp01(t / .08f));
                phase += 2f * Mathf.PI * f / SampleRate;
                float env = t < attack ? peak * (t / attack) : peak * Mathf.Exp(-k * (t - attack));
                data[s] += env * Mathf.Sin(phase);
                /* 清脆档标志性的「咚咚」二次轻叩 */
                if (second > 0f)
                {
                    float t2 = t - .115f;
                    if (t2 > 0f)
                    {
                        float e2 = t2 < attack ? peak * second * (t2 / attack)
                                              : peak * second * Mathf.Exp(-k * 1.15f * (t2 - attack));
                        data[s] += e2 * Mathf.Sin(phase * 1.002f);
                    }
                }
            }
        }

        float aNoise = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SampleRate);

        /* 接触脉冲：45ms 低通白噪声（手指拍到瓜皮那一下） */
        float lp = 0;
        int noiseLen = Mathf.Min(n, Mathf.CeilToInt(.045f * SampleRate));
        for (int s = 0; s < noiseLen; s++)
        {
            float t = (float)s / SampleRate;
            lp += aNoise * ((Random.value * 2f - 1f) - lp);
            data[s] += .30f * lp * Mathf.Pow(1f - t / .045f, 3f);
        }

        /* 浑浊档：整段都混着低通噪声，听感「发闷发浑」 */
        if (noise > .25f)
        {
            float lp2 = 0;
            for (int s = 0; s < n; s++)
            {
                float t = (float)s / SampleRate;
                lp2 += aNoise * ((Random.value * 2f - 1f) - lp2);
                data[s] += noise * .24f * lp2 * Mathf.Exp(-t / Mathf.Max(.03f, decay * .8f));
            }
        }

        /* 全段一阶低通：沉闷/浑浊档把高频压掉，清脆档几乎不处理 */
        float outLp = 0;
        for (int s = 0; s < n; s++)
        {
            outLp += aNoise * (data[s] - outLp);
            data[s] = outLp;
        }

        Normalize(data);
        AudioClip clip = AudioClip.Create("Tap_" + type + "_" + seed, n, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    AudioClip BuildCut()
    {
        int n = Mathf.CeilToInt(.26f * SampleRate);
        float[] data = new float[n];
        float lp = 0;
        for (int s = 0; s < n; s++)
        {
            float t = (float)s / SampleRate;
            float sweep = 3400f - 2700f * Mathf.Clamp01(t / .26f);
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * sweep / SampleRate);
            lp += a * ((Random.value * 2f - 1f) - lp);
            float env = Mathf.Exp(-t / .085f) * Mathf.Clamp01(t / .004f);
            data[s] = lp * env * .80f +
                      Mathf.Sin(2f * Mathf.PI * (190f - 70f * (t / .26f)) * t) * env * .18f;
        }
        Normalize(data);
        AudioClip clip = AudioClip.Create("CutSlice", n, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static void Normalize(float[] data)
    {
        float peak = 0f;
        for (int i = 0; i < data.Length; i++) if (Mathf.Abs(data[i]) > peak) peak = Mathf.Abs(data[i]);
        if (peak > .0001f)
        {
            float g = .86f / peak;
            for (int i = 0; i < data.Length; i++) data[i] *= g;
        }
        int fade = Mathf.Min(256, data.Length / 8);
        for (int s = 0; s < fade; s++)
        {
            data[s] *= (float)s / fade;
            data[data.Length - 1 - s] *= (float)s / fade;
        }
    }
}
