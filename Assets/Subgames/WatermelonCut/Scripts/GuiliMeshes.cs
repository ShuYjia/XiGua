using System.Collections.Generic;
using UnityEngine;

/* ============================================================
 * GuiliMeshes.cs —— Unity 端程序化「生模型」
 * 对应网页 js/watermelon.js 的 SphereGeometry(phiStart, phiLength)、
 * CircleGeometry、TubeGeometry(CatmullRomCurve3)。
 * 网格在运行时生成，并由 Editor/GuiliProjectSetup.cs 导出为 .asset 模型资产。
 * 所有表面均生成正反两组三角形（双面可见），避免翻底 / 剖切时背面消失。
 * ============================================================ */

public static class GuiliMeshes
{
    /* 半球：phiStart = -PI/2 为左半（x<=0），PI/2 为右半（x>=0） */
    public static Mesh HalfSphere(float r, int segU, int segV, float phiStart)
    {
        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<Vector3> normals = new List<Vector3>();
        List<int> tris = new List<int>();

        for (int v = 0; v <= segV; v++)
        {
            float theta = (float)v / segV * Mathf.PI;
            for (int u = 0; u <= segU; u++)
            {
                float phi = phiStart + (float)u / segU * Mathf.PI;
                float st = Mathf.Sin(theta), ct = Mathf.Cos(theta);
                Vector3 p = new Vector3(-r * Mathf.Cos(phi) * st, r * ct, r * Mathf.Sin(phi) * st);
                verts.Add(p);
                normals.Add(p.normalized);
                uvs.Add(new Vector2((float)u / segU, 1f - (float)v / segV));
            }
        }
        int stride = segU + 1;
        for (int v = 0; v < segV; v++)
        {
            for (int u = 0; u < segU; u++)
            {
                int a = v * stride + u;
                int b = a + 1;
                int c = a + stride;
                int d = c + 1;
                AddQuad(tris, a, b, c, d);
            }
        }
        Mesh mesh = new Mesh();
        mesh.name = "WatermelonHalf_" + (phiStart < 0 ? "L" : "R");
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /* 切面封盖：XY 平面圆盘，法线 +Z */
    public static Mesh Circle(float r, int seg)
    {
        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<Vector3> normals = new List<Vector3>();
        List<int> tris = new List<int>();
        verts.Add(Vector3.zero); uvs.Add(new Vector2(.5f, .5f)); normals.Add(Vector3.forward);
        for (int i = 0; i <= seg; i++)
        {
            float a = (float)i / seg * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0);
            verts.Add(p);
            uvs.Add(new Vector2((p.x / r + 1) * .5f, (p.y / r + 1) * .5f));
            normals.Add(Vector3.forward);
        }
        for (int i = 1; i <= seg; i++)
        {
            tris.Add(0); tris.Add(i); tris.Add(i + 1);
            tris.Add(0); tris.Add(i + 1); tris.Add(i);
        }
        Mesh mesh = new Mesh();
        mesh.name = "CrossSectionCap";
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /* Catmull-Rom 曲线管（瓜柄），control 为控制点 */
    public static Mesh Tube(Vector3[] control, float radius, int radial, int samples)
    {
        List<Vector3> path = new List<Vector3>();
        int n = control.Length - 1;
        for (int i = 0; i < n; i++)
        {
            Vector3 p0 = control[Mathf.Max(0, i - 1)];
            Vector3 p1 = control[i];
            Vector3 p2 = control[i + 1];
            Vector3 p3 = control[Mathf.Min(control.Length - 1, i + 2)];
            int steps = Mathf.Max(2, samples / n);
            for (int s = 0; s < steps; s++)
            {
                float t = (float)s / steps;
                path.Add(.5f * ((2 * p1) + (-p0 + p2) * t +
                    (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t +
                    (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t));
            }
        }
        path.Add(control[control.Length - 1]);

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 tangent = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
            Vector3 side = Vector3.Cross(tangent, Vector3.up);
            if (side.sqrMagnitude < .0001f) side = Vector3.Cross(tangent, Vector3.forward);
            side.Normalize();
            Vector3 up = Vector3.Cross(side, tangent).normalized;
            for (int j = 0; j <= radial; j++)
            {
                float a = (float)j / radial * Mathf.PI * 2f;
                Vector3 dir = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                verts.Add(path[i] + dir * radius);
                normals.Add(dir);
                uvs.Add(new Vector2((float)j / radial, (float)i / (path.Count - 1)));
            }
        }
        int stride = radial + 1;
        for (int i = 0; i < path.Count - 1; i++)
        {
            for (int j = 0; j < radial; j++)
            {
                int a = i * stride + j;
                int b = a + 1;
                int c = a + stride;
                int d = c + 1;
                AddQuad(tris, a, b, c, d);
            }
        }
        Mesh mesh = new Mesh();
        mesh.name = "WatermelonStem";
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /* 网页 makeStem 的弯茎形状：鲜绿偏直，干褐/成熟偏卷 */
    public static Mesh Stem(float r, string stemState)
    {
        bool dry = stemState.Contains("干") || stemState.Contains("褐");
        bool straight = stemState.Contains("直");
        float bend = straight ? .02f : (dry ? .11f : .07f);
        float height = straight ? .23f : .18f;
        Vector3[] control = {
            new Vector3(0, .04f, 0),
            new Vector3(.02f, .14f, .01f),
            new Vector3(bend * .65f, .18f, -.02f),
            new Vector3(bend, straight ? height : .14f, -.05f)
        };
        return Tube(control, .016f, 8, 16);
    }

    /* 双面四边形：a,b 为上行，c,d 为下行 */
    static void AddQuad(List<int> tris, int a, int b, int c, int d)
    {
        tris.Add(a); tris.Add(c); tris.Add(b);
        tris.Add(b); tris.Add(c); tris.Add(d);
        tris.Add(a); tris.Add(b); tris.Add(c);
        tris.Add(b); tris.Add(d); tris.Add(c);
    }
}
