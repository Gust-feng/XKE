using System.Runtime.InteropServices;
using System.Text;

namespace Xke.Security;

public static class XkeDpapi
{
    private const int CryptprotectUiForbidden = 0x01;
    private const string DefaultPurpose = "auth";
    private const string EntropyPrefix = "XKE|protected-json|v1|";

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob pDataIn,
        string? szDataDescr,
        ref DataBlob pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        out DataBlob pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob pDataIn,
        IntPtr ppszDataDescr,
        ref DataBlob pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        out DataBlob pDataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);

    public static byte[] Protect(byte[] plaintext, string? purpose = null)
    {
        return Transform(plaintext, protect: true, purpose);
    }

    public static byte[] Unprotect(byte[] ciphertext, string? purpose = null)
    {
        return Transform(ciphertext, protect: false, purpose);
    }

    private static byte[] Transform(byte[] input, bool protect, string? purpose)
    {
        var inputBlob = CreateBlob(input);
        var entropyBlob = CreateBlob(BuildEntropy(purpose));
        DataBlob outputBlob;

        try
        {
            var ok = protect
                ? CryptProtectData(ref inputBlob, null, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, CryptprotectUiForbidden, out outputBlob)
                : CryptUnprotectData(ref inputBlob, IntPtr.Zero, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, CryptprotectUiForbidden, out outputBlob);

            if (!ok)
            {
                throw new InvalidOperationException($"DPAPI {(protect ? "protect" : "unprotect")} failed: {Marshal.GetLastWin32Error()}");
            }

            try
            {
                if (outputBlob.cbData <= 0 || outputBlob.pbData == IntPtr.Zero)
                {
                    return [];
                }

                var output = new byte[outputBlob.cbData];
                Marshal.Copy(outputBlob.pbData, output, 0, outputBlob.cbData);
                return output;
            }
            finally
            {
                if (outputBlob.pbData != IntPtr.Zero)
                {
                    LocalFree(outputBlob.pbData);
                }
            }
        }
        finally
        {
            FreeBlob(inputBlob);
            FreeBlob(entropyBlob);
        }
    }

    private static DataBlob CreateBlob(byte[] data)
    {
        if (data.Length == 0)
        {
            return new DataBlob { cbData = 0, pbData = IntPtr.Zero };
        }

        var ptr = Marshal.AllocHGlobal(data.Length);
        Marshal.Copy(data, 0, ptr, data.Length);
        return new DataBlob { cbData = data.Length, pbData = ptr };
    }

    private static void FreeBlob(DataBlob blob)
    {
        if (blob.pbData != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(blob.pbData);
        }
    }

    private static byte[] BuildEntropy(string? purpose)
    {
        var normalizedPurpose = string.IsNullOrWhiteSpace(purpose)
            ? DefaultPurpose
            : purpose.Trim();
        return Encoding.UTF8.GetBytes($"{EntropyPrefix}{normalizedPurpose}");
    }
}
