using System.Collections.Generic;
using UnityEngine;

namespace CozinhaExtra
{
    internal static partial class Items
    {
        private static Texture2D _flatNormal;
        private static readonly Dictionary<int, Texture2D> MaskBySmoothness = new Dictionary<int, Texture2D>();

        // Cópia do material HDRP Lit da lata com cor lisa. As keywords ficam como no original
        // (o build do jogo pode não ter as variantes sem mapa); só as texturas viram neutras.
        public static Material MakeMaterial(Material source, Color color, float smoothness, string name)
        {
            Material m = source != null ? new Material(source) : new Material(Shader.Find("HDRP/Lit") ?? Shader.Find("Standard"));
            m.name = name;
            m.hideFlags = HideFlags.DontUnloadUnusedAsset;
            SetColor(m, color);
            SetTexture(m, "_BaseColorMap", Texture2D.whiteTexture);
            SetTexture(m, "_MainTex", Texture2D.whiteTexture);
            // Mask map do HDRP: R metálico, G oclusão, B máscara de detalhe, A lisura.
            SetTexture(m, "_MaskMap", MaskTexture(smoothness));
            SetTexture(m, "_NormalMap", FlatNormal());
            SetTexture(m, "_DetailMap", null);
            SetTexture(m, "_EmissiveColorMap", null);
            SetFloat(m, "_Metallic", 0f);
            SetFloat(m, "_Smoothness", smoothness);
            SetFloat(m, "_MetallicRemapMin", 0f);
            SetFloat(m, "_MetallicRemapMax", 1f);
            SetFloat(m, "_SmoothnessRemapMin", 0f);
            SetFloat(m, "_SmoothnessRemapMax", 1f);
            SetFloat(m, "_AORemapMin", 0f);
            SetFloat(m, "_AORemapMax", 1f);
            SetFloat(m, "_NormalScale", 1f);
            if (m.HasProperty("_EmissiveColor"))
            {
                m.SetColor("_EmissiveColor", Color.black);
            }
            return m;
        }

        public static void SetColor(Material m, Color color)
        {
            if (m.HasProperty("_BaseColor"))
            {
                m.SetColor("_BaseColor", color);
            }
            if (m.HasProperty("_Color"))
            {
                m.SetColor("_Color", color);
            }
        }

        private static void SetTexture(Material m, string prop, Texture tex)
        {
            if (m.HasProperty(prop))
            {
                m.SetTexture(prop, tex);
            }
        }

        private static void SetFloat(Material m, string prop, float value)
        {
            if (m.HasProperty(prop))
            {
                m.SetFloat(prop, value);
            }
        }

        private static Texture2D SolidTexture(string name, Color color)
        {
            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false, true) { name = name };
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return tex;
        }

        // Normal "reta": o HDRP lê x = R * A e y = G, então (0.5, 0.5, 1, 1) dá a normal (0, 0, 1).
        private static Texture2D FlatNormal()
        {
            if (_flatNormal == null)
            {
                _flatNormal = SolidTexture("CozinhaExtra_FlatNormal", new Color(0.5f, 0.5f, 1f, 1f));
            }
            return _flatNormal;
        }

        private static Texture2D MaskTexture(float smoothness)
        {
            int key = Mathf.RoundToInt(smoothness * 100f);
            if (!MaskBySmoothness.TryGetValue(key, out Texture2D tex) || tex == null)
            {
                tex = SolidTexture("CozinhaExtra_Mask" + key, new Color(0f, 1f, 0f, Mathf.Clamp01(smoothness)));
                MaskBySmoothness[key] = tex;
            }
            return tex;
        }
    }
}
