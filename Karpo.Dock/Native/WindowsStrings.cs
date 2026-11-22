// docs\widgets.md section 10 and §715: nothing managed crosses to Windows in the native build. A WinRT call that
// takes a list of strings (IIterable<String>) cannot be given a string[], a List<string> or a collection
// expression: the runtime would have to make a COM wrapper for it on the spot, the native build has none, and the
// call throws InvalidCastException ("Failed to create a CCW") before it does anything. A list Windows made itself
// goes back to it as it is. This file makes one.
//
// The list is a DataPackage's FileTypes: an IVector<String> of the clipboard's own code that takes any string and
// is read by nobody, since the package it belongs to is never set on the clipboard or shared.

using Windows.ApplicationModel.DataTransfer;

namespace Karpo.Dock.Native;

/// <summary>Lists of strings that Windows made, for WinRT calls that take one; see the top of the file.</summary>
internal static class WindowsStrings
{
    /// <summary>A list Windows made, holding <paramref name="first"/> (and <paramref name="second"/>).</summary>
    public static IList<string> Of(string first, string? second = null)
    {
        var list = new DataPackage().Properties.FileTypes;
        list.Add(first);
        if (second is not null)
        {
            list.Add(second);
        }
        return list;
    }
}
