using UnityEngine;

namespace AnyDrums.Menu
{
    /// <summary>
    /// Sonidos del menú generados por código: clic, roce del rayo y un golpe corto de caja por kit.
    /// Sirven de retroalimentación mientras no se integran los bancos de samples reales (Entregable 2);
    /// cuando existan, se asignan en <see cref="MenuAudio"/> y estos quedan como respaldo.
    /// </summary>
    public static class ProceduralDrumSounds
    {
        static AudioClip _click, _hover, _acousticSnare, _electronicSnare;

        public static AudioClip Click => _click != null ? _click : _click = BuildClick();
        public static AudioClip Hover => _hover != null ? _hover : _hover = BuildHover();
        public static AudioClip AcousticSnare => _acousticSnare != null ? _acousticSnare : _acousticSnare = BuildAcousticSnare();
        public static AudioClip ElectronicSnare => _electronicSnare != null ? _electronicSnare : _electronicSnare = BuildElectronicSnare();

        static int SampleRate => AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;

        static AudioClip BuildClick()
        {
            int rate = SampleRate;
            var data = new float[(int)(0.045f * rate)];
            var noise = new Noise(7);
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate;
                float tone = Mathf.Sin(2f * Mathf.PI * 1750f * t) * Mathf.Exp(-t / 0.007f);
                float tick = noise.Next() * Mathf.Exp(-t / 0.0015f);
                data[i] = 0.8f * tone + 0.35f * tick;
            }
            return Create("UI_Click", data, rate, 0.8f);
        }

        static AudioClip BuildHover()
        {
            int rate = SampleRate;
            var data = new float[(int)(0.02f * rate)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate;
                data[i] = Mathf.Sin(2f * Mathf.PI * 2900f * t) * Mathf.Exp(-t / 0.0035f);
            }
            return Create("UI_Hover", data, rate, 0.5f);
        }

        // Caja acústica: cuerpo grave que resuena, bordona con cola natural y un poco de sala.
        static AudioClip BuildAcousticSnare()
        {
            int rate = SampleRate;
            var data = new float[(int)(0.55f * rate)];
            var noise = new Noise(11);
            var highPass = new OnePole(1400f, rate);
            var lowPass = new OnePole(7500f, rate);
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate;
                float body = 0.6f * Mathf.Sin(2f * Mathf.PI * 186f * t) * Mathf.Exp(-t / 0.07f)
                           + 0.3f * Mathf.Sin(2f * Mathf.PI * 334f * t) * Mathf.Exp(-t / 0.04f);
                float n = noise.Next();
                float wires = lowPass.LowPass(n - highPass.LowPass(n)) * Mathf.Exp(-t / 0.15f);
                float attack = n * Mathf.Exp(-t / 0.002f);
                data[i] = body + 0.9f * wires + 0.4f * attack;
            }
            AddRoom(data, rate, 0.021f, 0.22f);
            AddRoom(data, rate, 0.034f, 0.15f);
            return Create("Preview_AcousticSnare", data, rate, 0.9f);
        }

        // Caja electrónica (estilo caja de ritmos): tono con barrido de afinación y ruido brillante cortado en seco.
        static AudioClip BuildElectronicSnare()
        {
            int rate = SampleRate;
            var data = new float[(int)(0.3f * rate)];
            var noise = new Noise(23);
            var highPass = new OnePole(3200f, rate);
            float phase = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate;
                float freq = 175f + 140f * Mathf.Exp(-t / 0.018f);
                phase += 2f * Mathf.PI * freq / rate;
                float tone = (Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 1.83f)) * Mathf.Exp(-t / 0.06f);
                float n = noise.Next();
                float snap = (n - highPass.LowPass(n)) * Mathf.Exp(-t / 0.075f);
                float gate = t < 0.2f ? 1f : Mathf.Clamp01(1f - (t - 0.2f) / 0.01f);
                data[i] = (0.7f * tone + 1.1f * snap) * gate;
            }
            return Create("Preview_ElectronicSnare", data, rate, 0.9f);
        }

        static void AddRoom(float[] data, int rate, float delaySeconds, float gain)
        {
            int delay = (int)(delaySeconds * rate);
            for (int i = delay; i < data.Length; i++)
                data[i] += data[i - delay] * gain;
        }

        static AudioClip Create(string name, float[] data, int rate, float peak)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
            float gain = max > 0f ? peak / max : 1f;
            // Rampa corta al final para que no haga clic al cortarse.
            int fade = Mathf.Min(data.Length, rate / 200);
            for (int i = 0; i < data.Length; i++)
            {
                float tail = i >= data.Length - fade ? (data.Length - i) / (float)fade : 1f;
                data[i] *= gain * tail;
            }
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        struct Noise
        {
            uint _state;
            public Noise(uint seed) => _state = seed * 747796405u + 2891336453u;

            public float Next()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return (_state / (float)uint.MaxValue) * 2f - 1f;
            }
        }

        struct OnePole
        {
            readonly float _a;
            float _y;

            public OnePole(float cutoff, int rate)
            {
                _a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / rate);
                _y = 0f;
            }

            public float LowPass(float x) => _y += _a * (x - _y);
        }
    }
}
