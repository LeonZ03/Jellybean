using System;
using System.IO;
using System.Runtime.InteropServices;

internal static class ShaderCompiler
{
    [ComImport, Guid("8BA5FB08-5195-40E2-AC58-0D989C3A0102"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface Blob
    {
        [PreserveSig] nint GetBufferPointer();
        [PreserveSig] nuint GetBufferSize();
    }
    [DllImport("d3dcompiler_47.dll", CharSet = CharSet.Ansi)]
    private static extern int D3DCompile(byte[] source, nuint length, string name, nint defines, nint includes,
        string entry, string target, uint flags, uint effectFlags, out Blob code, out Blob errors);

    public static void Compile(string input, string output)
    {
        var source = File.ReadAllBytes(input);
        var result = D3DCompile(source, (nuint)source.Length, input, 0, 0, "main", "ps_2_0", 0, 0, out var code, out var errors);
        if (result < 0) throw new InvalidOperationException(errors is null ? "Shader compilation failed." : Marshal.PtrToStringAnsi(errors.GetBufferPointer()));
        var bytes = new byte[(int)code.GetBufferSize()]; Marshal.Copy(code.GetBufferPointer(), bytes, 0, bytes.Length);
        File.WriteAllBytes(output, bytes); Marshal.ReleaseComObject(code);
        if (errors is not null) Marshal.ReleaseComObject(errors);
    }
}
