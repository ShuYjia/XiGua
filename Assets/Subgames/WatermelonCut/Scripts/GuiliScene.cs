using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/* ============================================================
 * GuiliScene.cs —— 三维瓜摊、西瓜模型与全部交互控制
 * 对应网页 js/atelier-scene.js + js/watermelon.js + js/atelier-game.js 的 3D 部分：
 *   - 木桌 / 桌腿 / 接触阴影 / 暖色光照
 *   - 九瓜 3x3 随机槽位，group 锁在桌面接触点、visual 承载全部旋转
 *   - 瓜体 / 瓜柄 / 花痕 / 花痕环 / 剖切半球
 *   - support()：按可见网格最低顶点计算支撑高度，翻底后不穿桌面
 *   - 相机 Spherical 平滑：全景 / 单瓜 / 瓜柄近观 / 切瓜
 *   - 拖动转瓜、拖动环顾、滚轮缩放(0.55~1.25)、双击切换、鼠标轻移、归位、方向键
 *   - 射线点选西瓜、翻底后保持底部朝上
 * ============================================================ */

public class GuiliMelon
{
    public MelonInfo info;
    public GameObject group, visual, body, stem, scar, ring, halves, halfL, halfR, shadow;
    public Texture2D skin, cross;
    public Material skinMat, capMat;
    public float radius;
    public float flip, yaw, tilt, target, rotated;
    public bool busy;
    public Vector3 home;
    public bool cutShown;

    public void ApplyPose()
    {
        visual.transform.localRotation = Quaternion.Euler((flip + tilt) * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, 0);
        Support();
    }
    public void Support()
    {
        visual.transform.localPosition = Vector3.zero;
        /* Unity 的 localToWorldMatrix 在访问时按当前层级即时计算，无需手动刷新 */
        Matrix4x4 inv = group.transform.worldToLocalMatrix;
        float low = float.PositiveInfinity;
        MeshFilter[] filters = visual.GetComponentsInChildren<MeshFilter>(true);
        Vector3 v = new Vector3();
        for (int f = 0; f < filters.Length; f++)
        {
            MeshFilter mf = filters[f];
            bool shown = mf.gameObject.activeInHierarchy;
            if (!shown || mf.sharedMesh == null) continue;
            Matrix4x4 mat = inv * mf.transform.localToWorldMatrix;
            Vector3[] verts = mf.sharedMesh.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                v = mat.MultiplyPoint3x4(verts[i]);
                if (v.y < low) low = v.y;
            }
        }
        if (float.IsInfinity(low)) low = -radius * .96f;
        visual.transform.localPosition = new Vector3(0, -low + .008f, 0);
    }
    /* 只归位姿态与剖切状态，不清空观察记录（对应 submit 里的 pose 重置） */
    public void ResetPose()
    {
        flip = yaw = tilt = target = rotated = 0;
        busy = false;
        cutShown = false;
        group.transform.localRotation = Quaternion.identity;
        visual.transform.localRotation = Quaternion.identity;
        body.SetActive(true); stem.SetActive(true); scar.SetActive(true); ring.SetActive(true);
        halves.SetActive(false);
        halfL.transform.localPosition = Vector3.zero; halfR.transform.localPosition = Vector3.zero;
        halfL.transform.localRotation = Quaternion.identity; halfR.transform.localRotation = Quaternion.identity;
        ApplyPose();
    }
    public void Reset()
    {
        if (info != null) info.observed = new bool[5];
        ResetPose();
    }
    public void ShowCut(bool show)
    {
        cutShown = show;
        body.SetActive(!show); stem.SetActive(!show); scar.SetActive(!show); ring.SetActive(!show);
        halves.SetActive(show);
        ApplyPose();
    }
    public Vector3 LabelAnchor() { return group.transform.position + new Vector3(0, 1.45f, 0); }
    public void Destroy()
    {
        if (group != null) UnityEngine.Object.Destroy(group);
        if (skin != null) UnityEngine.Object.Destroy(skin);
        if (cross != null) UnityEngine.Object.Destroy(cross);
        if (skinMat != null) UnityEngine.Object.Destroy(skinMat);
        if (capMat != null) UnityEngine.Object.Destroy(capMat);
    }
}

public class GuiliScene : MonoBehaviour
{
    public static GuiliScene Instance { get; private set; }
    public const float TableY = 1f;

    Camera bgCam, cam;
    GameObject world;
    Material woodMat, stemBaseMat, scarMat, ringMat, shadowMat;
    bool renderOn = true;
    readonly List<GuiliMelon> melons = new List<GuiliMelon>();
    public List<GuiliMelon> Melons { get { return melons; } }
    public GuiliMelon Active { get; set; }
    public bool Interactive { get; set; }
    public bool ZoomEnabled { get; set; }
    public Camera MainCamera { get { return cam; } }
    public bool OrbitMode { get; private set; }
    public bool Follow { get; private set; }
    public float Zoom { get; private set; }
    public Action<GuiliMelon> OnMelonClick;
    public Action<GuiliMelon, float, float> OnDragRotate;

    Vector3 camTarget = new Vector3(0, 7.6f, 8.6f);
    Vector3 target = new Vector3(0, 1, 0);
    Vector3 look = new Vector3(0, 1, 0);
    float orbitYaw, orbitPitch, zoom = 1f;
    Vector2 hover;
    RectTransform viewport;
    bool down;
    Vector2 lastMouse;
    float moved;
    float lastClickTime;
    Vector2 lastClickPos;

    public void Init(RectTransform viewportRect)
    {
        Instance = this;
        viewport = viewportRect;
        Follow = true;
        zoom = 1f;
        hover = Vector2.zero;

        /* 底色相机：纸色铺满，主相机只渲染中间视口 */
        GameObject bg = new GameObject("纸底相机");
        bgCam = bg.AddComponent<Camera>();
        bgCam.clearFlags = CameraClearFlags.SolidColor;
        bgCam.backgroundColor = GuiliData.Paper;
        bgCam.cullingMask = 0;
        bgCam.depth = -100;
        bgCam.orthographic = true;

        GameObject camGo = new GameObject("主摄像机");
        camGo.tag = "MainCamera";
        cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = GuiliData.Paper;
        cam.fieldOfView = 43f;
        cam.nearClipPlane = .1f;
        cam.farClipPlane = 80f;
        cam.depth = 0;
        cam.transform.position = camTarget;
        cam.transform.LookAt(look);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(1f, .965f, .863f);
        RenderSettings.fog = false;
        GameObject lightGo = new GameObject("暖色主光");
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, .961f, .851f);
        light.intensity = .55f;
        light.transform.position = new Vector3(-4, 8, 5);
        light.transform.LookAt(Vector3.zero);

        BuildTable();
        ApplyViewport();
    }

    Material Mat(string shaderName, Texture2D tex, Color color)
    {
        Shader sh = Shader.Find(shaderName);
        if (sh == null) sh = Shader.Find("Standard");
        Material m = new Material(sh);
        if (tex != null) m.mainTexture = tex;
        m.color = color;
        return m;
    }

    void BuildTable()
    {
        world = new GameObject("庞各庄瓜摊");
        woodMat = Mat("Legacy Shaders/Diffuse", GuiliTextures.MakeWood(), Color.white);
        stemBaseMat = Mat("Legacy Shaders/Diffuse", null, GuiliData.Hex("6b4f2a"));
        scarMat = Mat("Legacy Shaders/Diffuse", null, GuiliData.Hex("806a42"));
        ringMat = Mat("Legacy Shaders/Diffuse", null, GuiliData.Hex("b6a16b"));
        shadowMat = Mat("Legacy Shaders/Transparent/Diffuse", GuiliTextures.MakeShadow(), Color.white);

        GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cube);
        top.name = "木质展示桌";
        top.transform.SetParent(world.transform, false);
        top.transform.localPosition = new Vector3(0, .94f, 0);
        top.transform.localScale = new Vector3(6.8f, .12f, 5.6f);
        top.GetComponent<MeshRenderer>().sharedMaterial = woodMat;
        UnityEngine.Object.Destroy(top.GetComponent<Collider>());
        for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
        {
            GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leg.name = "桌腿";
            leg.transform.SetParent(world.transform, false);
            leg.transform.localPosition = new Vector3(x * 3f, .42f, z * 2.3f);
            leg.transform.localScale = new Vector3(.12f, 1f, .12f);
            leg.GetComponent<MeshRenderer>().sharedMaterial = woodMat;
            UnityEngine.Object.Destroy(leg.GetComponent<Collider>());
        }
    }

    /* 生成一颗完整西瓜（含剖切半球） */
    public GuiliMelon AddMelon(MelonInfo info, float radius, Vector3 pos)
    {
        GuiliMelon m = new GuiliMelon();
        m.info = info;
        m.radius = radius;
        m.home = pos;
        m.skin = GuiliTextures.MakeSkin(info);
        m.cross = GuiliTextures.MakeCrossSection(info.cut);
        m.skinMat = Mat("Legacy Shaders/Diffuse", m.skin, Color.white);
        m.skinMat.mainTextureScale = new Vector2(1, 1);
        Shader capSh = Shader.Find("Legacy Shaders/Transparent/Diffuse");
        if (capSh == null) capSh = Shader.Find("Standard");
        m.capMat = new Material(capSh);
        m.capMat.mainTexture = m.cross;
        m.capMat.color = Color.white;

        m.group = new GameObject("瓜_" + info.id);
        m.group.transform.position = pos;
        m.visual = new GameObject("瓜体朝向");
        m.visual.transform.SetParent(m.group.transform, false);

        /* 瓜体：球体略微椭化 */
        m.body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        m.body.name = "瓜体";
        m.body.transform.SetParent(m.visual.transform, false);
        /* Unity 内置 Sphere 半径为 0.5，需乘以 2r 才能等价于 THREE.SphereGeometry(r) */
        m.body.transform.localScale = new Vector3(1.06f, .96f, 1.06f) * (2f * radius);
        m.body.GetComponent<MeshRenderer>().sharedMaterial = m.skinMat;
        UnityEngine.Object.Destroy(m.body.GetComponent<Collider>());

        /* 瓜柄：纤维材质 + 弯曲管网格 + 蒂座 */
        bool dry = info.stem.Contains("干") || info.stem.Contains("褐");
        Texture2D fiber = MakeFiber(dry);
        Material fiberMat = Mat("Legacy Shaders/Diffuse", fiber, Color.white);
        m.stem = new GameObject("瓜柄");
        m.stem.transform.SetParent(m.visual.transform, false);
        m.stem.transform.localPosition = new Vector3(0, radius * .86f, 0);
        m.stem.transform.localScale = Vector3.one * (radius / .5f);
        GameObject baseCyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseCyl.name = "蒂座";
        baseCyl.transform.SetParent(m.stem.transform, false);
        baseCyl.transform.localPosition = new Vector3(0, .02f, 0);
        baseCyl.transform.localScale = new Vector3(.09f, .045f, .09f);
        baseCyl.GetComponent<MeshRenderer>().sharedMaterial = stemBaseMat;
        UnityEngine.Object.Destroy(baseCyl.GetComponent<Collider>());
        GameObject tube = new GameObject("弯曲瓜茎", typeof(MeshFilter), typeof(MeshRenderer));
        tube.transform.SetParent(m.stem.transform, false);
        tube.GetComponent<MeshFilter>().sharedMesh = GuiliMeshes.Stem(radius, info.stem);
        tube.GetComponent<MeshRenderer>().sharedMaterial = fiberMat;

        /* 底部花痕（瓜脐）与外环 */
        m.scar = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        m.scar.name = "花痕";
        m.scar.transform.SetParent(m.visual.transform, false);
        m.scar.transform.localPosition = new Vector3(0, -radius * .961f, 0);
        m.scar.transform.localScale = new Vector3(radius * .075f, radius * .075f * .22f, radius * .075f);
        m.scar.GetComponent<MeshRenderer>().sharedMaterial = scarMat;
        UnityEngine.Object.Destroy(m.scar.GetComponent<Collider>());
        m.ring = new GameObject("花痕环", typeof(MeshFilter), typeof(MeshRenderer));
        m.ring.transform.SetParent(m.visual.transform, false);
        m.ring.transform.localPosition = new Vector3(0, -radius * .963f, 0);
        m.ring.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        m.ring.GetComponent<MeshFilter>().sharedMesh = GuiliMeshes.Circle(radius * .08f, 24);
        m.ring.GetComponent<MeshRenderer>().sharedMaterial = ringMat;

        /* 剖切半球 */
        m.halves = new GameObject("两半瓜");
        m.halves.transform.SetParent(m.visual.transform, false);
        m.halfL = MakeHalf(m, -Mathf.PI / 2f, Mathf.PI / 2f, "左半球");
        m.halfR = MakeHalf(m, Mathf.PI / 2f, -Mathf.PI / 2f, "右半球");
        m.halves.SetActive(false);

        /* 接触阴影 */
        m.shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        m.shadow.name = "接触阴影";
        m.shadow.transform.SetParent(m.group.transform, false);
        m.shadow.transform.localPosition = new Vector3(0, .003f, 0);
        m.shadow.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        m.shadow.transform.localScale = new Vector3(radius * 2.8f, radius * 2.8f, 1);
        m.shadow.GetComponent<MeshRenderer>().sharedMaterial = shadowMat;
        UnityEngine.Object.Destroy(m.shadow.GetComponent<Collider>());

        m.ApplyPose();
        melons.Add(m);
        return m;
    }

    GameObject MakeHalf(GuiliMelon m, float phiStart, float capRotY, string name)
    {
        GameObject half = new GameObject(name);
        half.transform.SetParent(m.halves.transform, false);
        GameObject hbody = new GameObject("半球瓜皮", typeof(MeshFilter), typeof(MeshRenderer));
        hbody.transform.SetParent(half.transform, false);
        hbody.transform.localScale = new Vector3(1.06f, .96f, 1.06f) * m.radius;
        hbody.GetComponent<MeshFilter>().sharedMesh = GuiliMeshes.HalfSphere(1f, 48, 32, phiStart);
        hbody.GetComponent<MeshRenderer>().sharedMaterial = m.skinMat;
        GameObject cap = new GameObject("切面", typeof(MeshFilter), typeof(MeshRenderer));
        cap.transform.SetParent(half.transform, false);
        cap.transform.localScale = new Vector3(1.06f, .96f, 1) * m.radius * 1.02f;
        cap.transform.localRotation = Quaternion.Euler(0, capRotY * Mathf.Rad2Deg, 0);
        cap.GetComponent<MeshFilter>().sharedMesh = GuiliMeshes.Circle(1f, 48);
        cap.GetComponent<MeshRenderer>().sharedMaterial = m.capMat;
        return half;
    }

    Texture2D MakeFiber(bool dry)
    {
        PixelCanvas c = new PixelCanvas(64, 128, GuiliData.Hex(dry ? "94774e" : "5f7543"));
        for (int i = 0; i < 32; i++)
        {
            Color line = i % 3 == 0 ? new Color(.898f, .808f, .576f, .3f) : new Color(.153f, .157f, .078f, .18f);
            Vector2[] pts = new Vector2[9];
            for (int k = 0; k < 9; k++) pts[k] = new Vector2(i * 2f + Mathf.Sin(k * .7f) * 2f, k * 16f);
            c.Path(pts, line, 1f);
        }
        return c.ToTexture();
    }

    public void Remove(GuiliMelon m)
    {
        if (m == null) return;
        int i = melons.IndexOf(m);
        if (i >= 0) melons.RemoveAt(i);
        m.Destroy();
    }
    public void RemoveAll()
    {
        for (int i = 0; i < melons.Count; i++) melons[i].Destroy();
        melons.Clear();
        Active = null;
    }
    public void SetViewport(RectTransform rt)
    {
        viewport = rt;
        ApplyViewport();
    }

    /* ---------- 相机控制（对应 SceneMgr.CameraController） ---------- */
    public void ResetView()
    {
        orbitYaw = orbitPitch = 0;
        hover = Vector2.zero;
        zoom = 1f;
    }
    public void GoOverview() { ResetView(); camTarget.Set(0, 7.6f, 8.6f); target.Set(0, 1, 0); }
    public void GoTitle() { GoOverview(); }
    public void GoFocus(GuiliMelon m)
    {
        ResetView();
        Vector3 p = m.group.transform.position;
        camTarget.Set(p.x, 3.4f, p.z + 3.1f);
        target.Set(p.x, 1.6f, p.z);
    }
    public void GoStemClose(GuiliMelon m)
    {
        ResetView();
        Vector3 p = m.group.transform.position;
        camTarget.Set(p.x, 3.0f, p.z + 2.6f);
        target.Set(p.x, 1.65f, p.z);
    }
    public void GoCut()
    {
        ResetView();
        camTarget.Set(.3f, 3.3f, 3.7f);
        target.Set(.3f, 1.5f, 0);
    }
    public void SetOrbitMode(bool on) { OrbitMode = on; }
    public void SetFollow(bool on) { Follow = on; if (!on) hover = Vector2.zero; }
    public void SetZoom(float v) { zoom = Mathf.Clamp(v, .55f, 1.25f); }

    public void SetRenderEnabled(bool on)
    {
        renderOn = on;
        if (cam != null) cam.enabled = on;
    }

    public void ApplyViewport()
    {
        if (!renderOn || viewport == null || cam == null) return;
        if (!viewport.gameObject.activeInHierarchy) return;
        Vector3[] corners = new Vector3[4];
        viewport.GetWorldCorners(corners);
        float xMin = corners[0].x, yMin = corners[0].y;
        float xMax = corners[2].x, yMax = corners[2].y;
        // 3D 视口与 UI 内容区共用同一个 1920x1080 适配器
        cam.rect = WatermelonResolution.NormalizeScreenRect(
            new Vector2(xMin, yMin), new Vector2(xMax, yMax));
    }

    bool PointerInView()
    {
        // 屏幕坐标先经统一适配器换算，落在 16:9 内容区之外一律不响应拾取
        Vector2 d = WatermelonResolution.ScreenToDesign(Input.mousePosition);
        if (Mathf.Abs(d.x) > WatermelonResolution.DesignWidth * 0.5f ||
            Mathf.Abs(d.y) > WatermelonResolution.DesignHeight * 0.5f) return false;
        if (viewport == null) return true;
        return RectTransformUtility.RectangleContainsScreenPoint(viewport, Input.mousePosition, null);
    }
    bool PointerOnUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    void Update()
    {
        if (cam == null) return;
        ApplyViewport();
        float dt = Mathf.Min(Time.deltaTime, .05f);

        if (Interactive)
        {
            HandleInput();
            HandleKeys();
        }
        else if (down) down = false;

        /* Spherical 相机插值（与 atelier-scene.js frame() 一致） */
        Vector3 d = camTarget - target;
        float radius = d.magnitude;
        float theta = Mathf.Atan2(d.x, d.z);
        float phi = Mathf.Acos(Mathf.Clamp(d.y / Mathf.Max(.0001f, radius), -1, 1));
        bool interactive = Interactive;
        if (interactive)
        {
            radius *= zoom;
            theta += orbitYaw + (Follow ? hover.x : 0);
            phi = Mathf.Clamp(phi + orbitPitch + (Follow ? hover.y : 0), .28f, 1.38f);
        }
        float sp = Mathf.Sin(phi);
        Vector3 desired = new Vector3(radius * sp * Mathf.Sin(theta), radius * Mathf.Cos(phi), radius * sp * Mathf.Cos(theta)) + target;
        cam.transform.position = Vector3.Lerp(cam.transform.position, desired, 1 - Mathf.Exp(-5 * dt));
        look = Vector3.Lerp(look, target, 1 - Mathf.Exp(-7 * dt));
        cam.transform.LookAt(look);
        Zoom = zoom;
    }

    void HandleInput()
    {
        Vector2 mp = Input.mousePosition;
        if (!PointerInView())
        {
            if (!down) hover = Vector2.zero;
            return;
        }
        /* 滚轮缩放（弹窗打开时禁用，与网页 canZoom 一致） */
        bool modal = GuiliUI.Instance != null && GuiliUI.Instance.ModalOpen;
        float wheel = Input.mouseScrollDelta.y;
        if (!modal && ZoomEnabled && Mathf.Abs(wheel) > .001f && !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
        {
            float delta = Mathf.Clamp(wheel * 120f, -240f, 240f);
            SetZoom(zoom * Mathf.Exp(delta * .0015f));
        }
        /* 鼠标轻移 */
        if (!down && Follow && !PointerOnUI())
        {
            Vector3[] corners = new Vector3[4];
            viewport.GetWorldCorners(corners);
            float w = Mathf.Max(1, corners[2].x - corners[0].x), h = Mathf.Max(1, corners[2].y - corners[0].y);
            hover.Set(((mp.x - corners[0].x) / w - .5f) * .14f, ((mp.y - corners[0].y) / h - .5f) * .07f);
        }
        if (Input.GetMouseButtonDown(0) && !PointerOnUI())
        {
            down = true; lastMouse = mp; moved = 0;
        }
        if (down && Input.GetMouseButton(0))
        {
            float dx = mp.x - lastMouse.x, dy = mp.y - lastMouse.y;
            lastMouse = mp;
            moved += Mathf.Abs(dx) + Mathf.Abs(dy);
            if (Active != null && !OrbitMode && OnDragRotate != null)
                OnDragRotate(Active, dx, dy);
            else
            {
                orbitYaw = Mathf.Clamp(orbitYaw - dx * .004f, -1.1f, 1.1f);
                orbitPitch = Mathf.Clamp(orbitPitch + dy * .003f, -.28f, .25f);
            }
        }
        if (down && Input.GetMouseButtonUp(0))
        {
            down = false;
            /* 双击：在 1 与 0.6 之间切换 */
            if (!modal && ZoomEnabled && moved <= 7f && Time.unscaledTime - lastClickTime < .32f && Vector2.Distance(mp, lastClickPos) < 12f)
            {
                SetZoom(zoom < .8f ? 1f : .6f);
                lastClickTime = -1;
                return;
            }
            lastClickTime = Time.unscaledTime;
            lastClickPos = mp;
            if (moved <= 7f && !PointerOnUI()) PickMelon(mp);
        }
    }

    void HandleKeys()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow)) orbitYaw = Mathf.Clamp(orbitYaw - .08f, -1.1f, 1.1f);
        if (Input.GetKeyDown(KeyCode.RightArrow)) orbitYaw = Mathf.Clamp(orbitYaw + .08f, -1.1f, 1.1f);
        if (Input.GetKeyDown(KeyCode.UpArrow)) orbitPitch = Mathf.Clamp(orbitPitch - .05f, -.28f, .25f);
        if (Input.GetKeyDown(KeyCode.DownArrow)) orbitPitch = Mathf.Clamp(orbitPitch + .05f, -.28f, .25f);
    }

    /* 射线点选：手动 ray-sphere 相交，避免依赖碰撞体与物理层 */
    void PickMelon(Vector2 screenPoint)
    {
        if (GuiliUI.Instance != null && GuiliUI.Instance.ModalOpen) return;
        Ray ray = cam.ScreenPointToRay(screenPoint);
        float best = float.PositiveInfinity;
        GuiliMelon found = null;
        for (int i = 0; i < melons.Count; i++)
        {
            GuiliMelon m = melons[i];
            if (m.group == null || !m.group.activeInHierarchy) continue;
            Vector3 center = m.visual.transform.position;
            float R = m.radius * 1.12f;
            Vector3 oc = ray.origin - center;
            float b = Vector3.Dot(oc, ray.direction);
            float c = Vector3.Dot(oc, oc) - R * R;
            float disc = b * b - c;
            if (disc < 0) continue;
            float t = -b - Mathf.Sqrt(disc);
            if (t < 0) t = -b + Mathf.Sqrt(disc);
            if (t > 0 && t < best) { best = t; found = m; }
        }
        if (found != null && found != Active && OnMelonClick != null) OnMelonClick(found);
    }

    public bool RaycastFromScreen(Vector2 p, out Vector3 point)
    {
        Ray ray = cam.ScreenPointToRay(p);
        Plane plane = new Plane(Vector3.up, new Vector3(0, TableY, 0));
        float enter;
        if (plane.Raycast(ray, out enter)) { point = ray.GetPoint(enter); return true; }
        point = Vector3.zero;
        return false;
    }
}
