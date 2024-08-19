﻿using System.Runtime.InteropServices;

namespace CSnakes.Runtime.CPython;

internal unsafe partial class CPythonAPI
{
    private static nint PyLongType = IntPtr.Zero;
    private static nint PyLong_FromBytesMethod = IntPtr.Zero;

    [LibraryImport(PythonLibraryName)]
    internal static partial nint PyLong_FromLong(int v);

    [LibraryImport(PythonLibraryName)]
    internal static partial nint PyLong_FromLongLong(long v);

    [LibraryImport(PythonLibraryName)]
    internal static partial long PyLong_AsLongLong(nint p);

    internal static bool IsPyLong(nint p)
    {
        return PyObject_IsInstance(p, PyLongType);
    }

    [LibraryImport(PythonLibraryName)]
    internal static partial nint PyLong_FromUnicodeObject(nint unicode, int @base);
}
