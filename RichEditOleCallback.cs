using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace YunseulMemo;

/// <summary>
/// RichEdit OLE 콜백. 이미지 등 OLE 객체 삽입·리사이즈를 활성화합니다.
/// </summary>
[ComVisible(true)]
internal sealed class RichEditOleCallback : IRichEditOleCallback
{
    private const int S_OK = 0;
    private const int S_FALSE = 1;
    private const int E_NOTIMPL = unchecked((int)0x80004001);

    private const uint STGM_CREATE = 0x00001000;
    private const uint STGM_READWRITE = 0x00000002;
    private const uint STGM_SHARE_EXCLUSIVE = 0x00000010;

    [DllImport("ole32.dll")]
    private static extern int CreateILockBytesOnHGlobal(IntPtr hGlobal, bool fDeleteOnRelease, out IntPtr ppLkbyt);

    [DllImport("ole32.dll")]
    private static extern int StgCreateDocfileOnILockBytes(
        IntPtr plkbyt, uint grfMode, uint reserved, out IntPtr ppstgOpen);

    public int GetNewStorage(out IntPtr lplpstg)
    {
        lplpstg = IntPtr.Zero;
        int hr = CreateILockBytesOnHGlobal(IntPtr.Zero, true, out IntPtr lockBytes);
        if (hr != S_OK || lockBytes == IntPtr.Zero) return hr;

        return StgCreateDocfileOnILockBytes(
            lockBytes,
            STGM_CREATE | STGM_READWRITE | STGM_SHARE_EXCLUSIVE,
            0,
            out lplpstg);
    }

    public int QueryInsertObject(ref Guid lpclsid, IntPtr lpstg, int cp) => S_OK;

    public int DeleteObject(IntPtr lpoleobj) => S_OK;

    public int QueryAcceptData(IntPtr pdataobj, out short cfFormat, int reco, ref int fReally, IntPtr hMetaPict)
    {
        cfFormat = 0;
        fReally = 1;
        return S_OK;
    }

    public int ContextSensitiveHelp(int fEnterMode) => E_NOTIMPL;

    public int GetClipboardData(IntPtr pdataobj, out IntPtr poutdataobj)
    {
        poutdataobj = IntPtr.Zero;
        return S_FALSE;
    }

    public int GetDragDropEffect(bool fDrag, int grfKeyState, out int pdwEffect)
    {
        pdwEffect = fDrag ? 0 : 1; // DROPEFFECT_COPY
        return S_OK;
    }

    public int GetContextMenu(short seltype, IntPtr lpoleobj, IntPtr lpchrg, out IntPtr ppmenu)
    {
        ppmenu = IntPtr.Zero;
        return S_FALSE;
    }
}

[ComImport]
[Guid("00020D03-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IRichEditOleCallback
{
    [PreserveSig]
    int GetNewStorage(out IntPtr lplpstg);

    [PreserveSig]
    int QueryInsertObject(ref Guid lpclsid, IntPtr lpstg, int cp);

    [PreserveSig]
    int DeleteObject(IntPtr lpoleobj);

    [PreserveSig]
    int QueryAcceptData(IntPtr pdataobj, out short cfFormat, int reco, ref int fReally, IntPtr hMetaPict);

    [PreserveSig]
    int ContextSensitiveHelp(int fEnterMode);

    [PreserveSig]
    int GetClipboardData(IntPtr pdataobj, out IntPtr poutdataobj);

    [PreserveSig]
    int GetDragDropEffect(bool fDrag, int grfKeyState, out int pdwEffect);

    [PreserveSig]
    int GetContextMenu(short seltype, IntPtr lpoleobj, IntPtr lpchrg, out IntPtr ppmenu);
}
