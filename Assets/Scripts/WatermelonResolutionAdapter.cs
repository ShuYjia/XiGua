using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全局唯一设计基准 1920x1080。
///
/// 缩放只发生一次：CanvasScaler(ScaleWithScreenSize + Expand) 把屏幕换算成逻辑画布，
/// 16:9 下逻辑画布恰好 1920x1080，非 16:9 下逻辑画布更长/更高。
/// 内容根节点 WatermelonAspectRoot 始终保持 1920x1080 逻辑尺寸、居中、localScale=1，
/// 绝不做二次缩放；多出来的区域由 AspectBackground 填充（背景留白）。
///
/// 设计坐标约定：以内容区中心为原点，x 向右、y 向上，单位 = 逻辑像素。
/// 屏幕坐标约定：Unity 屏幕像素，原点左下、y 向上。
/// </summary>
public static class WatermelonResolution
{
    public const float DesignWidth = 1920f;
    public const float DesignHeight = 1080f;
    public const string ContentRootName = "WatermelonAspectRoot";
    public const string ClassroomContentRootName = "ClassroomAspectRoot";
    public const string BackgroundName = "AspectBackground";

    /// <summary>当前屏幕相对 1920x1080 的等比缩放值。</summary>
    public static float GetScale()
    {
        float scale = Mathf.Min(Screen.width / DesignWidth, Screen.height / DesignHeight);
        return scale > 0.0001f ? scale : 1f;
    }

    /// <summary>内容区在屏幕像素中的矩形（居中，恒定 16:9）。</summary>
    public static Rect GetContentRect()
    {
        float scale = GetScale();
        float w = DesignWidth * scale;
        float h = DesignHeight * scale;
        return new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
    }

    /// <summary>内容区相对屏幕左下角的像素偏移量。</summary>
    public static Vector2 GetOffset()
    {
        Rect r = GetContentRect();
        return new Vector2(r.x, r.y);
    }

    /// <summary>屏幕像素点 -> 设计坐标（内容区中心为原点）。</summary>
    public static Vector2 ScreenToDesign(Vector2 screenPoint)
    {
        Rect r = GetContentRect();
        float scale = GetScale();
        return new Vector2(
            (screenPoint.x - r.x) / scale - DesignWidth * 0.5f,
            (screenPoint.y - r.y) / scale - DesignHeight * 0.5f);
    }

    /// <summary>设计坐标 -> 屏幕像素点。</summary>
    public static Vector2 DesignToScreen(Vector2 designPoint)
    {
        Rect r = GetContentRect();
        float scale = GetScale();
        return new Vector2(
            r.x + (designPoint.x + DesignWidth * 0.5f) * scale,
            r.y + (designPoint.y + DesignHeight * 0.5f) * scale);
    }

    /// <summary>屏幕像素矩形 -> 相机 normalized rect（Cut 3D 视口共用）。</summary>
    public static Rect NormalizeScreenRect(Vector2 min, Vector2 max)
    {
        float sw = Mathf.Max(1, Screen.width);
        float sh = Mathf.Max(1, Screen.height);
        Rect r = new Rect(min.x / sw, min.y / sh, (max.x - min.x) / sw, (max.y - min.y) / sh);
        r.x = Mathf.Clamp01(r.x);
        r.y = Mathf.Clamp01(r.y);
        r.width = Mathf.Clamp(r.width, 0.05f, 1f);
        r.height = Mathf.Clamp(r.height, 0.05f, 1f);
        return r;
    }

    /// <summary>把 CanvasScaler 统一到 1920x1080 + Expand（16:9 下 scale=1，非 16:9 只留白不裁切）。</summary>
    public static void EnsureCanvasScaler(CanvasScaler scaler)
    {
        if (scaler == null) return;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(DesignWidth, DesignHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        scaler.matchWidthOrHeight = 0f;
    }

    /// <summary>
    /// 把内容根节点固定为 1920x1080、居中、不缩放。
    /// 注意：这里刻意不按屏幕比例改变 sizeDelta / localScale，缩放完全由 CanvasScaler 负责，
    /// 否则会与 CanvasScaler 叠加成二次缩放，1920x1080 下画面也会变化。
    /// </summary>
    public static void ApplyAspectFit(RectTransform root)
    {
        if (root == null) return;
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(DesignWidth, DesignHeight);
        root.anchoredPosition = Vector2.zero;
        root.localScale = Vector3.one;
    }

    /// <summary>在 Canvas 下创建或复用固定 1920x1080 的内容根节点。</summary>
    public static RectTransform EnsureContentRoot(Transform parent, string name = ContentRootName)
    {
        if (parent == null) return null;
        Transform existing = parent.Find(name);
        GameObject go = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform));
        if (existing == null) go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        ApplyAspectFit(rt);
        return rt;
    }

    /// <summary>在 Canvas 下创建铺满逻辑画布的背景（非 16:9 时形成留白），置于最底层。</summary>
    public static RectTransform EnsureBackground(Transform parent, Color color)
    {
        if (parent == null) return null;
        Transform existing = parent.Find(BackgroundName);
        GameObject go = existing != null
            ? existing.gameObject
            : new GameObject(BackgroundName, typeof(RectTransform), typeof(Image));
        if (existing == null) go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        Image img = go.GetComponent<Image>();
        if (img != null)
        {
            img.color = color;
            img.raycastTarget = false;
        }
        go.transform.SetAsFirstSibling();
        return rt;
    }
}
