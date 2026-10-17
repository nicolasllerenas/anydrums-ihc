using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AnyDrums.EditorTools
{
    /// <summary>
    /// Genera los sprites del menú (formas redondeadas, brillos e íconos) y la textura del piso
    /// dibujándolos con funciones de distancia, para no depender de imágenes externas.
    /// </summary>
    public static class MenuArtGenerator
    {
        public const string Folder = "Assets/AnyDrums/Art/Generated";

        // Radio de las formas redondeadas "base". Las Image usan pixelsPerUnitMultiplier = BaseRadius / radioDeseado.
        public const int BaseRadius = 64;

        public static string PathOf(string name) => $"{Folder}/{name}.png";

        public static void GenerateAll()
        {
            Directory.CreateDirectory(Folder);

            SaveSliced("round_fill", RoundedRect(BaseRadius, 0f), BaseRadius + 1);
            // Bordes dibujados al doble de resolución (usar pixelsPerUnitMultiplier = 2).
            SaveSliced("outline_panel", RoundedRect(88, 4f), 89);
            SaveSliced("outline_card", RoundedRect(56, 6f), 57);
            SaveSliced("outline_pill", RoundedRect(100, 5f), 101);
            SaveSliced("card_glow", SoftRoundedRect(28, 36), 28 + 36 + 1);
            SaveSprite("circle_fill", Draw(132, (p, c) => Sdf.Circle(p, c, 64f)));
            SaveSprite("circle_outline", Draw(132, (p, c) => Sdf.Ring(p, c, 61f, 5f)));
            SaveSprite("glow", RadialGlow(256));

            SaveSprite("icon_acoustic", Draw(256, AcousticIcon));
            SaveSprite("icon_electronic", Draw(256, ElectronicIcon));
            SaveSprite("icon_sticks", Draw(256, SticksIcon));
            SaveSprite("icon_volume_mute", Draw(256, (p, c) => Mathf.Min(SpeakerBody(p),
                Mathf.Min(Sdf.Segment(p, new Vector2(166, 100), new Vector2(222, 156), 9f),
                          Sdf.Segment(p, new Vector2(166, 156), new Vector2(222, 100), 9f)))));
            SaveSprite("icon_volume_low", Draw(256, (p, c) => Mathf.Min(SpeakerBody(p),
                Sdf.Arc(p, new Vector2(132, 128), 50f, 17f, -50f, 50f))));
            SaveSprite("icon_volume_high", Draw(256, (p, c) => Mathf.Min(SpeakerBody(p),
                Mathf.Min(Sdf.Arc(p, new Vector2(132, 128), 50f, 17f, -50f, 50f),
                          Sdf.Arc(p, new Vector2(132, 128), 90f, 17f, -50f, 50f)))));
            SaveSprite("icon_check", Draw(256, (p, c) => Mathf.Min(
                Sdf.Segment(p, new Vector2(66, 132), new Vector2(108, 90), 15f),
                Sdf.Segment(p, new Vector2(108, 90), new Vector2(194, 176), 15f))));
            SaveSprite("icon_arrow", Draw(256, (p, c) => Mathf.Min(
                Sdf.Segment(p, new Vector2(52, 128), new Vector2(200, 128), 13f),
                Mathf.Min(Sdf.Segment(p, new Vector2(140, 68), new Vector2(200, 128), 13f),
                          Sdf.Segment(p, new Vector2(140, 188), new Vector2(200, 128), 13f)))));
            SaveSprite("icon_power", Draw(256, (p, c) => Mathf.Min(
                Sdf.Arc(p, new Vector2(128, 116), 80f, 20f, 125f, 415f),
                Sdf.Segment(p, new Vector2(128, 128), new Vector2(128, 222), 10f))));
            SaveSprite("icon_minus", Draw(256, (p, c) => Sdf.Segment(p, new Vector2(66, 128), new Vector2(190, 128), 13f)));
            SaveSprite("icon_plus", Draw(256, (p, c) => Mathf.Min(
                Sdf.Segment(p, new Vector2(66, 128), new Vector2(190, 128), 13f),
                Sdf.Segment(p, new Vector2(128, 66), new Vector2(128, 190), 13f))));

            SaveFloorTexture("floor_grid", 1024, 14f);
            AssetDatabase.Refresh();
        }

        // ---------- Íconos ----------

        // Caja (vista lateral) con dos baquetas cruzadas golpeando el parche.
        static float AcousticIcon(Vector2 p, Vector2 c)
        {
            const float w = 14f;
            float d = Sdf.EllipseRing(p, new Vector2(128, 146), new Vector2(80, 26), w);
            d = Mathf.Min(d, Sdf.Segment(p, new Vector2(48, 146), new Vector2(48, 84), w / 2));
            d = Mathf.Min(d, Sdf.Segment(p, new Vector2(208, 146), new Vector2(208, 84), w / 2));
            // Mitad inferior del aro de abajo.
            var bottomCenter = new Vector2(128, 84);
            d = Mathf.Min(d, p.y <= bottomCenter.y
                ? Sdf.EllipseRing(p, bottomCenter, new Vector2(80, 26), w)
                : Mathf.Min((p - new Vector2(48, 84)).magnitude, (p - new Vector2(208, 84)).magnitude) - w / 2);
            // Tensores.
            foreach (float x in new[] { 84f, 128f, 172f })
                d = Mathf.Min(d, Sdf.Segment(p, new Vector2(x, 112), new Vector2(x, 70), 5f));
            // Baquetas.
            d = Mathf.Min(d, Sdf.Segment(p, new Vector2(58, 240), new Vector2(148, 176), 8f));
            d = Mathf.Min(d, Sdf.Segment(p, new Vector2(198, 240), new Vector2(108, 176), 8f));
            d = Mathf.Min(d, Sdf.Circle(p, new Vector2(148, 176), 11f));
            d = Mathf.Min(d, Sdf.Circle(p, new Vector2(108, 176), 11f));
            return d;
        }

        // Caja de ritmos: pantalla, perillas y una grilla de pads.
        static float ElectronicIcon(Vector2 p, Vector2 c)
        {
            float d = Sdf.RoundBoxOutline(p, new Vector2(128, 128), new Vector2(104, 96), 26f, 14f);
            d = Mathf.Min(d, Sdf.RoundBox(p, new Vector2(100, 186), new Vector2(46, 11), 6f));
            d = Mathf.Min(d, Sdf.Circle(p, new Vector2(172, 186), 10f));
            d = Mathf.Min(d, Sdf.Circle(p, new Vector2(200, 186), 10f));
            for (int col = 0; col < 4; col++)
            for (int row = 0; row < 3; row++)
                d = Mathf.Min(d, Sdf.RoundBox(p, new Vector2(68 + col * 40, 61 + row * 40), new Vector2(15, 15), 6f));
            return d;
        }

        static float SticksIcon(Vector2 p, Vector2 c)
        {
            float d = Sdf.Segment(p, new Vector2(62, 46), new Vector2(194, 206), 11f);
            d = Mathf.Min(d, Sdf.Segment(p, new Vector2(194, 46), new Vector2(62, 206), 11f));
            d = Mathf.Min(d, Sdf.Circle(p, new Vector2(194, 206), 17f));
            d = Mathf.Min(d, Sdf.Circle(p, new Vector2(62, 206), 17f));
            return d;
        }

        static float SpeakerBody(Vector2 p)
        {
            var shape = new[]
            {
                new Vector2(40, 102), new Vector2(80, 102), new Vector2(124, 60),
                new Vector2(124, 196), new Vector2(80, 154), new Vector2(40, 154)
            };
            return Sdf.Polygon(p, shape) - 5f;
        }

        // ---------- Formas base ----------

        static Color[] RoundedRect(int radius, float stroke)
        {
            int size = radius * 2 + 4;
            var center = new Vector2(size / 2f, size / 2f);
            var half = new Vector2(size / 2f - 1f, size / 2f - 1f);
            return Draw(size, (p, c) =>
            {
                float d = Sdf.RoundBox(p, center, half, radius);
                return stroke <= 0f ? d : Mathf.Abs(d + stroke / 2f) - stroke / 2f;
            });
        }

        static Color[] SoftRoundedRect(int radius, int blur)
        {
            int size = (radius + blur) * 2 + 4;
            var center = new Vector2(size / 2f, size / 2f);
            var half = new Vector2(radius + 1f, radius + 1f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Max(0f, Sdf.RoundBox(new Vector2(x + 0.5f, y + 0.5f), center, half, radius));
                float a = Mathf.Exp(-d * d / (2f * (blur / 2.6f) * (blur / 2.6f)));
                a *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(blur * 0.8f, blur, d));
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            return pixels;
        }

        static Color[] RadialGlow(int size)
        {
            var pixels = new Color[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude / half;
                float a = Mathf.Exp(-r * r * 4.2f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.82f, 1f, r)));
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            return pixels;
        }

        static Color[] Draw(int size, Func<Vector2, Vector2, float> sdf)
        {
            var pixels = new Color[size * size];
            var center = new Vector2(size / 2f, size / 2f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = sdf(new Vector2(x + 0.5f, y + 0.5f), center);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
            }
            return pixels;
        }

        // Piso oscuro con una grilla tenue cada 1 m que se desvanece con la distancia,
        // un círculo suave donde está parado el usuario y bordes del mismo color que el fondo.
        static void SaveFloorTexture(string name, int size, float worldSize)
        {
            var background = MenuPalette.Background;
            var center = new Color32(0x16, 0x19, 0x22, 0xFF);
            float metersPerPixel = worldSize / size;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var m = new Vector2((x + 0.5f) * metersPerPixel - worldSize / 2f, (y + 0.5f) * metersPerPixel - worldSize / 2f);
                float r = m.magnitude;
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2f, 6.6f, r));
                var color = Color.Lerp(background, center, Mathf.Exp(-r * r / 6f) * 0.9f);

                float gx = Mathf.Abs(m.x - Mathf.Round(m.x)) / metersPerPixel;
                float gy = Mathf.Abs(m.y - Mathf.Round(m.y)) / metersPerPixel;
                float line = Mathf.Clamp01(1.1f - Mathf.Min(gx, gy));
                color = Color.Lerp(color, Color.white, line * 0.07f * fade);

                float ring = Mathf.Clamp01(1.2f - Mathf.Abs(r - 0.42f) / metersPerPixel);
                color = Color.Lerp(color, Color.white, ring * 0.16f);

                color.a = 1f;
                pixels[y * size + x] = color;
            }
            string path = PathOf(name);
            WritePng(path, size, pixels);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 8;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        // ---------- Guardado ----------

        static void SaveSprite(string name, Color[] pixels) => SaveSliced(name, pixels, 0);

        static void SaveSliced(string name, Color[] pixels, int border)
        {
            int size = Mathf.RoundToInt(Mathf.Sqrt(pixels.Length));
            string path = PathOf(name);
            WritePng(path, size, pixels);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        static void WritePng(string path, int size, Color[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        /// <summary>Funciones de distancia con signo (negativo = dentro de la forma), en píxeles.</summary>
        static class Sdf
        {
            public static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

            public static float Ring(Vector2 p, Vector2 c, float r, float width) => Mathf.Abs((p - c).magnitude - r) - width / 2f;

            public static float Segment(Vector2 p, Vector2 a, Vector2 b, float radius)
            {
                Vector2 pa = p - a, ba = b - a;
                float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
                return (pa - ba * h).magnitude - radius;
            }

            public static float RoundBox(Vector2 p, Vector2 c, Vector2 half, float r)
            {
                var q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + new Vector2(r, r);
                return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
            }

            public static float RoundBoxOutline(Vector2 p, Vector2 c, Vector2 half, float r, float width)
                => Mathf.Abs(RoundBox(p, c, half, r)) - width / 2f;

            // Aproximación de primer orden de la distancia a una elipse, suficiente para trazos.
            public static float EllipseRing(Vector2 p, Vector2 c, Vector2 radii, float width)
            {
                Vector2 q = p - c;
                var k = new Vector2(q.x / radii.x, q.y / radii.y);
                float len = k.magnitude;
                if (len < 1e-4f) return Mathf.Min(radii.x, radii.y) - width / 2f;
                var gradient = new Vector2(q.x / (radii.x * radii.x), q.y / (radii.y * radii.y)) / len;
                return Mathf.Abs(len - 1f) / gradient.magnitude - width / 2f;
            }

            // Arco entre dos ángulos (en grados, sentido antihorario) con puntas redondeadas.
            public static float Arc(Vector2 p, Vector2 c, float r, float width, float fromDeg, float toDeg)
            {
                Vector2 q = p - c;
                float angle = Mathf.Atan2(q.y, q.x) * Mathf.Rad2Deg;
                while (angle < fromDeg) angle += 360f;
                while (angle >= fromDeg + 360f) angle -= 360f;
                if (angle <= toDeg) return Mathf.Abs(q.magnitude - r) - width / 2f;
                Vector2 a = c + new Vector2(Mathf.Cos(fromDeg * Mathf.Deg2Rad), Mathf.Sin(fromDeg * Mathf.Deg2Rad)) * r;
                Vector2 b = c + new Vector2(Mathf.Cos(toDeg * Mathf.Deg2Rad), Mathf.Sin(toDeg * Mathf.Deg2Rad)) * r;
                return Mathf.Min((p - a).magnitude, (p - b).magnitude) - width / 2f;
            }

            public static float Polygon(Vector2 p, Vector2[] v)
            {
                float d = Vector2.Dot(p - v[0], p - v[0]);
                float s = 1f;
                for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
                {
                    Vector2 e = v[j] - v[i], w = p - v[i];
                    Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                    d = Mathf.Min(d, Vector2.Dot(b, b));
                    bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                    if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
                }
                return s * Mathf.Sqrt(d);
            }
        }
    }
}
