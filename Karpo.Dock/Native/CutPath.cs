// §464 (the person, 2026-10-31: "i also dont want that items stack their background … if a folder face is transparent
// for example it should not show the background of the taskbar but it should show the desktop directly"): the bar's
// background is drawn only where no card stands. Composition clips a visual to one geometry; the bar's is its strip with
// a hole for every card, which no composition geometry of its own says. A CompositionPath can be any Direct2D geometry,
// handed over by an object Windows asks for it through IGeometrySource2DInterop: this file is that object, made by hand —
// a COM object of two interfaces (Windows.Graphics.IGeometrySource2D, which has no methods of its own, and the interop
// one) holding the geometry — and the geometry, the strip less each hole, combined by Direct2D. §584: a popup's outline with
// its beak is one more such path (Beaked).

using System.Runtime.InteropServices;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using WinRT;
using static TerraFX.Interop.DirectX.DirectX;
using static TerraFX.Interop.Windows.Windows;
using Edge = Karpo.Core.Edge;

namespace Karpo.Dock.Native;

/// <summary>§464: a hole in the bar's background: a card's rectangle, its corners rounded.</summary>
/// <param name="Rect">The card, in the strip's px.</param>
/// <param name="Radius">Its corners' radius.</param>
internal readonly record struct Hole(RectF Rect, float Radius);

/// <summary>§464: composition paths from Direct2D geometry; see the top of the file.</summary>
internal static unsafe class CutPath
{
    /// <summary>
    /// The path of the strip (from its origin, <paramref name="width"/> by <paramref name="height"/>) with every one of
    /// <paramref name="holes"/> cut out; null where Direct2D could not make it, and the caller draws the whole.
    /// </summary>
    public static CompositionPath? Of(float width, float height, IReadOnlyList<Hole> holes)
    {
        var factory = Factory();
        if (factory == null)
        {
            return null;
        }
        ID2D1Geometry* shape = null;
        try
        {
            ID2D1RectangleGeometry* whole = null;
            var all = new D2D_RECT_F(0, 0, width, height);
            if (factory->CreateRectangleGeometry(&all, &whole).FAILED)
            {
                return null;
            }
            shape = (ID2D1Geometry*)whole;
            foreach (var hole in holes)
            {
                var piece = hole.Rect.IsEmpty ? null : Piece(factory, hole);
                if (piece == null)
                {
                    continue;
                }
                var less = Combine(factory, shape, piece, D2D1_COMBINE_MODE.D2D1_COMBINE_MODE_EXCLUDE);
                ((IUnknown*)piece)->Release();
                if (less == null)
                {
                    return null;
                }
                ((IUnknown*)shape)->Release();
                shape = less;
            }
            return new CompositionPath(Source.Wrap(shape));
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: the bar's cut not made: {error.Message}");
            return null;
        }
        finally
        {
            if (shape != null)
            {
                ((IUnknown*)shape)->Release();
            }
        }
    }

    /// <summary>
    /// §584: a popup's outline with its beak (<see cref="Beak"/>) as one figure: the <paramref name="width"/> ×
    /// <paramref name="height"/> panel's rectangle, its corners <paramref name="radius"/> round, <paramref name="inset"/> px in
    /// (its corners as much less round), and the beak <paramref name="tall"/> out of its top or its foot
    /// (<paramref name="side"/>), its tip <paramref name="tip"/> px along it. In px from the top left of what holds the panel
    /// and the beak: the panel <paramref name="tall"/> down where the beak stands on its top. Null where Direct2D could not
    /// make it, and the caller keeps the rounded rectangle.
    /// </summary>
    public static CompositionPath? Beaked(float width, float height, float radius, float inset, Edge side, float tip, float tall)
    {
        var factory = Factory();
        if (factory == null)
        {
            return null;
        }
        ID2D1PathGeometry* path = null;
        ID2D1GeometrySink* sink = null;
        try
        {
            if (factory->CreatePathGeometry(&path).FAILED || path->Open(&sink).FAILED)
            {
                return null;
            }
            var down = side == Edge.Top ? tall : 0;
            var (left, top, right, bottom) = (inset, down + inset, width - inset, down + height - inset);
            var round = MathF.Max(0, radius - inset);
            var (from, peak, to) = Beak.Triangle(height, side, tip, tall, inset);
            ReadOnlySpan<D2D_POINT_2F> beak = [new(from.X, from.Y + down), new(peak.X, peak.Y + down), new(to.X, to.Y + down)];
            // Clockwise from the top's left end: the top (its beak), the right, the foot (its beak), the left.
            sink->BeginFigure(new D2D_POINT_2F(left + round, top), D2D1_FIGURE_BEGIN.D2D1_FIGURE_BEGIN_FILLED);
            if (side == Edge.Top)
            {
                Lines(sink, beak);
            }
            sink->AddLine(new D2D_POINT_2F(right - round, top));
            Corner(sink, right, top + round, round);
            sink->AddLine(new D2D_POINT_2F(right, bottom - round));
            Corner(sink, right - round, bottom, round);
            if (side == Edge.Bottom)
            {
                Lines(sink, beak);
            }
            sink->AddLine(new D2D_POINT_2F(left + round, bottom));
            Corner(sink, left, bottom - round, round);
            sink->AddLine(new D2D_POINT_2F(left, top + round));
            Corner(sink, left + round, top, round);
            sink->EndFigure(D2D1_FIGURE_END.D2D1_FIGURE_END_CLOSED);
            if (sink->Close().FAILED)
            {
                return null;
            }
            return new CompositionPath(Source.Wrap((ID2D1Geometry*)path));
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: a popup's beak not made: {error.Message}");
            return null;
        }
        finally
        {
            if (sink != null)
            {
                ((IUnknown*)sink)->Release();
            }
            if (path != null)
            {
                ((IUnknown*)path)->Release();
            }
        }
    }

    /// <summary>Straight lines to each of <paramref name="points"/>.</summary>
    private static void Lines(ID2D1GeometrySink* sink, ReadOnlySpan<D2D_POINT_2F> points)
    {
        foreach (var point in points)
        {
            sink->AddLine(point);
        }
    }

    /// <summary>A quarter round of <paramref name="radius"/>, clockwise, to (<paramref name="x"/>, <paramref name="y"/>).</summary>
    private static void Corner(ID2D1GeometrySink* sink, float x, float y, float radius)
    {
        var arc = new D2D1_ARC_SEGMENT
        {
            point = new D2D_POINT_2F(x, y), size = new D2D_SIZE_F(radius, radius), rotationAngle = 0,
            sweepDirection = D2D1_SWEEP_DIRECTION.D2D1_SWEEP_DIRECTION_CLOCKWISE, arcSize = D2D1_ARC_SIZE.D2D1_ARC_SIZE_SMALL,
        };
        sink->AddArc(&arc);
    }

    /// <summary>A hole's shape: its rounded rectangle, as the card itself is drawn (Scene.PlaceCard).</summary>
    private static ID2D1Geometry* Piece(ID2D1Factory* factory, Hole hole)
    {
        var rect = hole.Rect;
        var rounded = new D2D1_ROUNDED_RECT { rect = new D2D_RECT_F(rect.X, rect.Y, rect.Right, rect.Bottom), radiusX = hole.Radius, radiusY = hole.Radius };
        ID2D1RoundedRectangleGeometry* round = null;
        return factory->CreateRoundedRectangleGeometry(&rounded, &round).FAILED ? null : (ID2D1Geometry*)round;
    }

    /// <summary><paramref name="one"/> combined with <paramref name="other"/> into a new path geometry the caller owns; null where it failed.</summary>
    private static ID2D1Geometry* Combine(ID2D1Factory* factory, ID2D1Geometry* one, ID2D1Geometry* other, D2D1_COMBINE_MODE mode)
    {
        ID2D1PathGeometry* path = null;
        ID2D1GeometrySink* sink = null;
        if (factory->CreatePathGeometry(&path).FAILED)
        {
            return null;
        }
        if (path->Open(&sink).FAILED)
        {
            ((IUnknown*)path)->Release();
            return null;
        }
        var made = one->CombineWithGeometry(other, mode, null, (ID2D1SimplifiedGeometrySink*)sink).SUCCEEDED;
        made &= sink->Close().SUCCEEDED;
        ((IUnknown*)sink)->Release();
        if (!made)
        {
            ((IUnknown*)path)->Release();
            return null;
        }
        return (ID2D1Geometry*)path;
    }

    /// <summary>The Direct2D factory the cuts are made with, for the process's life; multithreaded, as several bars' tests run at once.</summary>
    private static ID2D1Factory* Factory()
    {
        if (factoryAt == 0)
        {
            ID2D1Factory* made = null;
            if (D2D1CreateFactory(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_MULTI_THREADED, __uuidof<ID2D1Factory>(), null, (void**)&made).FAILED)
            {
                return null;
            }
            if (Interlocked.CompareExchange(ref factoryAt, (nint)made, 0) != 0)
            {
                ((IUnknown*)made)->Release();
            }
        }
        return (ID2D1Factory*)factoryAt;
    }

    private static nint factoryAt;

    /// <summary>
    /// The object a CompositionPath asks its geometry of: Windows.Graphics.IGeometrySource2D (an IInspectable with nothing of
    /// its own) in its first slot, IGeometrySource2DInterop in its second, a count of who holds it and the geometry.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Source
    {
        public void** Inspectable;
        public void** Interop;
        public int Count;
        public ID2D1Geometry* Geometry;

        private const int S_OK = 0;
        private const int E_NOTIMPL = unchecked((int)0x80004001);
        private const int E_NOINTERFACE = unchecked((int)0x80004002);
        private const int E_POINTER = unchecked((int)0x80004003);

        /// <summary>IGeometrySource2DInterop's id.</summary>
        private static readonly Guid InteropId = new("0657AF73-53FD-47CF-84FF-C8492D2A80A3");

        private static readonly Guid UnknownId = new("00000000-0000-0000-C000-000000000046");
        private static readonly Guid InspectableId = new("AF86E2E0-B12D-4C6A-9C5A-D7AA65101E90");
        private static readonly Guid AgileId = new("94EA2B94-E9CC-49E0-C0FF-EE64CA8F5B90");

        /// <summary>Windows.Graphics.IGeometrySource2D's id, as the projection has it.</summary>
        private static readonly Guid SourceId = typeof(global::Windows.Graphics.IGeometrySource2D).GUID;

        private static readonly void** InspectableTable = Table(
            (delegate* unmanaged<void*, Guid*, void**, int>)&QueryInterface,
            (delegate* unmanaged<void*, uint>)&AddRef,
            (delegate* unmanaged<void*, uint>)&Release,
            (delegate* unmanaged<void*, uint*, Guid**, int>)&GetIids,
            (delegate* unmanaged<void*, nint*, int>)&GetRuntimeClassName,
            (delegate* unmanaged<void*, int*, int>)&GetTrustLevel);

        private static readonly void** InteropTable = Table(
            (delegate* unmanaged<void*, Guid*, void**, int>)&InteropQueryInterface,
            (delegate* unmanaged<void*, uint>)&InteropAddRef,
            (delegate* unmanaged<void*, uint>)&InteropRelease,
            (delegate* unmanaged<void*, ID2D1Geometry**, int>)&GetGeometry,
            (delegate* unmanaged<void*, ID2D1Factory*, ID2D1Geometry**, int>)&TryGetGeometryUsingFactory);

        /// <summary><paramref name="geometry"/> (held anew) as the projection's IGeometrySource2D.</summary>
        public static global::Windows.Graphics.IGeometrySource2D Wrap(ID2D1Geometry* geometry)
        {
            var made = (Source*)NativeMemory.AllocZeroed((nuint)sizeof(Source));
            made->Inspectable = InspectableTable;
            made->Interop = InteropTable;
            made->Count = 1;
            ((IUnknown*)geometry)->AddRef();
            made->Geometry = geometry;
            try
            {
                return MarshalInspectable<global::Windows.Graphics.IGeometrySource2D>.FromAbi((nint)made)!;
            }
            finally
            {
                Release(made);
            }
        }

        private static void** Table(params void*[] entries)
        {
            var table = (void**)NativeMemory.Alloc((nuint)(entries.Length * sizeof(void*)));
            for (var index = 0; index < entries.Length; index++)
            {
                table[index] = entries[index];
            }
            return table;
        }

        // Every callback catches: nothing may leave a call from Windows as an exception (NativeBoundaryTests).
        private const int E_FAIL = unchecked((int)0x80004005);

        [UnmanagedCallersOnly]
        private static int QueryInterface(void* self, Guid* iid, void** result)
        {
            try
            {
                return Query((Source*)self, iid, result);
            }
            catch (Exception)
            {
                return E_FAIL;
            }
        }

        /// <summary>What <paramref name="source"/> is asked to be: either of its interfaces, held anew.</summary>
        private static int Query(Source* source, Guid* iid, void** result)
        {
            if (result == null)
            {
                return E_POINTER;
            }
            var id = *iid;
            if (id == UnknownId || id == InspectableId || id == SourceId || id == AgileId)
            {
                *result = &source->Inspectable;
            }
            else if (id == InteropId)
            {
                *result = &source->Interop;
            }
            else
            {
                *result = null;
                return E_NOINTERFACE;
            }
            Interlocked.Increment(ref source->Count);
            return S_OK;
        }

        [UnmanagedCallersOnly]
        private static uint AddRef(void* self)
        {
            try
            {
                return (uint)Interlocked.Increment(ref ((Source*)self)->Count);
            }
            catch (Exception)
            {
                return 1;
            }
        }

        [UnmanagedCallersOnly]
        private static uint Release(void* self)
        {
            try
            {
                return Release((Source*)self);
            }
            catch (Exception)
            {
                return 1;
            }
        }

        private static uint Release(Source* source)
        {
            var left = Interlocked.Decrement(ref source->Count);
            if (left == 0)
            {
                ((IUnknown*)source->Geometry)->Release();
                NativeMemory.Free(source);
            }
            return (uint)left;
        }

        [UnmanagedCallersOnly]
        private static int GetIids(void* self, uint* count, Guid** iids)
        {
            try
            {
                *count = 0;
                *iids = null;
                return S_OK;
            }
            catch (Exception)
            {
                return E_FAIL;
            }
        }

        [UnmanagedCallersOnly]
        private static int GetRuntimeClassName(void* self, nint* name)
        {
            try
            {
                *name = 0;
                return S_OK;
            }
            catch (Exception)
            {
                return E_FAIL;
            }
        }

        [UnmanagedCallersOnly]
        private static int GetTrustLevel(void* self, int* level)
        {
            try
            {
                *level = 0;
                return S_OK;
            }
            catch (Exception)
            {
                return E_FAIL;
            }
        }

        /// <summary>The object an interop pointer points into: its second slot.</summary>
        private static Source* Of(void* self) => (Source*)((byte*)self - sizeof(void*));

        [UnmanagedCallersOnly]
        private static int InteropQueryInterface(void* self, Guid* iid, void** result)
        {
            try
            {
                if (result != null && *iid == InteropId)
                {
                    *result = &Of(self)->Interop;
                    Interlocked.Increment(ref Of(self)->Count);
                    return S_OK;
                }
                return Query(Of(self), iid, result);
            }
            catch (Exception)
            {
                return E_FAIL;
            }
        }

        [UnmanagedCallersOnly]
        private static uint InteropAddRef(void* self)
        {
            try
            {
                return (uint)Interlocked.Increment(ref Of(self)->Count);
            }
            catch (Exception)
            {
                return 1;
            }
        }

        [UnmanagedCallersOnly]
        private static uint InteropRelease(void* self)
        {
            try
            {
                return Release(Of(self));
            }
            catch (Exception)
            {
                return 1;
            }
        }

        [UnmanagedCallersOnly]
        private static int GetGeometry(void* self, ID2D1Geometry** value)
        {
            try
            {
                if (value == null)
                {
                    return E_POINTER;
                }
                var geometry = Of(self)->Geometry;
                ((IUnknown*)geometry)->AddRef();
                *value = geometry;
                return S_OK;
            }
            catch (Exception)
            {
                return E_FAIL;
            }
        }

        [UnmanagedCallersOnly]
        private static int TryGetGeometryUsingFactory(void* self, ID2D1Factory* factory, ID2D1Geometry** value)
        {
            try
            {
                if (value != null)
                {
                    *value = null;
                }
                return E_NOTIMPL;
            }
            catch (Exception)
            {
                return E_FAIL;
            }
        }
    }
}
