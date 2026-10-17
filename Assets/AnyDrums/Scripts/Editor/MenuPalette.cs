using UnityEngine;

namespace AnyDrums.EditorTools
{
    /// <summary>Colores del menú. Fondo oscuro y texto de alto contraste; cada kit tiene su color de acento.</summary>
    public static class MenuPalette
    {
        public static readonly Color Background = new Color32(0x07, 0x08, 0x0C, 0xFF);
        public static readonly Color Panel = new Color32(0x12, 0x14, 0x1B, 0xF7);
        public static readonly Color TextPrimary = new Color32(0xF4, 0xF6, 0xFA, 0xFF);
        public static readonly Color TextSecondary = new Color32(0x9A, 0xA3, 0xB5, 0xFF);
        public static readonly Color TextMuted = new Color32(0x74, 0x7D, 0x8F, 0xFF);
        public static readonly Color OnAccent = new Color32(0x0C, 0x0E, 0x13, 0xFF);

        // Acústica: ámbar cálido (madera, calidez orgánica). Electrónica: cian (pantallas, pads, versatilidad moderna).
        public static readonly Color AcousticAccent = new Color32(0xF5, 0xA6, 0x23, 0xFF);
        public static readonly Color ElectronicAccent = new Color32(0x2B, 0xD4, 0xF2, 0xFF);
    }
}
