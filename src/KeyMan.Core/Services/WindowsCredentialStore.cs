using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace KeyMan.Services;

public sealed class WindowsCredentialStore
{
    private const int GenericCredential = 1;
    private const int PersistLocalMachine = 2;
    private const int MaximumSecretBytes = 2560;
    private const int ErrorNotFound = 1168;

    public void Save(Guid id, string secret)
    {
        var secretBytes = Encoding.Unicode.GetByteCount(secret);
        if (secretBytes > MaximumSecretBytes)
        {
            throw new ArgumentException("키 또는 토큰은 1,280자 이하여야 합니다.", nameof(secret));
        }

        var secretPointer = Marshal.StringToCoTaskMemUni(secret);
        try
        {
            var credential = new NativeCredential
            {
                Type = GenericCredential,
                TargetName = GetTargetName(id),
                CredentialBlobSize = secretBytes,
                CredentialBlob = secretPointer,
                Persist = PersistLocalMachine,
                UserName = "KeyMan"
            };

            if (!CredWrite(ref credential, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            Marshal.ZeroFreeCoTaskMemUnicode(secretPointer);
        }
    }

    public string Read(Guid id)
    {
        if (!CredRead(GetTargetName(id), GenericCredential, 0, out var credentialPointer))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            return Marshal.PtrToStringUni(
                credential.CredentialBlob,
                credential.CredentialBlobSize / sizeof(char)) ?? string.Empty;
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public void Delete(Guid id)
    {
        if (CredDelete(GetTargetName(id), GenericCredential, 0))
        {
            return;
        }

        var error = Marshal.GetLastWin32Error();
        if (error != ErrorNotFound)
        {
            throw new Win32Exception(error);
        }
    }

    private static string GetTargetName(Guid id) => $"KeyMan/{id:N}";

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential credential, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPointer);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr credentialPointer);
}
