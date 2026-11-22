// §642: Windows' own index, asked as Microsoft documents it: the Windows Search OLE DB provider (Search.CollatorDSO,
// CLSID_CollatorDataSource) and a SQL question over SystemIndex (Karpo.Core\WinSearchQuery.cs), its rows read through an
// accessor. Raw COM through the interfaces' tables, as the rest of the dock reaches Windows (TerraFX's pointers, no
// runtime marshalling), so it compiles ahead of time: oledb.h's interfaces are not in TerraFX, so their slots are
// written here, each checked against oledb.h (IUnknown's three, then the interface's own in its order).
//
// Every question is a data source, a session, a command and a rowset of its own, made and let go of on the thread that
// asks (a thread of its own with COM up, DriveReader.Start): nothing is held between keystrokes, so a Windows Search
// that restarts is simply found again. A keystroke's cancel is the command's own Cancel (ICommand::Cancel may be called
// from another thread) and stops the rows being read. The service off, or the provider missing, is a problem the line
// says (WinSearchIndexAnswer.Problem), never an exception. §655: what Windows indexes is no longer read here: the line's
// page links to Windows' own Searching Windows page, where it is chosen.

using System.Runtime.InteropServices;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.WinSearch;

/// <summary>§642: Windows' index through the Windows Search OLE DB provider (see the top of the file).</summary>
internal sealed unsafe class WindowsIndex : IWinSearchIndex
{
    // searchapi.h: CLSID_CollatorDataSource, the provider "Search.CollatorDSO".
    private static readonly Guid CollatorDataSource = new("9E175B8B-F52A-11D8-B9A5-505054503030");

    // oledb.h's interfaces.
    private static readonly Guid IDBInitialize = new("0C733A8B-2A1C-11CE-ADE5-00AA0044773D");
    private static readonly Guid IDBCreateSession = new("0C733A5D-2A1C-11CE-ADE5-00AA0044773D");
    private static readonly Guid IDBCreateCommand = new("0C733A1D-2A1C-11CE-ADE5-00AA0044773D");
    private static readonly Guid ICommandText = new("0C733A27-2A1C-11CE-ADE5-00AA0044773D");
    private static readonly Guid IRowset = new("0C733A7C-2A1C-11CE-ADE5-00AA0044773D");
    private static readonly Guid IAccessor = new("0C733A8C-2A1C-11CE-ADE5-00AA0044773D");

    // oledb.h: the dialect a command's text is in when it is the provider's own.
    private static readonly Guid DefaultDialect = new("C8B521FB-5CF3-11CE-ADE5-00AA0044773D");

    // The slots, after IUnknown's three.
    private const int SlotInitialize = 3; // IDBInitialize::Initialize
    private const int SlotCreateSession = 3; // IDBCreateSession::CreateSession
    private const int SlotCreateCommand = 3; // IDBCreateCommand::CreateCommand
    private const int SlotCancel = 3; // ICommand::Cancel
    private const int SlotExecute = 4; // ICommand::Execute
    private const int SlotSetCommandText = 7; // ICommandText::SetCommandText, after ICommand's Cancel, Execute, GetDBSession and its own GetCommandText
    private const int SlotCreateAccessor = 4; // IAccessor::CreateAccessor, after AddRefAccessor
    private const int SlotReleaseAccessor = 6; // IAccessor::ReleaseAccessor, after GetBindings
    private const int SlotGetData = 4; // IRowset::GetData, after AddRefRows
    private const int SlotGetNextRows = 5; // IRowset::GetNextRows
    private const int SlotReleaseRows = 6; // IRowset::ReleaseRows

    // oledb.h's numbers.
    private const ushort DbTypeI4 = 3;
    private const ushort DbTypeI8 = 20;
    private const ushort DbTypeWStr = 130;
    private const ushort DbTypeFileTime = 64;
    private const ushort DbTypeByRef = 0x4000;
    private const uint DbPartValue = 1;
    private const uint DbPartLength = 2;
    private const uint DbPartStatus = 4;
    private const uint DbAccessorRowData = 2;
    private const uint DbStatusOk = 0;
    private const int EndOfRowset = 0x00040EC6; // DB_S_ENDOFROWSET

    /// <summary>The rows asked for at a time.</summary>
    private const int Batch = 32;

    /// <summary>Each column's room in a row's buffer: its value (8), its length (8), its status (4, padded to 8).</summary>
    private const int Column = 24;

    /// <summary>oledb.h's DBBINDING, as x64 lays it out (pack 8).</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Binding
    {
        public nuint Ordinal;
        public nuint ValueAt;
        public nuint LengthAt;
        public nuint StatusAt;
        public void* TypeInfo;
        public void* Object;
        public void* Extension;
        public uint Part;
        public uint MemoryOwner;
        public uint ParamIo;
        public nuint MaxLength;
        public uint Flags;
        public ushort Type;
        public byte Precision;
        public byte Scale;
    }

    private static void** Table(void* unknown) => *(void***)unknown;

    private static void Release(ref void* unknown)
    {
        if (unknown != null)
        {
            _ = ((IUnknown*)unknown)->Release();
            unknown = null;
        }
    }

    public WinSearchIndexAnswer Query(string sql, CancellationToken cancel)
    {
        void* source = null;
        void* sessions = null;
        void* session = null;
        void* command = null;
        void* rowset = null;
        void* accessors = null;
        nuint accessor = 0;
        try
        {
            var clsid = CollatorDataSource;
            var iid = IDBInitialize;
            var hr = CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, &iid, &source).Value;
            if (hr < 0 || source == null)
            {
                return Failed("creating the provider", hr);
            }
            hr = ((delegate* unmanaged[Stdcall]<void*, int>)Table(source)[SlotInitialize])(source);
            if (hr < 0)
            {
                return Failed("starting the provider", hr);
            }
            iid = IDBCreateSession;
            if ((hr = ((IUnknown*)source)->QueryInterface(&iid, &sessions).Value) < 0)
            {
                return Failed("asking for sessions", hr);
            }
            iid = IDBCreateCommand;
            if ((hr = ((delegate* unmanaged[Stdcall]<void*, void*, Guid*, void**, int>)Table(sessions)[SlotCreateSession])(sessions, null, &iid, &session)) < 0)
            {
                return Failed("opening a session", hr);
            }
            iid = ICommandText;
            if ((hr = ((delegate* unmanaged[Stdcall]<void*, void*, Guid*, void**, int>)Table(session)[SlotCreateCommand])(session, null, &iid, &command)) < 0)
            {
                return Failed("making a command", hr);
            }
            var dialect = DefaultDialect;
            fixed (char* text = sql)
            {
                if ((hr = ((delegate* unmanaged[Stdcall]<void*, Guid*, char*, int>)Table(command)[SlotSetCommandText])(command, &dialect, text)) < 0)
                {
                    return Failed("setting the question", hr);
                }
            }
            if (cancel.IsCancellationRequested)
            {
                return WinSearchIndexAnswer.Empty;
            }
            // A keystroke's cancel is the command's own, from whichever thread it comes.
            var asked = (nint)command;
            using var stop = cancel.Register(() => ((delegate* unmanaged[Stdcall]<void*, int>)Table((void*)asked)[SlotCancel])((void*)asked));
            iid = IRowset;
            nint affected = 0;
            hr = ((delegate* unmanaged[Stdcall]<void*, void*, Guid*, void*, nint*, void**, int>)Table(command)[SlotExecute])(command, null, &iid, null, &affected, &rowset);
            if (cancel.IsCancellationRequested)
            {
                return WinSearchIndexAnswer.Empty;
            }
            if (hr < 0 || rowset == null)
            {
                return Failed("asking the index", hr);
            }
            iid = IAccessor;
            if ((hr = ((IUnknown*)rowset)->QueryInterface(&iid, &accessors).Value) < 0)
            {
                return Failed("asking for an accessor", hr);
            }
            var columns = WinSearchQuery.Columns.Length;
            var bindings = stackalloc Binding[columns];
            for (var at = 0; at < columns; at++)
            {
                bindings[at] = new Binding
                {
                    Ordinal = (nuint)(at + 1),
                    ValueAt = (nuint)(at * Column),
                    LengthAt = (nuint)((at * Column) + 8),
                    StatusAt = (nuint)((at * Column) + 16),
                    Part = DbPartValue | DbPartLength | DbPartStatus,
                    // Client owned: a string's buffer is the provider's CoTaskMemAlloc, ours to free.
                    MemoryOwner = 0,
                    MaxLength = 8,
                    Type = at switch
                    {
                        <= 3 => DbTypeWStr | DbTypeByRef,
                        4 => DbTypeFileTime,
                        5 => DbTypeI8,
                        _ => DbTypeI4,
                    },
                };
            }
            var statuses = stackalloc uint[columns];
            if ((hr = ((delegate* unmanaged[Stdcall]<void*, uint, nuint, Binding*, nuint, nuint*, uint*, int>)Table(accessors)[SlotCreateAccessor])(
                accessors, DbAccessorRowData, (nuint)columns, bindings, (nuint)(columns * Column), &accessor, statuses)) < 0)
            {
                return Failed("binding the columns", hr);
            }
            return new WinSearchIndexAnswer(Rows(rowset, accessor, columns, cancel));
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Troubles.Report("Windows search: asking the index", error); // not-ui: a trouble's name
            return new WinSearchIndexAnswer([], S.DockWinSearchServiceOff);
        }
        finally
        {
            if (accessor != 0 && accessors != null)
            {
                _ = ((delegate* unmanaged[Stdcall]<void*, nuint, uint*, int>)Table(accessors)[SlotReleaseAccessor])(accessors, accessor, null);
            }
            Release(ref accessors);
            Release(ref rowset);
            Release(ref command);
            Release(ref session);
            Release(ref sessions);
            Release(ref source);
        }
    }

    /// <summary>Every row of <paramref name="rowset"/>, a batch at a time, until the end or the cancel.</summary>
    private static List<WinSearchRow> Rows(void* rowset, nuint accessor, int columns, CancellationToken cancel)
    {
        List<WinSearchRow> rows = [];
        var buffer = stackalloc byte[columns * Column];
        while (!cancel.IsCancellationRequested)
        {
            nuint obtained = 0;
            nuint* handles = null;
            var hr = ((delegate* unmanaged[Stdcall]<void*, nuint, nint, nint, nuint*, nuint**, int>)Table(rowset)[SlotGetNextRows])(rowset, 0, 0, Batch, &obtained, &handles);
            try
            {
                if (hr < 0 || obtained == 0)
                {
                    break;
                }
                for (nuint at = 0; at < obtained; at++)
                {
                    new Span<byte>(buffer, columns * Column).Clear();
                    if (((delegate* unmanaged[Stdcall]<void*, nuint, nuint, void*, int>)Table(rowset)[SlotGetData])(rowset, handles[at], accessor, buffer) < 0)
                    {
                        continue;
                    }
                    var name = Text(buffer, 0);
                    var path = Text(buffer, 1);
                    var url = Text(buffer, 2);
                    var type = Text(buffer, 3);
                    DateTime? modified = Ok(buffer, 4) && *(long*)(buffer + (4 * Column)) is var ticks and > 0 ? DateTime.FromFileTimeUtc(ticks) : null;
                    long? size = Ok(buffer, 5) ? *(long*)(buffer + (5 * Column)) : null;
                    var rank = Ok(buffer, 6) ? *(int*)(buffer + (6 * Column)) : 0;
                    rows.Add(new WinSearchRow(name ?? "", path ?? "", url ?? "", type ?? "", modified, size, rank));
                }
            }
            finally
            {
                if (handles != null)
                {
                    if (obtained > 0)
                    {
                        _ = ((delegate* unmanaged[Stdcall]<void*, nuint, nuint*, uint*, uint*, uint*, int>)Table(rowset)[SlotReleaseRows])(rowset, obtained, handles, null, null, null);
                    }
                    CoTaskMemFree(handles);
                }
            }
            if (hr == EndOfRowset)
            {
                break;
            }
        }
        return rows;
    }

    /// <summary>Whether column <paramref name="at"/> came with a value.</summary>
    private static bool Ok(byte* buffer, int at) => *(uint*)(buffer + (at * Column) + 16) == DbStatusOk;

    /// <summary>Column <paramref name="at"/>'s string, its buffer freed; null where it has none.</summary>
    private static string? Text(byte* buffer, int at)
    {
        var text = *(char**)(buffer + (at * Column));
        if (text == null)
        {
            return null;
        }
        try
        {
            return Ok(buffer, at) ? new string(text) : null;
        }
        finally
        {
            CoTaskMemFree(text);
        }
    }

    /// <summary>The index could not be asked: reported once a question, and said on the line.</summary>
    private static WinSearchIndexAnswer Failed(string step, int hr)
    {
        Troubles.Write($"Windows search: {step} failed, 0x{hr:X8}"); // not-ui: a trouble's words
        return new WinSearchIndexAnswer([], S.DockWinSearchServiceOff);
    }
}
