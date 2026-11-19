using System.Collections.Generic;
using UnityEngine;

namespace CozinhaExtra
{
    // Malhas geradas por código: prato (por revolução de um perfil), colisor e disco de líquido.
    internal static class MeshFactory
    {
        // Mathf.PI não existe no UnityEngine enxugado do jogo.
        private const float Pi = (float)System.Math.PI;
        public const float PlateHeight = 0.025f;
        public const float PlateRadius = 0.113f;
        private const int Segments = 40;

        // Corte do prato (raio, altura), percorrido de modo que o lado de fora fique à esquerda.
        private static readonly Vector2[] PlateProfile =
        {
            new Vector2(0f, 0.006f),
            new Vector2(0.066f, 0.006f),
            new Vector2(0.076f, 0.0085f),
            new Vector2(0.098f, 0.0215f),
            new Vector2(0.108f, 0.0245f),
            new Vector2(0.112f, 0.025f),
            new Vector2(0.113f, 0.0225f),
            new Vector2(0.106f, 0.019f),
            new Vector2(0.080f, 0.0045f),
            new Vector2(0.062f, 0.0015f),
            new Vector2(0.058f, 0f),
            new Vector2(0.045f, 0f),
            new Vector2(0.043f, 0.0015f),
            new Vector2(0f, 0.0015f)
        };

        private const float SoupRadius = 0.086f;
        private const float SoupHeight = 0.0135f;

        // Submalha 0 = louça; submalha 1 = sopa (se withSoup).
        public static Mesh Plate(float yOffset, bool withSoup)
        {
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> plate = new List<int>();

            for (int k = 0; k < PlateProfile.Length - 1; k++)
            {
                Vector2 a = PlateProfile[k];
                Vector2 b = PlateProfile[k + 1];
                Vector2 travel = b - a;
                Vector2 outside = new Vector2(-travel.y, travel.x);
                int start = verts.Count;
                for (int i = 0; i <= Segments; i++)
                {
                    float t = i / (float)Segments;
                    float ang = t * Pi * 2f;
                    float c = Mathf.Cos(ang);
                    float s = Mathf.Sin(ang);
                    verts.Add(new Vector3(a.x * c, a.y + yOffset, a.x * s));
                    verts.Add(new Vector3(b.x * c, b.y + yOffset, b.x * s));
                    uvs.Add(new Vector2(t, k / (float)PlateProfile.Length));
                    uvs.Add(new Vector2(t, (k + 1) / (float)PlateProfile.Length));
                }
                for (int i = 0; i < Segments; i++)
                {
                    int v00 = start + i * 2;
                    int v10 = v00 + 1;
                    int v01 = v00 + 2;
                    int v11 = v00 + 3;
                    float mid = (i + 0.5f) / Segments * Pi * 2f;
                    Vector3 desired = new Vector3(outside.x * Mathf.Cos(mid), outside.y, outside.x * Mathf.Sin(mid));
                    AddTriangle(plate, verts, v00, v10, v01, desired);
                    AddTriangle(plate, verts, v01, v10, v11, desired);
                }
            }

            List<int> soup = new List<int>();
            if (withSoup)
            {
                AddDisc(verts, uvs, soup, SoupRadius, SoupHeight + yOffset);
            }

            Mesh mesh = new Mesh { name = withSoup ? "CozinhaExtra_SoupPlate" : "CozinhaExtra_Plate" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = withSoup ? 2 : 1;
            mesh.SetTriangles(plate, 0);
            if (withSoup)
            {
                mesh.SetTriangles(soup, 1);
            }
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return mesh;
        }

        // Cilindro (colisor convexo do prato e da caneca; basta o contorno).
        public static Mesh Cylinder(float radius, float height, float yOffset)
        {
            const int n = 16;
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            for (int i = 0; i < n; i++)
            {
                float ang = i / (float)n * Pi * 2f;
                float x = Mathf.Cos(ang) * radius;
                float z = Mathf.Sin(ang) * radius;
                verts.Add(new Vector3(x, yOffset, z));
                verts.Add(new Vector3(x, yOffset + height, z));
            }
            for (int i = 0; i < n; i++)
            {
                int a = i * 2;
                int b = ((i + 1) % n) * 2;
                tris.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                if (i > 0 && i < n - 1)
                {
                    tris.AddRange(new[] { 0, b, a });
                    tris.AddRange(new[] { 1, a + 1, b + 1 });
                }
            }
            Mesh mesh = new Mesh { name = "CozinhaExtra_Collider" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return mesh;
        }

        // Disco de raio 1 virado para cima (superfície do líquido da panela, escalado depois).
        public static Mesh UnitDisc()
        {
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();
            AddDisc(verts, uvs, tris, 1f, 0f);
            Mesh mesh = new Mesh { name = "CozinhaExtra_LiquidDisc" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return mesh;
        }

        private static void AddDisc(List<Vector3> verts, List<Vector2> uvs, List<int> tris, float radius, float y)
        {
            int center = verts.Count;
            verts.Add(new Vector3(0f, y, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= Segments; i++)
            {
                float ang = i / (float)Segments * Pi * 2f;
                verts.Add(new Vector3(Mathf.Cos(ang) * radius, y, Mathf.Sin(ang) * radius));
                uvs.Add(new Vector2(0.5f + Mathf.Cos(ang) * 0.5f, 0.5f + Mathf.Sin(ang) * 0.5f));
            }
            for (int i = 0; i < Segments; i++)
            {
                AddTriangle(tris, verts, center, center + 1 + i, center + 2 + i, Vector3.up);
            }
        }

        // Ajusta a ordem dos vértices para a face da frente ficar do lado "desired" (convenção do Unity).
        private static void AddTriangle(List<int> tris, List<Vector3> verts, int a, int b, int c, Vector3 desired)
        {
            Vector3 n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (Vector3.Dot(n, desired) < 0f)
            {
                tris.Add(a);
                tris.Add(c);
                tris.Add(b);
            }
            else
            {
                tris.Add(a);
                tris.Add(b);
                tris.Add(c);
            }
        }
    }

    // Ícones da hotbar desenhados por código (prato visto de cima).
    internal static class IconFactory
    {
        private const int Size = 128;

        public static Sprite Plate(Color soup)
        {
            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "CozinhaExtra_PlateIcon", wrapMode = TextureWrapMode.Clamp };
            Color rim = new Color(0.80f, 0.79f, 0.75f, 1f);
            Color face = new Color(0.96f, 0.95f, 0.92f, 1f);
            Color well = new Color(0.90f, 0.89f, 0.85f, 1f);
            Color[] pixels = new Color[Size * Size];
            float c = (Size - 1) / 2f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    Color col = Color.Lerp(face, rim, Mathf.InverseLerp(52f, 60f, d));
                    if (d < 40f)
                    {
                        col = Color.Lerp(well, face, Mathf.InverseLerp(36f, 40f, d));
                    }
                    if (soup.a > 0f && d < 37f)
                    {
                        Color s = soup;
                        s.a = 1f;
                        // Leve brilho no canto superior esquerdo.
                        float shine = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), new Vector2(c - 12f, c + 12f)) / 30f) * 0.18f;
                        col = Color.Lerp(col, Color.Lerp(s, Color.white, shine), Mathf.Clamp01(37f - d));
                    }
                    col.a = Mathf.Clamp01(61f - d);
                    pixels[y * Size + x] = col;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = tex.name;
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }
    }
}
