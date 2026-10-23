using System.Runtime.InteropServices;

namespace YunseulMemo;

/// <summary>
/// 이미지 리사이즈 마커 커서 변경 및 이미지+텍스트 혼합 붙여넣기를 지원하는 RichTextBox.
/// 삽입된 이미지 클릭 시 나타나는 마커(핸들) 위로 마우스가 올라가면,
/// 높이 조절(SizeNS), 넓이 조절(SizeWE), 전체 대각선 크기 조절(SizeNWSE / SizeNESW) 커서로 즉시 전환됩니다.
/// </summary>
public class ResizableRichTextBox : RichTextBox
{
    private const int WM_SETCURSOR = 0x0020;
    private const int WM_PASTE = 0x0302;
    private const int EM_GETOLEINTERFACE = 0x043C;
    private const int HTCLIENT = 1;

    /// <summary>
    /// 외부에서 커스텀 붙여넣기 로직을 주입할 수 있는 이벤트.
    /// 핸들러가 true를 반환하면 기본 WM_PASTE를 차단합니다.
    /// </summary>
    public Func<bool>? CustomPasteHandler { get; set; }

    public ResizableRichTextBox()
    {
        DetectUrls = false;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_SETCURSOR)
        {
            if (LOWORD(m.LParam) == HTCLIENT)
            {
                var hitCursor = GetMarkerCursorAtMouse();
                if (hitCursor != null)
                {
                    Cursor.Current = hitCursor;
                    m.Result = (IntPtr)1;
                    return;
                }
            }
        }
        else if (m.Msg == WM_PASTE)
        {
            if (CustomPasteHandler != null && CustomPasteHandler())
                return;
        }

        base.WndProc(ref m);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var hitCursor = GetMarkerCursorAtMouse();
        if (hitCursor != null)
        {
            Cursor.Current = hitCursor;
        }
    }

    private static int LOWORD(IntPtr value) => (int)(value.ToInt64() & 0xFFFF);

    /// <summary>
    /// 현재 마우스 위치가 선택되거나 클릭된 OLE 이미지의 리사이즈 마커(핸들) 위에 있는지 판별하여
    /// 적절한 리사이즈 커서(SizeNS, SizeWE, SizeNWSE, SizeNESW)를 반환합니다.
    /// </summary>
    private Cursor? GetMarkerCursorAtMouse()
    {
        if (!IsHandleCreated) return null;

        Point clientMouse = PointToClient(Cursor.Position);
        if (!ClientRectangle.Contains(clientMouse)) return null;

        var ole = GetRichEditOle();
        if (ole == null) return null;

        int count = ole.GetObjectCount();
        if (count == 0) return null;

        float dpiX, dpiY;
        using (var g = CreateGraphics())
        {
            dpiX = g.DpiX;
            dpiY = g.DpiY;
        }

        for (int i = 0; i < count; i++)
        {
            var reObj = new REOBJECT();
            int hr = ole.GetObject(i, reObj, 7 /* REO_GETOBJ_ALL_INTERFACES */);
            if (hr != 0) continue;

            try
            {
                // 이미지 크기(HIMETRIC: 1/100mm) 구하기
                int cxHimetric = reObj.sizel.cx;
                int cyHimetric = reObj.sizel.cy;

                if ((cxHimetric <= 0 || cyHimetric <= 0) && reObj.poleobj != IntPtr.Zero)
                {
                    var oleObj = Marshal.GetObjectForIUnknown(reObj.poleobj) as IOleObject;
                    if (oleObj != null)
                    {
                        if (oleObj.GetExtent(1 /* DVASPECT_CONTENT */, out var extent) == 0)
                        {
                            cxHimetric = extent.cx;
                            cyHimetric = extent.cy;
                        }
                    }
                }

                if (cxHimetric <= 0 || cyHimetric <= 0) continue;

                // HIMETRIC -> 픽셀 변환
                int pixelW = (int)Math.Round(cxHimetric * dpiX / 2540.0);
                int pixelH = (int)Math.Round(cyHimetric * dpiY / 2540.0);

                if (pixelW <= 0 || pixelH <= 0) continue;

                // 문자 인덱스 위치(좌상단 픽셀 좌표)
                Point charPt = GetPositionFromCharIndex(reObj.cp);
                var imgRect = new Rectangle(charPt.X, charPt.Y, pixelW, pixelH);

                // 마우스가 이미지 영역 주위(마커 포함 ±14px)에 없으면 통과
                var expanded = Rectangle.Inflate(imgRect, 14, 14);
                if (!expanded.Contains(clientMouse)) continue;

                // 마커 히트 영역 크기 (약 12×12 픽셀로 마우스 호버 감지)
                const int handleR = 6;
                const int handleSize = 12;

                // 1. 높이 조절 마커 (상단 중앙, 하단 중앙) -> SizeNS (↕)
                var topCenter = new Rectangle(imgRect.Left + imgRect.Width / 2 - handleR, imgRect.Top - handleR, handleSize, handleSize);
                var bottomCenter = new Rectangle(imgRect.Left + imgRect.Width / 2 - handleR, imgRect.Bottom - handleR, handleSize, handleSize);

                if (topCenter.Contains(clientMouse) || bottomCenter.Contains(clientMouse))
                    return Cursors.SizeNS;

                // 2. 넓이 조절 마커 (좌측 중앙, 우측 중앙) -> SizeWE (↔)
                var leftCenter = new Rectangle(imgRect.Left - handleR, imgRect.Top + imgRect.Height / 2 - handleR, handleSize, handleSize);
                var rightCenter = new Rectangle(imgRect.Right - handleR, imgRect.Top + imgRect.Height / 2 - handleR, handleSize, handleSize);

                if (leftCenter.Contains(clientMouse) || rightCenter.Contains(clientMouse))
                    return Cursors.SizeWE;

                // 3. 전체 사이즈 대각선 모서리 마커
                // 좌상단, 우하단 -> SizeNWSE (⤡)
                var topLeft = new Rectangle(imgRect.Left - handleR, imgRect.Top - handleR, handleSize, handleSize);
                var bottomRight = new Rectangle(imgRect.Right - handleR, imgRect.Bottom - handleR, handleSize, handleSize);

                if (topLeft.Contains(clientMouse) || bottomRight.Contains(clientMouse))
                    return Cursors.SizeNWSE;

                // 우상단, 좌하단 -> SizeNESW (⤢)
                var topRight = new Rectangle(imgRect.Right - handleR, imgRect.Top - handleR, handleSize, handleSize);
                var bottomLeft = new Rectangle(imgRect.Left - handleR, imgRect.Bottom - handleR, handleSize, handleSize);

                if (topRight.Contains(clientMouse) || bottomLeft.Contains(clientMouse))
                    return Cursors.SizeNESW;

                // 4. 마커가 아닌 이미지 본체 위 -> 기본 화살표 커서
                if (imgRect.Contains(clientMouse))
                    return Cursors.Default;
            }
            finally
            {
                if (reObj.poleobj != IntPtr.Zero) Marshal.Release(reObj.poleobj);
                if (reObj.pstg != IntPtr.Zero) Marshal.Release(reObj.pstg);
                if (reObj.polesite != IntPtr.Zero) Marshal.Release(reObj.polesite);
            }
        }

        return null;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref IntPtr lParam);

    private IRichEditOle? GetRichEditOle()
    {
        IntPtr ppv = IntPtr.Zero;
        SendMessage(Handle, EM_GETOLEINTERFACE, IntPtr.Zero, ref ppv);
        if (ppv != IntPtr.Zero)
        {
            try
            {
                return Marshal.GetObjectForIUnknown(ppv) as IRichEditOle;
            }
            finally
            {
                Marshal.Release(ppv);
            }
        }
        return null;
    }

    #region P/Invoke OLE Interfaces

    [ComImport]
    [Guid("00020D00-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IRichEditOle
    {
        [PreserveSig] int GetClientSite(out IntPtr lplpolesite);
        [PreserveSig] int GetObjectCount();
        [PreserveSig] int GetLinkCount();
        [PreserveSig] int GetObject(int iob, [In, Out] REOBJECT lpreobject, uint dwFlags);
        [PreserveSig] int InsertObject(REOBJECT lpreobject);
        [PreserveSig] int ConvertObject(int iob, ref Guid rclsidNew, string lpstrUserTypeNew);
        [PreserveSig] int ActivateAs(ref Guid rclsid, ref Guid rclsidAs);
        [PreserveSig] int SetHostNames(string lpstrContainerApp, string lpstrContainerObj);
        [PreserveSig] int SetLinkAvailable(int iob, bool fAvailable);
        [PreserveSig] int SetDvaspect(int iob, uint uintDvaspect);
        [PreserveSig] int HandsOffStorage(int iob);
        [PreserveSig] int SaveCompleted(int iob, IntPtr lpstg);
        [PreserveSig] int InPlaceDeactivate();
        [PreserveSig] int ContextSensitiveHelp(bool fEnterMode);
        [PreserveSig] int GetClipboardData(ref CHARRANGE lpchrg, uint reco, out IntPtr lplpdataobj);
        [PreserveSig] int ImportDataObject(IntPtr lpdataobj, int cf, IntPtr hMetaPict);
    }

    [StructLayout(LayoutKind.Sequential)]
    private class REOBJECT
    {
        public int cbStruct = Marshal.SizeOf(typeof(REOBJECT));
        public int cp;
        public Guid clsid;
        public IntPtr poleobj;
        public IntPtr pstg;
        public IntPtr polesite;
        public SIZEL sizel;
        public uint dvaspect;
        public uint dwFlags;
        public uint dwUser;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZEL
    {
        public int cx;
        public int cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CHARRANGE
    {
        public int cpMin;
        public int cpMax;
    }

    [ComImport]
    [Guid("00000112-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IOleObject
    {
        [PreserveSig] int SetClientSite(IntPtr pClientSite);
        [PreserveSig] int GetClientSite(out IntPtr ppClientSite);
        [PreserveSig] int SetHostNames(string szContainerApp, string szContainerObj);
        [PreserveSig] int Close(uint dwSaveOption);
        [PreserveSig] int SetMoniker(uint dwWhichMoniker, IntPtr pmk);
        [PreserveSig] int GetMoniker(uint dwAssign, uint dwWhichMoniker, out IntPtr ppmk);
        [PreserveSig] int InitFromData(IntPtr pDataObject, bool fCreation, uint dwReserved);
        [PreserveSig] int GetClipboardData(uint dwReserved, out IntPtr ppDataObject);
        [PreserveSig] int DoVerb(int iVerb, IntPtr lpmsg, IntPtr pActiveSite, int lindex, IntPtr hwndParent, ref RECT lprcPosRect);
        [PreserveSig] int EnumVerbs(out IntPtr ppEnumOleVerb);
        [PreserveSig] int Update();
        [PreserveSig] int IsUpToDate();
        [PreserveSig] int GetUserClassID(out Guid pClsid);
        [PreserveSig] int GetUserType(uint dwFormOfType, out string pszUserType);
        [PreserveSig] int SetExtent(uint dwDrawAspect, ref SIZEL psizel);
        [PreserveSig] int GetExtent(uint dwDrawAspect, out SIZEL psizel);
        [PreserveSig] int Advise(IntPtr pAdvSink, out uint pdwConnection);
        [PreserveSig] int Unadvise(uint dwConnection);
        [PreserveSig] int EnumAdvise(out IntPtr ppenumAdvise);
        [PreserveSig] int GetMiscStatus(uint dwAspect, out uint pdwStatus);
        [PreserveSig] int SetColorScheme(IntPtr pLogpal);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    #endregion
}
