using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AnyDrums.EditorTools
{
    /// <summary>
    /// Genera los bancos de samples acústico y electrónico (10 piezas cada uno) como archivos WAV,
    /// crea los assets <see cref="DrumSampleBank"/> y el <see cref="DrumKitCatalog"/> en Resources.
    /// Son sonidos sintetizados de respaldo: si un campo del banco ya tiene otro clip (por ejemplo, los samples
    /// del informe), no se reemplaza.
    /// </summary>
    public static class DrumSampleSynth
    {
        const string AudioFolder = "Assets/AnyDrums/Audio";
        const string CatalogPath = "Assets/AnyDrums/Resources/AnyDrums/DrumKitCatalog.asset";
        const int Rate = 44100;

        [MenuItem("AnyDrums/Generar bancos de samples", priority = 10)]
        public static void GenerateAll()
        {
            var acoustic = BuildBank(DrumKitType.Acoustic, "Batería acústica", "Acoustic");
            var electronic = BuildBank(DrumKitType.Electronic, "Batería electrónica", "Electronic");

            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
            AssetDatabase.Refresh();
            var catalog = AssetDatabase.LoadAssetAtPath<DrumKitCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<DrumKitCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var so = new SerializedObject(catalog);
            so.FindProperty("_acousticBank").objectReferenceValue = acoustic;
            so.FindProperty("_electronicBank").objectReferenceValue = electronic;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[AnyDrums] Bancos de samples generados.");
        }

        static DrumSampleBank BuildBank(DrumKitType kit, string displayName, string folderName)
        {
            string folder = $"{AudioFolder}/{folderName}";
            Directory.CreateDirectory(folder);
            bool electronic = kit == DrumKitType.Electronic;

            string bankPath = $"{AudioFolder}/Bank_{folderName}.asset";
            var bank = AssetDatabase.LoadAssetAtPath<DrumSampleBank>(bankPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<DrumSampleBank>();
                AssetDatabase.CreateAsset(bank, bankPath);
            }

            var so = new SerializedObject(bank);
            so.FindProperty("_kit").enumValueIndex = (int)kit;
            so.FindProperty("_displayName").stringValue = displayName;
            foreach (DrumPiece piece in Enum.GetValues(typeof(DrumPiece)))
            {
                string clipPath = $"{folder}/{folderName.ToLowerInvariant()}_{piece.ToString().ToLowerInvariant()}.wav";
                WriteWav(clipPath, electronic ? Electronic(piece) : Acoustic(piece));
                AssetDatabase.ImportAsset(clipPath, ImportAssetOptions.ForceSynchronousImport);
                ConfigureImporter(clipPath);

                var property = so.FindProperty(FieldName(piece));
                var current = property.objectReferenceValue;
                // Solo se asigna si está vacío o si apunta a un sample generado por este script.
                if (current == null || AssetDatabase.GetAssetPath(current) == clipPath)
                    property.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bank);
            return bank;
        }

        static string FieldName(DrumPiece piece)
        {
            string name = piece.ToString();
            return "_" + char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        static void ConfigureImporter(string path)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = true;
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        // ---------- Batería acústica ----------

        static float[] Acoustic(DrumPiece piece)
        {
            switch (piece)
            {
                case DrumPiece.Kick:
                    return Render(0.7f, 0.95f, (t, n, s) =>
                    {
                        s.Phase += 2f * Mathf.PI * (48f + 75f * Mathf.Exp(-t / 0.035f)) / Rate;
                        float body = Mathf.Sin(s.Phase) * Mathf.Exp(-t / 0.28f);
                        float click = s.Lp1.LowPass(n, 4000f) * Mathf.Exp(-t / 0.003f) * 0.6f;
                        float beater = Mathf.Sin(2f * Mathf.PI * 1800f * t) * Mathf.Exp(-t / 0.004f) * 0.2f;
                        return Tanh(1.4f * (body + click + beater));
                    });
                case DrumPiece.Snare:
                    return Room(Render(0.6f, 0.9f, (t, n, s) =>
                    {
                        float body = 0.6f * Mathf.Sin(2f * Mathf.PI * 186f * t) * Mathf.Exp(-t / 0.07f)
                                   + 0.3f * Mathf.Sin(2f * Mathf.PI * 334f * t) * Mathf.Exp(-t / 0.04f);
                        float wires = s.Lp2.LowPass(n - s.Lp1.LowPass(n, 1400f), 7500f) * Mathf.Exp(-t / 0.17f);
                        return body + 0.9f * wires + 0.4f * n * Mathf.Exp(-t / 0.002f);
                    }), 0.9f);
                case DrumPiece.HiHatClosed: return HiHat(0.25f, 0.03f, 1f, noise: 0.5f, peak: 0.85f);
                case DrumPiece.HiHatOpen: return HiHat(1.1f, 0.32f, 1f, noise: 0.5f, peak: 0.85f);
                case DrumPiece.HiHatPedal:
                    return Render(0.18f, 0.8f, (t, n, s) =>
                        HighPass(s, Metallic(t, 1f), 7000f) * Mathf.Exp(-t / 0.018f)
                        + 0.3f * Mathf.Sin(2f * Mathf.PI * 180f * t) * Mathf.Exp(-t / 0.01f));
                case DrumPiece.Tom1: return Room(AcousticTom(0.9f, 196f, 60f, 0.28f), 0.9f);
                case DrumPiece.Tom2: return Room(AcousticTom(1.0f, 147f, 45f, 0.33f), 0.9f);
                case DrumPiece.FloorTom: return Room(AcousticTom(1.3f, 98f, 30f, 0.45f), 0.9f);
                case DrumPiece.Crash:
                    return Render(2.6f, 0.8f, (t, n, s) =>
                    {
                        float shimmer = 0f;
                        for (int i = 0; i < CymbalPartials.Length; i++)
                            shimmer += Mathf.Sin(2f * Mathf.PI * CymbalPartials[i] * t + i) * (1f / (1 + i % 4));
                        float wash = s.Lp2.LowPass(n - s.Lp1.LowPass(n, 4000f), 12000f);
                        float metal = HighPass(s, Metallic(t, 2.3f), 5000f);
                        float env = Attack(t, 0.002f) * Mathf.Exp(-t / 0.9f);
                        return env * (0.7f * wash + 0.35f * metal + 0.08f * shimmer) + n * Mathf.Exp(-t / 0.04f) * 0.4f;
                    });
                case DrumPiece.Ride:
                    return Render(2.6f, 0.8f, (t, n, s) =>
                    {
                        float bell = 0.32f * Mathf.Sin(2f * Mathf.PI * 580f * t) + 0.24f * Mathf.Sin(2f * Mathf.PI * 1250f * t)
                                   + 0.18f * Mathf.Sin(2f * Mathf.PI * 2110f * t) + 0.12f * Mathf.Sin(2f * Mathf.PI * 3330f * t);
                        float metal = HighPass(s, Metallic(t, 3.1f), 6000f);
                        float tick = (n - s.Lp1.LowPass(n, 6000f)) * Mathf.Exp(-t / 0.004f);
                        return bell * Mathf.Exp(-t / 1.4f) + 0.35f * metal * Mathf.Exp(-t / 1.1f) + 0.5f * tick;
                    });
                default: return new float[1];
            }
        }

        static float[] AcousticTom(float seconds, float pitch, float bend, float decay) =>
            Render(seconds, 0.9f, (t, n, s) =>
            {
                s.Phase += 2f * Mathf.PI * (pitch + bend * Mathf.Exp(-t / 0.06f)) / Rate;
                float body = Mathf.Sin(s.Phase) * Mathf.Exp(-t / decay) + 0.25f * Mathf.Sin(s.Phase * 1.5f) * Mathf.Exp(-t / (decay * 0.4f));
                float attack = s.Lp1.LowPass(n, 3000f) * Mathf.Exp(-t / 0.006f) * 0.4f;
                return Tanh(1.2f * (body + attack));
            });

        static float[] HiHat(float seconds, float decay, float scale, float noise, float peak) =>
            Render(seconds, peak, (t, n, s) =>
            {
                float metal = HighPass(s, Metallic(t, scale), 7000f);
                float hiss = n - s.Lp2.LowPass(n, 8000f);
                return (0.7f * metal + noise * hiss) * Attack(t, 0.001f) * Mathf.Exp(-t / decay);
            });

        // ---------- Batería electrónica (caja de ritmos) ----------

        static float[] Electronic(DrumPiece piece)
        {
            switch (piece)
            {
                case DrumPiece.Kick:
                    return Render(1.3f, 0.95f, (t, n, s) =>
                    {
                        s.Phase += 2f * Mathf.PI * (50f + 110f * Mathf.Exp(-t / 0.045f)) / Rate;
                        float body = Mathf.Sin(s.Phase) * Mathf.Exp(-t / 0.55f);
                        float click = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 900f * t)) * Mathf.Exp(-t / 0.003f) * 0.25f;
                        return Tanh(1.8f * (body + click));
                    });
                case DrumPiece.Snare:
                    return Render(0.35f, 0.9f, (t, n, s) =>
                    {
                        s.Phase += 2f * Mathf.PI * (175f + 140f * Mathf.Exp(-t / 0.018f)) / Rate;
                        float tone = (Mathf.Sin(s.Phase) + 0.35f * Mathf.Sin(s.Phase * 1.83f)) * Mathf.Exp(-t / 0.06f);
                        float snap = (n - s.Lp1.LowPass(n, 3200f)) * Mathf.Exp(-t / 0.075f);
                        float gate = t < 0.2f ? 1f : Mathf.Clamp01(1f - (t - 0.2f) / 0.01f);
                        return (0.7f * tone + 1.1f * snap) * gate;
                    });
                case DrumPiece.HiHatClosed: return HiHat(0.15f, 0.022f, 1f, noise: 0f, peak: 0.8f);
                case DrumPiece.HiHatOpen: return HiHat(0.8f, 0.22f, 1f, noise: 0f, peak: 0.8f);
                case DrumPiece.HiHatPedal: return HiHat(0.1f, 0.012f, 1f, noise: 0f, peak: 0.75f);
                case DrumPiece.Tom1: return ElectronicTom(0.7f, 260f, 90f, 0.2f);
                case DrumPiece.Tom2: return ElectronicTom(0.8f, 190f, 70f, 0.24f);
                case DrumPiece.FloorTom: return ElectronicTom(1.0f, 130f, 50f, 0.3f);
                case DrumPiece.Crash:
                    return Crush(Render(2.0f, 0.8f, (t, n, s) =>
                    {
                        float metal = HighPass(s, Metallic(t, 1.7f), 5000f);
                        float hiss = n - s.Lp2.LowPass(n, 5000f);
                        return (0.6f * metal + 0.6f * hiss) * Attack(t, 0.001f) * Mathf.Exp(-t / 0.7f);
                    }));
                case DrumPiece.Ride:
                    return Crush(Render(1.8f, 0.8f, (t, n, s) =>
                    {
                        float metal = HighPass(s, Metallic(t, 2.6f), 6000f);
                        float bell = Mathf.Sin(2f * Mathf.PI * 3000f * t) * Mathf.Exp(-t / 0.6f) * 0.2f;
                        return metal * Mathf.Exp(-t / 0.9f) + bell;
                    }));
                default: return new float[1];
            }
        }

        static float[] ElectronicTom(float seconds, float pitch, float bend, float decay) =>
            Render(seconds, 0.9f, (t, n, s) =>
            {
                s.Phase += 2f * Mathf.PI * (pitch + bend * Mathf.Exp(-t / 0.03f)) / Rate;
                return Mathf.Sin(s.Phase) * Mathf.Exp(-t / decay);
            });

        // ---------- Utilidades de síntesis ----------

        // Frecuencias de los seis osciladores cuadrados del hi-hat de la TR-808.
        static readonly float[] HatFrequencies = { 205.3f, 304.4f, 369.6f, 522.7f, 540f, 800f };
        static readonly float[] CymbalPartials = { 3150f, 3720f, 4410f, 5030f, 5640f, 6210f, 6880f, 7530f, 8270f, 8990f, 9640f, 10420f };

        class State
        {
            public float Phase;
            public Filter Lp1 = new Filter(), Lp2 = new Filter(), Hp1 = new Filter(), Hp2 = new Filter();
        }

        class Filter
        {
            float _y;
            public float LowPass(float x, float cutoff) => _y += (1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate)) * (x - _y);
        }

        static float Metallic(float t, float scale)
        {
            float sum = 0f;
            foreach (float f in HatFrequencies) sum += Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * scale * t));
            return sum / HatFrequencies.Length;
        }

        static float HighPass(State s, float x, float cutoff)
        {
            float a = x - s.Hp1.LowPass(x, cutoff);
            return a - s.Hp2.LowPass(a, cutoff);
        }

        static float Attack(float t, float time) => Mathf.Clamp01(t / time);

        static float Tanh(float x)
        {
            float e = Mathf.Exp(2f * x);
            return (e - 1f) / (e + 1f);
        }

        delegate float SampleFunction(float t, float noise, State state);

        static float[] Render(float seconds, float peak, SampleFunction function)
        {
            var data = new float[(int)(seconds * Rate)];
            var state = new State();
            uint seed = 0x9E3779B9u;
            for (int i = 0; i < data.Length; i++)
            {
                seed ^= seed << 13;
                seed ^= seed >> 17;
                seed ^= seed << 5;
                float noise = seed / (float)uint.MaxValue * 2f - 1f;
                data[i] = function((float)i / Rate, noise, state);
            }
            return Normalize(data, peak);
        }

        // Pequeñas reflexiones tempranas para que los tambores acústicos suenen en una sala.
        static float[] Room(float[] data, float peak)
        {
            int[] delays = { (int)(0.019f * Rate), (int)(0.031f * Rate), (int)(0.047f * Rate) };
            float[] gains = { 0.22f, 0.15f, 0.09f };
            var output = (float[])data.Clone();
            for (int d = 0; d < delays.Length; d++)
                for (int i = delays[d]; i < output.Length; i++)
                    output[i] += data[i - delays[d]] * gains[d];
            return Normalize(output, peak);
        }

        // Reduce resolución y frecuencia de muestreo: el carácter "digital" de las cajas de ritmos.
        static float[] Crush(float[] data)
        {
            const float levels = 96f;
            for (int i = 0; i < data.Length; i++)
            {
                float held = data[i - i % 2];
                data[i] = Mathf.Round(held * levels) / levels;
            }
            return data;
        }

        static float[] Normalize(float[] data, float peak)
        {
            float max = 1e-6f;
            foreach (float v in data) max = Mathf.Max(max, Mathf.Abs(v));
            float gain = peak / max;
            int fade = Mathf.Min(data.Length, Rate / 200);
            for (int i = 0; i < data.Length; i++)
            {
                float tail = i >= data.Length - fade ? (data.Length - i) / (float)fade : 1f;
                data[i] *= gain * tail;
            }
            return data;
        }

        static void WriteWav(string path, float[] samples)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var writer = new BinaryWriter(stream);
            int dataSize = samples.Length * 2;
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataSize);
            writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(Rate);
            writer.Write(Rate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataSize);
            foreach (float s in samples) writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
        }
    }
}
