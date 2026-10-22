using UnityEngine;

namespace AnyDrums.Menu
{
    /// <summary>
    /// Color de acento de cada kit. Acústica: ámbar cálido (madera, calidez orgánica).
    /// Electrónica: cian (pantallas y pads, versatilidad moderna).
    /// </summary>
    public static class KitPalette
    {
        public static readonly Color AcousticAccent = new Color32(0xF5, 0xA6, 0x23, 0xFF);
        public static readonly Color ElectronicAccent = new Color32(0x2B, 0xD4, 0xF2, 0xFF);

        public static Color AccentFor(DrumKitType kit) => kit == DrumKitType.Acoustic ? AcousticAccent : ElectronicAccent;

        public static string DisplayName(DrumKitType kit) => kit == DrumKitType.Acoustic ? "Batería acústica" : "Batería electrónica";
    }
}
