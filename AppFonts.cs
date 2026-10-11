using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace QuodePaletteCapture
{
    /// <summary>
    /// Loads the embedded Montserrat font files into memory once at startup and exposes
    /// them as FontFamily instances. The UI can therefore use Montserrat on any Windows
    /// machine, installed or not, without touching the Windows font registry.
    /// </summary>
    internal static class AppFonts
    {
        // The memory behind each embedded font must stay alive for as long as the
        // PrivateFontCollection uses it. These handles are held for the lifetime of the
        // app and never freed. The OS reclaims them when the process exits.
        private static readonly List<IntPtr> _fontMemoryHandles = new();
        private static readonly PrivateFontCollection _collection = new();

        public static readonly FontFamily Regular;
        public static readonly FontFamily Medium;
        public static readonly FontFamily SemiBold;
        public static readonly FontFamily Bold;

        static AppFonts()
        {
            FontFamily fallback = FontFamily.GenericSansSerif;

            Regular = LoadEmbedded("QuodePaletteCapture.Fonts.Montserrat-Regular.ttf") ?? fallback;
            Medium = LoadEmbedded("QuodePaletteCapture.Fonts.Montserrat-Medium.ttf") ?? Regular;
            SemiBold = LoadEmbedded("QuodePaletteCapture.Fonts.Montserrat-SemiBold.ttf") ?? Regular;
            Bold = LoadEmbedded("QuodePaletteCapture.Fonts.Montserrat-Bold.ttf") ?? Regular;
        }

        private static FontFamily? LoadEmbedded(string resourceName)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using Stream? stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    return null;
                }

                byte[] fontData = new byte[stream.Length];
                int totalRead = 0;
                while (totalRead < fontData.Length)
                {
                    int read = stream.Read(fontData, totalRead, fontData.Length - totalRead);
                    if (read <= 0)
                    {
                        break;
                    }
                    totalRead += read;
                }

                IntPtr fontPtr = Marshal.AllocHGlobal(fontData.Length);
                Marshal.Copy(fontData, 0, fontPtr, fontData.Length);
                _fontMemoryHandles.Add(fontPtr);

                _collection.AddMemoryFont(fontPtr, fontData.Length);

                int count = _collection.Families.Length;
                return count > 0 ? _collection.Families[count - 1] : null;
            }
            catch
            {
                // The caller falls back to a system font. A font problem should never
                // stop the app from starting.
                return null;
            }
        }

        /// <summary>Creates a Font, falling back to a generic system font if anything goes wrong.</summary>
        public static Font Make(FontFamily family, float sizePt, FontStyle style = FontStyle.Regular)
        {
            try
            {
                return new Font(family, sizePt, style);
            }
            catch
            {
                return new Font(FontFamily.GenericSansSerif, sizePt, style);
            }
        }
    }
}
