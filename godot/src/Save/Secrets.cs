// 機密（Discord Client Secret、token）存檔前加密：Windows DPAPI，綁定目前的 Windows 帳號，別的帳號或別台電腦解不開
// 存成 "dpapi:<base64>"；讀到沒有前綴的舊資料當成明文（下次存檔就會加密）
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DeskPet.Save;

public static class Secrets
{
    const string Prefix = "dpapi:";

    public static string Protect(string plain)
    {
        if (string.IsNullOrEmpty(plain) || !OperatingSystem.IsWindows()) return plain;
        var data = Transform(Encoding.UTF8.GetBytes(plain), protect: true);
        return data == null ? plain : Prefix + Convert.ToBase64String(data);
    }

    public static string Unprotect(string stored)
    {
        if (string.IsNullOrEmpty(stored) || !stored.StartsWith(Prefix)) return stored ?? "";
        try
        {
            var data = Transform(Convert.FromBase64String(stored[Prefix.Length..]), protect: false);
            return data == null ? "" : Encoding.UTF8.GetString(data);
        }
        catch (FormatException) { return ""; }
    }

    static byte[]? Transform(byte[] input, bool protect)
    {
        var inBlob = new DataBlob { Size = input.Length, Data = Marshal.AllocHGlobal(input.Length) };
        try
        {
            Marshal.Copy(input, 0, inBlob.Data, input.Length);
            var ok = protect
                ? CryptProtectData(ref inBlob, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, out var outBlob)
                : CryptUnprotectData(ref inBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, out outBlob);
            if (!ok) return null;
            var result = new byte[outBlob.Size];
            Marshal.Copy(outBlob.Data, result, 0, outBlob.Size);
            LocalFree(outBlob.Data);
            return result;
        }
        finally { Marshal.FreeHGlobal(inBlob.Data); }
    }

    const int UiForbidden = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    struct DataBlob
    {
        public int Size;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool CryptProtectData(ref DataBlob dataIn, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true)]
    static extern bool CryptUnprotectData(ref DataBlob dataIn, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob dataOut);

    [DllImport("kernel32.dll")]
    static extern IntPtr LocalFree(IntPtr mem);
}
