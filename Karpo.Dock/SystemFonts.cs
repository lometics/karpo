// §556: the font families on this PC a look's text may be in (docs\widgets.md §556): DirectWrite's system collection, the
// one Gfx lays every line out of — each family by its English name (the file's word for it, whatever language Windows
// speaks), the weights of its upright faces of normal width that are its own, not made up by DirectWrite (a bold simulated
// on a regular). A family that cannot write a Latin "a" is left out: a symbol or icon font (Marlett, Wingdings, Segoe Fluent
// Icons), whose words would be boxes. Read once, when the Font list is first built (InstalledFonts), and again after
// Windows says the fonts changed (WM_FONTCHANGE, Dock.cs).

using Karpo.Core;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.DirectX.DirectX;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>§556: DirectWrite's system font families, for <see cref="InstalledFonts.Reader"/>; see the top of the file.</summary>
internal static unsafe class SystemFonts
{
    /// <summary>Every family of the system collection a text may be in, with its weights; none where DirectWrite cannot be asked.</summary>
    public static IReadOnlyList<InstalledFont> Read()
    {
        IDWriteFactory* factory = null;
        IDWriteFontCollection* fonts = null;
        try
        {
            if (DWriteCreateFactory(DWRITE_FACTORY_TYPE.DWRITE_FACTORY_TYPE_SHARED, __uuidof<IDWriteFactory>(), (IUnknown**)&factory).FAILED
                || factory->GetSystemFontCollection(&fonts, BOOL.TRUE).FAILED)
            {
                return InstalledFonts.Shipped;
            }
            List<InstalledFont> read = [];
            var count = fonts->GetFontFamilyCount();
            for (uint at = 0; at < count; at++)
            {
                IDWriteFontFamily* family = null;
                if (fonts->GetFontFamily(at, &family).FAILED)
                {
                    continue;
                }
                try
                {
                    if (Of(family) is { } font)
                    {
                        read.Add(font);
                    }
                }
                finally
                {
                    family->Release();
                }
            }
            return read;
        }
        finally
        {
            if (fonts is not null)
            {
                fonts->Release();
            }
            if (factory is not null)
            {
                factory->Release();
            }
        }
    }

    /// <summary><paramref name="family"/>'s English name and its own upright weights; null for one to leave out (see the top of the file).</summary>
    private static InstalledFont? Of(IDWriteFontFamily* family)
    {
        if (NameOf(family) is not { Length: > 0 } name || name[0] == '@')
        {
            return null;
        }
        SortedSet<int> weights = [];
        var writes = false;
        var count = family->GetFontCount();
        for (uint at = 0; at < count; at++)
        {
            IDWriteFont* font = null;
            if (family->GetFont(at, &font).FAILED)
            {
                continue;
            }
            try
            {
                if (font->GetStyle() != DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_NORMAL || font->GetStretch() != DWRITE_FONT_STRETCH.DWRITE_FONT_STRETCH_NORMAL
                    || font->GetSimulations() != DWRITE_FONT_SIMULATIONS.DWRITE_FONT_SIMULATIONS_NONE || font->IsSymbolFont())
                {
                    continue;
                }
                // One face asked is enough: the faces of a family write the same letters.
                if (!writes)
                {
                    BOOL has = BOOL.FALSE;
                    writes = font->HasCharacter('a', &has).SUCCEEDED && has;
                    if (!writes)
                    {
                        return null;
                    }
                }
                weights.Add((int)font->GetWeight());
            }
            finally
            {
                font->Release();
            }
        }
        return weights.Count == 0 ? null : new InstalledFont(name, [.. weights]);
    }

    /// <summary><paramref name="family"/>'s name in English, else its first.</summary>
    private static string? NameOf(IDWriteFontFamily* family)
    {
        IDWriteLocalizedStrings* names = null;
        if (family->GetFamilyNames(&names).FAILED)
        {
            return null;
        }
        try
        {
            uint index = 0;
            BOOL exists = BOOL.FALSE;
            fixed (char* english = "en-us")
            {
                if (names->FindLocaleName(english, &index, &exists).FAILED || !exists)
                {
                    index = 0;
                }
            }
            uint length = 0;
            if (names->GetStringLength(index, &length).FAILED)
            {
                return null;
            }
            var chars = new char[length + 1];
            fixed (char* into = chars)
            {
                return names->GetString(index, into, length + 1).FAILED ? null : new string(into, 0, (int)length);
            }
        }
        finally
        {
            names->Release();
        }
    }
}
