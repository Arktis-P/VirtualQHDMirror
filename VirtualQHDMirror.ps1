param(
    [string]$SourceDevice = "",
    [string]$DestinationDevice = "",
    [ValidateSet("adaptive","30","60","120")]
    [string]$Mode = "adaptive",
    [ValidateSet("portal","captured","legacy")]
    [string]$InputMode = "portal",
    [ValidateSet(60,120,240)]
    [int]$CursorHz = 120
)

$ErrorActionPreference = "Stop"

$startupForm = $null
$startupLabel = $null

try {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing

    $startupForm = New-Object System.Windows.Forms.Form
    $startupForm.Text = "VirtualQHDMirror v0.14.1"
    $startupForm.StartPosition = "CenterScreen"
    $startupForm.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::FixedDialog
    $startupForm.MaximizeBox = $false
    $startupForm.MinimizeBox = $false
    $startupForm.ControlBox = $false
    $startupForm.ShowInTaskbar = $false
    $startupForm.TopMost = $true
    $startupForm.ClientSize = New-Object System.Drawing.Size(420, 132)

    $startupTitle = New-Object System.Windows.Forms.Label
    $startupTitle.Text = "VirtualQHDMirror"
    $startupTitle.Font = New-Object System.Drawing.Font("Segoe UI", 15, [System.Drawing.FontStyle]::Bold)
    $startupTitle.AutoSize = $true
    $startupTitle.Location = New-Object System.Drawing.Point(22, 18)
    $startupForm.Controls.Add($startupTitle)

    $startupLabel = New-Object System.Windows.Forms.Label
    $startupLabel.Text = "Preparing mirror engine..."
    $startupLabel.Font = New-Object System.Drawing.Font("Segoe UI", 9)
    $startupLabel.AutoSize = $true
    $startupLabel.Location = New-Object System.Drawing.Point(24, 58)
    $startupForm.Controls.Add($startupLabel)

    $startupProgress = New-Object System.Windows.Forms.ProgressBar
    $startupProgress.Style = [System.Windows.Forms.ProgressBarStyle]::Marquee
    $startupProgress.MarqueeAnimationSpeed = 24
    $startupProgress.Location = New-Object System.Drawing.Point(25, 87)
    $startupProgress.Size = New-Object System.Drawing.Size(370, 18)
    $startupForm.Controls.Add($startupProgress)

    $startupForm.Show()
    [System.Windows.Forms.Application]::DoEvents()

    $code = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

public sealed class MirrorV0141 : Form
{
    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc cb, IntPtr data);
    delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT rect, IntPtr data);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX info);

    [DllImport("gdi32.dll", CharSet=CharSet.Unicode)] static extern IntPtr CreateDC(string driver,string device,string output,IntPtr initData);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);
    [DllImport("gdi32.dll")] static extern int GetObject(IntPtr hgdiobj,int cbBuffer,ref BITMAP lpvObject);
    [DllImport("gdi32.dll")] static extern bool StretchBlt(IntPtr dst,int xd,int yd,int wd,int hd,IntPtr src,int xs,int ys,int ws,int hs,uint rop);
    [DllImport("gdi32.dll")] static extern int SetStretchBltMode(IntPtr hdc,int mode);
    [DllImport("gdi32.dll")] static extern bool SetBrushOrgEx(IntPtr hdc,int x,int y,IntPtr prev);

    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr hwnd,ref POINT p);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd,uint msg,IntPtr wp,IntPtr lp);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,uint msg,IntPtr wp,IntPtr lp);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hwnd,int id,uint mods,uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hwnd,int id);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out RECT rect);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int cx,int cy,uint flags);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd,int cmd);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")] static extern bool ClipCursor(ref RECT rect);
    [DllImport("user32.dll", EntryPoint="ClipCursor")] static extern bool ReleaseCursorClip(IntPtr rect);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint processId);
    [DllImport("user32.dll")] static extern bool GetIconInfo(IntPtr hIcon,out ICONINFO info);
    [DllImport("user32.dll")] static extern uint SendInput(uint nInputs,INPUT[] inputs,int cbSize);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] static extern bool GetCursorInfo(ref CURSORINFO info);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int valueSize);
    [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint uPeriod);
    [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint uPeriod);
    [DllImport("user32.dll")] static extern bool DrawIconEx(
        IntPtr hdc,int x,int y,IntPtr hIcon,int w,int h,uint step,IntPtr brush,uint flags);

    const uint SRCCOPY=0x00CC0020;
    const int HALFTONE=4;

    const uint WM_MOUSEMOVE=0x0200;
    const uint WM_LBUTTONDOWN=0x0201, WM_LBUTTONUP=0x0202, WM_LBUTTONDBLCLK=0x0203;
    const uint WM_RBUTTONDOWN=0x0204, WM_RBUTTONUP=0x0205;
    const uint WM_MBUTTONDOWN=0x0207, WM_MBUTTONUP=0x0208;
    const uint WM_MOUSEWHEEL=0x020A;
    const uint WM_XBUTTONDOWN=0x020B, WM_XBUTTONUP=0x020C;
    const uint WM_NCHITTEST=0x0084;
    const uint WM_NCLBUTTONDOWN=0x00A1, WM_NCLBUTTONUP=0x00A2;
    const uint WM_SYSCOMMAND=0x0112;
    const uint WM_HOTKEY=0x0312;

    const int HTCLIENT=1, HTCAPTION=2, HTMINBUTTON=8, HTMAXBUTTON=9, HTCLOSE=20;
    const int SC_MINIMIZE=0xF020, SC_MAXIMIZE=0xF030, SC_CLOSE=0xF060, SC_RESTORE=0xF120;
    const int MK_LBUTTON=1, MK_RBUTTON=2, MK_MBUTTON=0x10;
    const int MK_XBUTTON1=0x20, MK_XBUTTON2=0x40;
    const int XBUTTON1=1, XBUTTON2=2;

    const uint GA_ROOT=2;
    const uint MOD_ALT=1, MOD_CONTROL=2;
    const int HK_EXIT=1, HK_INPUT=2, HK_OVERLAY=3, HK_MODE=4;
    const int VK_F9=0x78, VK_F10=0x79, VK_F11=0x7A, VK_F12=0x7B;
    const int WS_EX_NOACTIVATE=0x08000000, WS_EX_TOOLWINDOW=0x80;
    const int WS_EX_TRANSPARENT=0x20, WS_EX_LAYERED=0x00080000;
    const uint SWP_NOSIZE=0x0001, SWP_NOZORDER=0x0004, SWP_NOACTIVATE=0x0010;

    const int SW_RESTORE=9, SW_MAXIMIZE=3;
    const int SM_XVIRTUALSCREEN=76, SM_YVIRTUALSCREEN=77, SM_CXVIRTUALSCREEN=78, SM_CYVIRTUALSCREEN=79;
    const int DWMWA_DISALLOW_PEEK=11, DWMWA_EXCLUDED_FROM_PEEK=12;

    const uint CURSOR_SHOWING=0x00000001;
    const uint DI_NORMAL=0x0003;

    const uint INPUT_MOUSE=0;
    const uint MOUSEEVENTF_MOVE=0x0001;
    const uint MOUSEEVENTF_LEFTDOWN=0x0002;
    const uint MOUSEEVENTF_LEFTUP=0x0004;
    const uint MOUSEEVENTF_RIGHTDOWN=0x0008;
    const uint MOUSEEVENTF_RIGHTUP=0x0010;
    const uint MOUSEEVENTF_MIDDLEDOWN=0x0020;
    const uint MOUSEEVENTF_MIDDLEUP=0x0040;
    const uint MOUSEEVENTF_XDOWN=0x0080;
    const uint MOUSEEVENTF_XUP=0x0100;
    const uint MOUSEEVENTF_WHEEL=0x0800;
    const uint MOUSEEVENTF_VIRTUALDESK=0x4000;
    const uint MOUSEEVENTF_ABSOLUTE=0x8000;

    static readonly IntPtr PMV2=new IntPtr(-4);

    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int left,top,right,bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X,Y; public POINT(int x,int y){X=x;Y=y;} }

    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    public struct MONITORINFOEX {
        public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CURSORINFO {
        public int cbSize;
        public uint flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ICONINFO {
        [MarshalAs(UnmanagedType.Bool)] public bool fIcon;
        public uint xHotspot;
        public uint yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAP {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public InputUnion U; }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT {
        public int dx,dy; public uint mouseData,dwFlags,time; public IntPtr dwExtraInfo;
    }

    class Mon {
        public string Name;
        public Rectangle Rect;
        public Rectangle Work;
    }

    sealed class CursorShape {
        public int Width=32;
        public int Height=32;
        public int HotX=0;
        public int HotY=0;
    }

    sealed class CursorOverlayV0141 : Form
    {
        readonly Rectangle sourceRect;
        readonly int pollHz;
        readonly System.Windows.Forms.Timer cursorTimer;
        readonly Dictionary<IntPtr,CursorShape> shapes=new Dictionary<IntPtr,CursorShape>();

        CURSORINFO current;
        bool currentVisible=false;
        Rectangle currentDrawRect=Rectangle.Empty;
        bool timerResolutionRaised=false;

        public CursorOverlayV0141(Rectangle source,Rectangle destination,int requestedHz) {
            sourceRect=source;
            pollHz=requestedHz<=60?60:(requestedHz>=240?240:120);

            Text="VirtualQHDMirror v0.14.1 Cursor";
            FormBorderStyle=FormBorderStyle.None;
            StartPosition=FormStartPosition.Manual;
            Bounds=destination;
            BackColor=Color.Fuchsia;
            TransparencyKey=Color.Fuchsia;
            TopMost=true;
            ShowInTaskbar=false;
            DoubleBuffered=true;

            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);

            cursorTimer=new System.Windows.Forms.Timer();
            cursorTimer.Interval=Math.Max(4,(int)Math.Round(1000.0/pollHz));
            cursorTimer.Tick+=delegate{ PollCursor(); };

            Shown+=delegate{
                Bounds=destination;
                if(timeBeginPeriod(1)==0)timerResolutionRaised=true;
                cursorTimer.Start();
            };

            FormClosed+=delegate{
                cursorTimer.Stop();
                if(timerResolutionRaised)timeEndPeriod(1);
            };
        }

        protected override CreateParams CreateParams {
            get {
                var cp=base.CreateParams;
                cp.ExStyle|=WS_EX_NOACTIVATE|WS_EX_TOOLWINDOW|WS_EX_TRANSPARENT|WS_EX_LAYERED;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void WndProc(ref Message m) {
            if(m.Msg==WM_NCHITTEST) {
                m.Result=new IntPtr(-1);
                return;
            }
            base.WndProc(ref m);
        }

        CursorShape Shape(IntPtr cursor) {
            CursorShape cached;
            if(shapes.TryGetValue(cursor,out cached))return cached;

            var shape=new CursorShape();
            ICONINFO ii;
            if(GetIconInfo(cursor,out ii)) {
                shape.HotX=(int)ii.xHotspot;
                shape.HotY=(int)ii.yHotspot;
                try {
                    BITMAP bm=new BITMAP();
                    if(ii.hbmColor!=IntPtr.Zero &&
                       GetObject(ii.hbmColor,Marshal.SizeOf(typeof(BITMAP)),ref bm)>0) {
                        shape.Width=Math.Max(1,bm.bmWidth);
                        shape.Height=Math.Max(1,Math.Abs(bm.bmHeight));
                    }
                    else if(ii.hbmMask!=IntPtr.Zero &&
                            GetObject(ii.hbmMask,Marshal.SizeOf(typeof(BITMAP)),ref bm)>0) {
                        shape.Width=Math.Max(1,bm.bmWidth);
                        shape.Height=Math.Max(1,Math.Abs(bm.bmHeight)/2);
                    }
                }
                finally {
                    if(ii.hbmMask!=IntPtr.Zero)DeleteObject(ii.hbmMask);
                    if(ii.hbmColor!=IntPtr.Zero)DeleteObject(ii.hbmColor);
                }
            }

            shapes[cursor]=shape;
            return shape;
        }

        Rectangle CursorRect(CURSORINFO ci) {
            double scaleX=ClientSize.Width/(double)sourceRect.Width;
            double scaleY=ClientSize.Height/(double)sourceRect.Height;
            var shape=Shape(ci.hCursor);

            int hotspotX=(int)Math.Round(shape.HotX*scaleX);
            int hotspotY=(int)Math.Round(shape.HotY*scaleY);
            int drawW=Math.Max(1,(int)Math.Round(shape.Width*scaleX));
            int drawH=Math.Max(1,(int)Math.Round(shape.Height*scaleY));

            int dx=(int)Math.Round((ci.ptScreenPos.X-sourceRect.Left)*scaleX)-hotspotX;
            int dy=(int)Math.Round((ci.ptScreenPos.Y-sourceRect.Top )*scaleY)-hotspotY;
            return new Rectangle(dx,dy,drawW,drawH);
        }

        void PollCursor() {
            CURSORINFO next=new CURSORINFO();
            next.cbSize=Marshal.SizeOf(typeof(CURSORINFO));

            bool nextVisible=false;
            Rectangle nextRect=Rectangle.Empty;

            if(GetCursorInfo(ref next) &&
               (next.flags&CURSOR_SHOWING)!=0 &&
               next.hCursor!=IntPtr.Zero &&
               next.ptScreenPos.X>=sourceRect.Left&&next.ptScreenPos.X<sourceRect.Right&&
               next.ptScreenPos.Y>=sourceRect.Top&&next.ptScreenPos.Y<sourceRect.Bottom) {
                nextVisible=true;
                nextRect=CursorRect(next);
            }

            bool changed=
                nextVisible!=currentVisible ||
                (nextVisible && (!currentVisible ||
                    next.hCursor!=current.hCursor ||
                    next.ptScreenPos.X!=current.ptScreenPos.X ||
                    next.ptScreenPos.Y!=current.ptScreenPos.Y));

            if(!changed)return;

            Rectangle dirty=Rectangle.Empty;
            if(currentVisible)dirty=currentDrawRect;
            if(nextVisible)dirty=dirty.IsEmpty?nextRect:Rectangle.Union(dirty,nextRect);
            if(!dirty.IsEmpty)dirty.Inflate(3,3);

            current=next;
            currentVisible=nextVisible;
            currentDrawRect=nextRect;

            if(!dirty.IsEmpty) {
                Invalidate(dirty);
                Update();
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e) {
            using(var b=new SolidBrush(BackColor))
                e.Graphics.FillRectangle(b,e.ClipRectangle);
        }

        protected override void OnPaint(PaintEventArgs e) {
            if(!currentVisible||current.hCursor==IntPtr.Zero)return;

            IntPtr hdc=e.Graphics.GetHdc();
            try {
                DrawIconEx(
                    hdc,
                    currentDrawRect.X,currentDrawRect.Y,
                    current.hCursor,
                    currentDrawRect.Width,currentDrawRect.Height,
                    0,IntPtr.Zero,DI_NORMAL);
            }
            finally {
                e.Graphics.ReleaseHdc(hdc);
            }
        }
    }

    static List<Mon> Monitors() {
        var list=new List<Mon>();
        EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,
            delegate(IntPtr h,IntPtr d,ref RECT r,IntPtr data) {
                var mi=new MONITORINFOEX();
                mi.cbSize=Marshal.SizeOf(typeof(MONITORINFOEX));
                if(GetMonitorInfo(h,ref mi)) {
                    list.Add(new Mon {
                        Name=mi.szDevice,
                        Rect=Rectangle.FromLTRB(mi.rcMonitor.left,mi.rcMonitor.top,mi.rcMonitor.right,mi.rcMonitor.bottom),
                        Work=Rectangle.FromLTRB(mi.rcWork.left,mi.rcWork.top,mi.rcWork.right,mi.rcWork.bottom)
                    });
                }
                return true;
            },IntPtr.Zero);
        return list;
    }

    readonly Mon src,dst;
    readonly IntPtr srcDC;
    IntPtr dstDC=IntPtr.Zero;
    readonly System.Windows.Forms.Timer timer;
    readonly string inputMode;
    readonly int cursorHz;

    Thread cursorOverlayThread=null;
    CursorOverlayV0141 cursorOverlay=null;
    readonly ManualResetEvent cursorOverlayReady=new ManualResetEvent(false);
    volatile bool cursorOverlayAvailable=false;
    bool timerResolutionRaised=false;
    double lastRenderCostMs=0.0;

    bool inputEnabled=true;
    bool overlay=false;

    const int PORTAL_EDGE_NONE=0;
    const int PORTAL_EDGE_LEFT=1;
    const int PORTAL_EDGE_RIGHT=2;
    const int PORTAL_EDGE_TOP=3;
    const int PORTAL_EDGE_BOTTOM=4;
    const int PORTAL_ENTRY_THRESHOLD=16;
    const int PORTAL_UNBLOCK_MARGIN=40;

    bool portalActive=false;
    int portalBlockedEdge=PORTAL_EDGE_NONE;
    POINT lastPortalCursor;
    bool lastPortalCursorValid=false;

    bool capturedInput=false;
    bool capturePending=false;
    MouseButtons capturePendingButton=MouseButtons.None;
    POINT captureReturnPoint;
    POINT lastCapturedCursor;
    bool lastCapturedCursorValid=false;

    IntPtr dragTarget=IntPtr.Zero;
    int mouseButtons=0;

    bool movingWindow=false;
    IntPtr movingRoot=IntPtr.Zero;
    POINT moveStartMouse;
    RECT moveStartRect;

    string renderMode;
    DateTime lastInput=DateTime.UtcNow;
    int lastInterval=-1;

    Point lastClient=Point.Empty;
    POINT lastMapped=new POINT(0,0);
    string lastZone="DESKTOP";

    DateTime lastLeftClick=DateTime.MinValue;
    POINT lastLeftClickPoint=new POINT(-99999,-99999);
    IntPtr lastLeftClickRoot=IntPtr.Zero;

    readonly Dictionary<IntPtr,CursorShape> cursorShapes=new Dictionary<IntPtr,CursorShape>();

    bool pendingNativeClick=false;
    MouseButtons pendingNativeButton=MouseButtons.None;
    bool pendingNativeTaskbar=false;

    bool pendingCaptionCommand=false;
    IntPtr pendingCaptionRoot=IntPtr.Zero;
    int pendingCaptionHit=HTCLIENT;

    bool nativeInteraction=false;
    bool nativeTaskbarInteraction=false;
    POINT nativeReturnPoint;
    IntPtr nativeStartForeground=IntPtr.Zero;
    DateTime nativeStarted=DateTime.MinValue;
    DateTime nativeLastShellSeen=DateTime.MinValue;

    public static void EnableDpi(){ try{SetProcessDpiAwarenessContext(PMV2);}catch{} }

    public MirrorV0141(string srcName,string dstName,string mode,string requestedInputMode,int requestedCursorHz) {
        renderMode=mode;
        cursorHz=requestedCursorHz<=60?60:(requestedCursorHz>=240?240:120);
        if(String.Equals(requestedInputMode,"legacy",StringComparison.OrdinalIgnoreCase)) inputMode="legacy";
        else if(String.Equals(requestedInputMode,"captured",StringComparison.OrdinalIgnoreCase)) inputMode="captured";
        else inputMode="portal";

        foreach(var m in Monitors()) {
            if(String.Equals(m.Name,srcName,StringComparison.OrdinalIgnoreCase)) src=m;
            if(String.Equals(m.Name,dstName,StringComparison.OrdinalIgnoreCase)) dst=m;
        }
        if(src==null) throw new Exception("Source not found: "+srcName);
        if(dst==null) throw new Exception("Destination not found: "+dstName);

        srcDC=CreateDC("DISPLAY",src.Name,null,IntPtr.Zero);
        if(srcDC==IntPtr.Zero) throw new Exception("CreateDC failed: "+src.Name);

        Text="VirtualQHDMirror v0.14.1";
        FormBorderStyle=FormBorderStyle.None;
        StartPosition=FormStartPosition.Manual;
        Bounds=dst.Rect;
        BackColor=Color.Black;
        TopMost=true;
        ShowInTaskbar=false;
        DoubleBuffered=false;

        timer=new System.Windows.Forms.Timer();
        timer.Tick+=delegate{
            AdjustInterval();
            if(inputMode=="portal") UpdatePortalInput();
            else if(inputMode=="captured") UpdateCapturedInput();
            else UpdateNativeInteraction();
            DrawFrame();
        };

        Shown+=delegate{
            Bounds=dst.Rect;
            ApplyDwmIsolation();
            if(timeBeginPeriod(1)==0)timerResolutionRaised=true;
            dstDC=GetDC(Handle);
            if(dstDC==IntPtr.Zero) throw new Exception("GetDC failed for mirror window.");

            SetStretchBltMode(dstDC,HALFTONE);
            SetBrushOrgEx(dstDC,0,0,IntPtr.Zero);

            StartCursorOverlay();

            RegisterHotKey(Handle,HK_EXIT,MOD_CONTROL|MOD_ALT,VK_F12);
            RegisterHotKey(Handle,HK_INPUT,MOD_CONTROL|MOD_ALT,VK_F11);
            RegisterHotKey(Handle,HK_OVERLAY,MOD_CONTROL|MOD_ALT,VK_F10);
            RegisterHotKey(Handle,HK_MODE,MOD_CONTROL|MOD_ALT,VK_F9);

            ApplyInterval(16);
            timer.Start();
        };

        FormClosed+=delegate{
            StopCursorOverlay();
            if(portalActive) EndPortalToDestination();
            else if(capturedInput) EndCapturedInput();
            else ReleaseCursorClip(IntPtr.Zero);
            if(nativeInteraction) EndNativeInteraction();
            timer.Stop();

            UnregisterHotKey(Handle,HK_EXIT);
            UnregisterHotKey(Handle,HK_INPUT);
            UnregisterHotKey(Handle,HK_OVERLAY);
            UnregisterHotKey(Handle,HK_MODE);

            if(dstDC!=IntPtr.Zero) {
                ReleaseDC(Handle,dstDC);
                dstDC=IntPtr.Zero;
            }
            if(srcDC!=IntPtr.Zero) DeleteDC(srcDC);
            if(timerResolutionRaised)timeEndPeriod(1);
        };
    }

    protected override CreateParams CreateParams {
        get { var cp=base.CreateParams; cp.ExStyle|=WS_EX_NOACTIVATE|WS_EX_TOOLWINDOW; return cp; }
    }
    protected override bool ShowWithoutActivation { get { return true; } }

    protected override void WndProc(ref Message m) {
        if(m.Msg==WM_HOTKEY) {
            int id=m.WParam.ToInt32();
            if(id==HK_EXIT){
                if(portalActive) EndPortalToDestination();
                else if(capturedInput) EndCapturedInput();
                Close();
                return;
            }
            if(id==HK_INPUT){
                if(inputMode=="portal") {
                    lastZone=portalActive?"PORTAL":"PORTAL READY";
                    return;
                }
                if(inputMode=="captured") {
                    capturePending=false;
                    if(capturedInput) EndCapturedInput();
                    else lastZone="ARMED";
                    return;
                }

                if(nativeInteraction) EndNativeInteraction();
                pendingNativeClick=false;
                pendingCaptionCommand=false;
                pendingCaptionRoot=IntPtr.Zero;
                inputEnabled=!inputEnabled;
                return;
            }
            if(id==HK_OVERLAY){ overlay=!overlay; return; }
            if(id==HK_MODE){
                renderMode = renderMode=="adaptive" ? "30" : (renderMode=="30" ? "60" : (renderMode=="60" ? "120" : "adaptive"));
                lastInput=DateTime.UtcNow;
                return;
            }
        }
        base.WndProc(ref m);
    }

    void ApplyDwmIsolation() {
        try {
            int enabled=1;
            DwmSetWindowAttribute(Handle,DWMWA_DISALLOW_PEEK,ref enabled,sizeof(int));
            DwmSetWindowAttribute(Handle,DWMWA_EXCLUDED_FROM_PEEK,ref enabled,sizeof(int));
        }
        catch {}
    }

    void ApplyInterval(int ms) {
        if(ms<4) ms=4;
        if(lastInterval==ms) return;
        lastInterval=ms;
        timer.Interval=ms;
    }

    void AdjustInterval() {
        if(renderMode=="120"){ ApplyInterval(8); return; }
        if(renderMode=="60"){ ApplyInterval(16); return; }
        if(renderMode=="30"){ ApplyInterval(33); return; }

        double idle=(DateTime.UtcNow-lastInput).TotalSeconds;
        if(idle<0.8) ApplyInterval(16);      // active interaction
        else if(idle<8.0) ApplyInterval(33); // normal productivity
        else ApplyInterval(66);              // long idle
    }

    void MarkInput() {
        lastInput=DateTime.UtcNow;
        if(renderMode=="adaptive") ApplyInterval(16);
    }

    void StartCursorOverlay() {
        cursorOverlayReady.Reset();
        cursorOverlayAvailable=false;

        cursorOverlayThread=new Thread(delegate{
            try {
                var local=new CursorOverlayV0141(src.Rect,dst.Rect,cursorHz);
                cursorOverlay=local;
                cursorOverlayAvailable=true;
                cursorOverlayReady.Set();
                Application.Run(local);
            }
            catch {
                cursorOverlayAvailable=false;
                cursorOverlayReady.Set();
            }
            finally {
                cursorOverlayAvailable=false;
                cursorOverlay=null;
            }
        });
        cursorOverlayThread.IsBackground=true;
        cursorOverlayThread.Name="VirtualQHDMirror Cursor Overlay";
        cursorOverlayThread.SetApartmentState(ApartmentState.STA);
        cursorOverlayThread.Start();
        cursorOverlayReady.WaitOne(1200);
    }

    void StopCursorOverlay() {
        cursorOverlayAvailable=false;
        try {
            var local=cursorOverlay;
            if(local!=null&&!local.IsDisposed&&local.IsHandleCreated) {
                local.BeginInvoke((MethodInvoker)delegate{ local.Close(); });
            }
        }
        catch {}

        try {
            if(cursorOverlayThread!=null&&cursorOverlayThread.IsAlive)
                cursorOverlayThread.Join(800);
        }
        catch {}

        cursorOverlayThread=null;
        cursorOverlay=null;
    }

    void DrawFrame() {
        if(dstDC==IntPtr.Zero) return;

        long t0=System.Diagnostics.Stopwatch.GetTimestamp();

        StretchBlt(
            dstDC,
            0,0,ClientSize.Width,ClientSize.Height,
            srcDC,
            0,0,src.Rect.Width,src.Rect.Height,
            SRCCOPY);

        // Fallback only: normally the cursor is rendered by a dedicated high-rate overlay.
        if(!cursorOverlayAvailable)DrawSourceCursorIfNeeded();

        long t1=System.Diagnostics.Stopwatch.GetTimestamp();
        double ms=(t1-t0)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        lastRenderCostMs=lastRenderCostMs<=0?ms:(lastRenderCostMs*0.85+ms*0.15);

        if(overlay) DrawOverlay();
    }

    CursorShape GetCursorShape(IntPtr cursor) {
        CursorShape cached;
        if(cursorShapes.TryGetValue(cursor,out cached)) return cached;

        var shape=new CursorShape();
        ICONINFO ii;
        if(GetIconInfo(cursor,out ii)) {
            shape.HotX=(int)ii.xHotspot;
            shape.HotY=(int)ii.yHotspot;
            try {
                BITMAP bm=new BITMAP();
                if(ii.hbmColor!=IntPtr.Zero &&
                   GetObject(ii.hbmColor,Marshal.SizeOf(typeof(BITMAP)),ref bm)>0) {
                    shape.Width=Math.Max(1,bm.bmWidth);
                    shape.Height=Math.Max(1,Math.Abs(bm.bmHeight));
                }
                else if(ii.hbmMask!=IntPtr.Zero &&
                        GetObject(ii.hbmMask,Marshal.SizeOf(typeof(BITMAP)),ref bm)>0) {
                    shape.Width=Math.Max(1,bm.bmWidth);
                    shape.Height=Math.Max(1,Math.Abs(bm.bmHeight)/2);
                }
            }
            finally {
                if(ii.hbmMask!=IntPtr.Zero)DeleteObject(ii.hbmMask);
                if(ii.hbmColor!=IntPtr.Zero)DeleteObject(ii.hbmColor);
            }
        }

        cursorShapes[cursor]=shape;
        return shape;
    }

    void DrawSourceCursorIfNeeded() {
        CURSORINFO ci=new CURSORINFO();
        ci.cbSize=Marshal.SizeOf(typeof(CURSORINFO));

        if(!GetCursorInfo(ref ci)) return;
        if((ci.flags&CURSOR_SHOWING)==0 || ci.hCursor==IntPtr.Zero) return;
        if(!IsSourcePoint(ci.ptScreenPos)) return;

        double scaleX=ClientSize.Width/(double)src.Rect.Width;
        double scaleY=ClientSize.Height/(double)src.Rect.Height;
        var shape=GetCursorShape(ci.hCursor);

        int hotspotX=(int)Math.Round(shape.HotX*scaleX);
        int hotspotY=(int)Math.Round(shape.HotY*scaleY);
        int drawW=Math.Max(1,(int)Math.Round(shape.Width*scaleX));
        int drawH=Math.Max(1,(int)Math.Round(shape.Height*scaleY));

        int dx=(int)Math.Round((ci.ptScreenPos.X-src.Rect.Left)*scaleX)-hotspotX;
        int dy=(int)Math.Round((ci.ptScreenPos.Y-src.Rect.Top )*scaleY)-hotspotY;

        DrawIconEx(dstDC,dx,dy,ci.hCursor,drawW,drawH,0,IntPtr.Zero,DI_NORMAL);
    }

    bool IsCustomCaptionButtonZone(IntPtr target, POINT sp) {
        IntPtr root=GetAncestor(target,GA_ROOT);
        if(root==IntPtr.Zero) return false;

        RECT r;
        if(!GetWindowRect(root,out r)) return false;

        int width=r.right-r.left;
        int height=r.bottom-r.top;
        if(width<300 || height<120) return false;

        int topBand=64;
        int buttonBand=190;

        return sp.Y>=r.top && sp.Y<r.top+topBand &&
               sp.X>=r.right-buttonBand && sp.X<r.right;
    }

    void DrawOverlay() {
        using(var g=Graphics.FromHwnd(Handle))
        using(var bg=new SolidBrush(Color.FromArgb(180,0,0,0)))
        using(var fg=new SolidBrush(Color.White))
        using(var f=new Font("Consolas",10)) {
            string inputState;
            if(inputMode=="portal")
                inputState=portalActive?"PORTAL NATIVE":"PORTAL READY";
            else if(inputMode=="captured")
                inputState=capturedInput?"CAPTURED":"ARMED";
            else
                inputState=inputEnabled?"LEGACY ON":"LEGACY OFF";

            string t=
                "VirtualQHDMirror v0.14.1\n"+
                "render: "+renderMode+" / "+(1000.0/Math.Max(1,lastInterval)).ToString("0")+" fps target\n"+
                "render cost: "+lastRenderCostMs.ToString("0.0")+" ms\n"+
                "cursor: "+(cursorOverlayAvailable?cursorHz.ToString()+" Hz overlay":"frame fallback")+"\n"+
                "zone: "+lastZone+"\n"+
                "input: "+inputState;
            var s=g.MeasureString(t,f);
            g.FillRectangle(bg,10,10,s.Width+14,s.Height+10);
            g.DrawString(t,f,fg,17,15);
        }
    }

    POINT Map(Point p) {
        double sx=((p.X+0.5)*src.Rect.Width/(double)dst.Rect.Width)-0.5;
        double sy=((p.Y+0.5)*src.Rect.Height/(double)dst.Rect.Height)-0.5;
        int x=src.Rect.Left+(int)Math.Round(sx);
        int y=src.Rect.Top +(int)Math.Round(sy);
        x=Math.Max(src.Rect.Left,Math.Min(src.Rect.Right-1,x));
        y=Math.Max(src.Rect.Top, Math.Min(src.Rect.Bottom-1,y));
        return new POINT(x,y);
    }

    bool IsSourcePoint(POINT p) {
        return p.X>=src.Rect.Left&&p.X<src.Rect.Right&&p.Y>=src.Rect.Top&&p.Y<src.Rect.Bottom;
    }

    POINT MapSourceToDestination(POINT p) {
        double dx=((p.X-src.Rect.Left+0.5)*dst.Rect.Width/(double)src.Rect.Width)-0.5;
        double dy=((p.Y-src.Rect.Top +0.5)*dst.Rect.Height/(double)src.Rect.Height)-0.5;
        int x=dst.Rect.Left+(int)Math.Round(dx);
        int y=dst.Rect.Top +(int)Math.Round(dy);
        x=Math.Max(dst.Rect.Left,Math.Min(dst.Rect.Right-1,x));
        y=Math.Max(dst.Rect.Top, Math.Min(dst.Rect.Bottom-1,y));
        return new POINT(x,y);
    }

    bool IsTaskbarZone(POINT p) {
        bool inMonitor=p.X>=src.Rect.Left&&p.X<src.Rect.Right&&p.Y>=src.Rect.Top&&p.Y<src.Rect.Bottom;
        bool inWork=p.X>=src.Work.Left&&p.X<src.Work.Right&&p.Y>=src.Work.Top&&p.Y<src.Work.Bottom;
        return inMonitor&&!inWork;
    }

    static IntPtr LP(int x,int y){ return new IntPtr((y<<16)|(x&0xffff)); }

    void Post(IntPtr hwnd,uint msg,int keys,POINT sp) {
        if(hwnd==IntPtr.Zero)return;
        POINT cp=sp;
        ScreenToClient(hwnd,ref cp);
        PostMessage(hwnd,msg,new IntPtr(keys),LP(cp.X,cp.Y));
    }

    IntPtr LPClient(IntPtr hwnd,POINT sp) {
        POINT cp=sp;
        ScreenToClient(hwnd,ref cp);
        return LP(cp.X,cp.Y);
    }

    int HitTest(IntPtr root,POINT sp) {
        if(root==IntPtr.Zero)return HTCLIENT;
        return SendMessage(root,WM_NCHITTEST,IntPtr.Zero,LP(sp.X,sp.Y)).ToInt32();
    }

    bool IsCaptionButtonHit(int ht) {
        return ht==HTMINBUTTON||ht==HTMAXBUTTON||ht==HTCLOSE;
    }

    void ExecuteCaptionCommand(IntPtr root,int ht) {
        if(root==IntPtr.Zero)return;

        if(ht==HTMINBUTTON) {
            PostMessage(root,WM_SYSCOMMAND,new IntPtr(SC_MINIMIZE),IntPtr.Zero);
        }
        else if(ht==HTMAXBUTTON) {
            int cmd=IsZoomed(root)?SC_RESTORE:SC_MAXIMIZE;
            PostMessage(root,WM_SYSCOMMAND,new IntPtr(cmd),IntPtr.Zero);
        }
        else if(ht==HTCLOSE) {
            PostMessage(root,WM_SYSCOMMAND,new IntPtr(SC_CLOSE),IntPtr.Zero);
        }
    }

    bool IsDoubleClick(IntPtr root,POINT sp) {
        var now=DateTime.UtcNow;
        int maxMs=SystemInformation.DoubleClickTime;
        Size sz=SystemInformation.DoubleClickSize;
        bool ok=(root==lastLeftClickRoot)&&
                ((now-lastLeftClick).TotalMilliseconds<=maxMs)&&
                Math.Abs(sp.X-lastLeftClickPoint.X)<=sz.Width&&
                Math.Abs(sp.Y-lastLeftClickPoint.Y)<=sz.Height;

        lastLeftClick=now;
        lastLeftClickPoint=sp;
        lastLeftClickRoot=root;
        return ok;
    }

    int DetectPortalEntryEdge(Point p) {
        int w=Math.Max(1,ClientSize.Width);
        int h=Math.Max(1,ClientSize.Height);

        if(p.X<=PORTAL_ENTRY_THRESHOLD) return PORTAL_EDGE_LEFT;
        if(p.X>=w-1-PORTAL_ENTRY_THRESHOLD) return PORTAL_EDGE_RIGHT;
        if(p.Y<=PORTAL_ENTRY_THRESHOLD) return PORTAL_EDGE_TOP;
        if(p.Y>=h-1-PORTAL_ENTRY_THRESHOLD) return PORTAL_EDGE_BOTTOM;
        return PORTAL_EDGE_NONE;
    }

    bool HasMonitorPoint(POINT p) {
        foreach(var m in Monitors()) {
            if(p.X>=m.Rect.Left&&p.X<m.Rect.Right&&
               p.Y>=m.Rect.Top&&p.Y<m.Rect.Bottom) return true;
        }
        return false;
    }

    bool BeginPortalInput(Point destinationPoint,POINT sourcePoint) {
        if(portalActive)return true;

        RECT clip=SourceClipRect();
        if(!ClipCursor(ref clip))return false;

        portalActive=true;
        portalBlockedEdge=DetectPortalEntryEdge(destinationPoint);
        lastPortalCursor=sourcePoint;
        lastPortalCursorValid=true;
        lastZone="PORTAL";

        if(!SendAbsoluteMouseMove(sourcePoint)) {
            ReleaseCursorClip(IntPtr.Zero);
            portalActive=false;
            portalBlockedEdge=PORTAL_EDGE_NONE;
            lastPortalCursorValid=false;
            lastZone="PORTAL ERROR";
            return false;
        }

        MarkInput();
        return true;
    }

    void EndPortalToDestination() {
        POINT current;
        POINT destination=new POINT(
            dst.Rect.Left+Math.Max(0,Math.Min(dst.Rect.Width-1,lastClient.X)),
            dst.Rect.Top +Math.Max(0,Math.Min(dst.Rect.Height-1,lastClient.Y)));

        if(GetCursorPos(out current)&&IsSourcePoint(current))
            destination=MapSourceToDestination(current);

        ReleaseCursorClip(IntPtr.Zero);
        portalActive=false;
        portalBlockedEdge=PORTAL_EDGE_NONE;
        lastPortalCursorValid=false;
        SendAbsoluteMouseMove(destination);
        lastZone="PORTAL RELEASED";
        MarkInput();
    }

    bool TryExitPortal(int edge,POINT sourcePoint) {
        POINT destination=MapSourceToDestination(sourcePoint);
        POINT target=destination;

        if(edge==PORTAL_EDGE_LEFT) target.X=dst.Rect.Left-1;
        else if(edge==PORTAL_EDGE_RIGHT) target.X=dst.Rect.Right;
        else if(edge==PORTAL_EDGE_TOP) target.Y=dst.Rect.Top-1;
        else if(edge==PORTAL_EDGE_BOTTOM) target.Y=dst.Rect.Bottom;
        else return false;

        if(!HasMonitorPoint(target))return false;

        ReleaseCursorClip(IntPtr.Zero);
        portalActive=false;
        portalBlockedEdge=PORTAL_EDGE_NONE;
        lastPortalCursorValid=false;
        SendAbsoluteMouseMove(target);
        lastZone="PORTAL EXIT";
        MarkInput();
        return true;
    }

    void UpdatePortalInput() {
        if(!portalActive)return;

        POINT p;
        if(!GetCursorPos(out p))return;

        if(!IsSourcePoint(p)) {
            ReleaseCursorClip(IntPtr.Zero);
            portalActive=false;
            portalBlockedEdge=PORTAL_EDGE_NONE;
            lastPortalCursorValid=false;
            lastZone="PORTAL EXIT";
            return;
        }

        if(portalBlockedEdge==PORTAL_EDGE_LEFT && p.X>=src.Rect.Left+PORTAL_UNBLOCK_MARGIN)
            portalBlockedEdge=PORTAL_EDGE_NONE;
        else if(portalBlockedEdge==PORTAL_EDGE_RIGHT && p.X<=src.Rect.Right-1-PORTAL_UNBLOCK_MARGIN)
            portalBlockedEdge=PORTAL_EDGE_NONE;
        else if(portalBlockedEdge==PORTAL_EDGE_TOP && p.Y>=src.Rect.Top+PORTAL_UNBLOCK_MARGIN)
            portalBlockedEdge=PORTAL_EDGE_NONE;
        else if(portalBlockedEdge==PORTAL_EDGE_BOTTOM && p.Y<=src.Rect.Bottom-1-PORTAL_UNBLOCK_MARGIN)
            portalBlockedEdge=PORTAL_EDGE_NONE;

        if(portalBlockedEdge!=PORTAL_EDGE_LEFT && p.X<=src.Rect.Left+1) {
            if(TryExitPortal(PORTAL_EDGE_LEFT,p))return;
        }
        if(portalBlockedEdge!=PORTAL_EDGE_RIGHT && p.X>=src.Rect.Right-2) {
            if(TryExitPortal(PORTAL_EDGE_RIGHT,p))return;
        }
        if(portalBlockedEdge!=PORTAL_EDGE_TOP && p.Y<=src.Rect.Top+1) {
            if(TryExitPortal(PORTAL_EDGE_TOP,p))return;
        }
        if(portalBlockedEdge!=PORTAL_EDGE_BOTTOM && p.Y>=src.Rect.Bottom-2) {
            if(TryExitPortal(PORTAL_EDGE_BOTTOM,p))return;
        }

        if(!lastPortalCursorValid ||
           p.X!=lastPortalCursor.X || p.Y!=lastPortalCursor.Y) {
            lastPortalCursor=p;
            lastPortalCursorValid=true;
            MarkInput();
        }
    }

    RECT SourceClipRect() {
        return new RECT {
            left=src.Rect.Left,
            top=src.Rect.Top,
            right=src.Rect.Right,
            bottom=src.Rect.Bottom
        };
    }

    bool AcquireCapturedInput(POINT sourcePoint) {
        if(capturedInput)return true;

        POINT old;
        if(!GetCursorPos(out old))
            old=MapSourceToDestination(sourcePoint);

        RECT clip=SourceClipRect();
        if(!ClipCursor(ref clip))return false;

        captureReturnPoint=old;
        capturedInput=true;
        capturePending=false;
        lastCapturedCursor=sourcePoint;
        lastCapturedCursorValid=true;
        lastZone="CAPTURED";

        SendAbsoluteMouseMove(sourcePoint);
        MarkInput();
        return true;
    }

    void BeginCapturedClick(POINT sourcePoint,MouseButtons button) {
        if(!AcquireCapturedInput(sourcePoint))return;

        Application.DoEvents();
        Thread.Sleep(15);
        SendNativeButton(button,true);
        Thread.Sleep(15);
        SendNativeButton(button,false);
        MarkInput();
    }

    void SendNativeWheel(int delta) {
        INPUT[] input=new INPUT[1];
        input[0].type=INPUT_MOUSE;
        input[0].U.mi.mouseData=unchecked((uint)delta);
        input[0].U.mi.dwFlags=MOUSEEVENTF_WHEEL;
        SendInput(1,input,Marshal.SizeOf(typeof(INPUT)));
    }

    void EndCapturedInput() {
        POINT current;
        bool haveCurrent=GetCursorPos(out current);

        ReleaseCursorClip(IntPtr.Zero);
        capturedInput=false;
        capturePending=false;
        lastCapturedCursorValid=false;

        POINT destination=captureReturnPoint;
        if(haveCurrent&&IsSourcePoint(current))
            destination=MapSourceToDestination(current);

        SendAbsoluteMouseMove(destination);
        lastZone="ARMED";
        MarkInput();
    }

    void UpdateCapturedInput() {
        if(!capturedInput)return;

        POINT p;
        if(!GetCursorPos(out p))return;

        if(!IsSourcePoint(p)) {
            ReleaseCursorClip(IntPtr.Zero);
            capturedInput=false;
            capturePending=false;
            lastCapturedCursorValid=false;
            lastZone="ARMED";
            return;
        }

        if(!lastCapturedCursorValid ||
           p.X!=lastCapturedCursor.X || p.Y!=lastCapturedCursor.Y) {
            lastCapturedCursor=p;
            lastCapturedCursorValid=true;
            MarkInput();
        }
    }

    bool IsShellWindow(IntPtr hwnd) {
        if(hwnd==IntPtr.Zero)return false;
        IntPtr root=GetAncestor(hwnd,GA_ROOT);
        if(root!=IntPtr.Zero)hwnd=root;

        uint pid;
        GetWindowThreadProcessId(hwnd,out pid);
        if(pid==0)return false;

        try {
            string name=System.Diagnostics.Process.GetProcessById((int)pid).ProcessName;
            return name.Equals("explorer",StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("ShellExperienceHost",StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("StartMenuExperienceHost",StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("SearchHost",StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("SearchApp",StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    bool SendAbsoluteMouseMove(POINT p) {
        int vx=GetSystemMetrics(SM_XVIRTUALSCREEN);
        int vy=GetSystemMetrics(SM_YVIRTUALSCREEN);
        int vw=GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int vh=GetSystemMetrics(SM_CYVIRTUALSCREEN);
        if(vw<=1||vh<=1)return SetCursorPos(p.X,p.Y);

        int nx=(int)Math.Round((p.X-vx)*65535.0/(vw-1));
        int ny=(int)Math.Round((p.Y-vy)*65535.0/(vh-1));
        nx=Math.Max(0,Math.Min(65535,nx));
        ny=Math.Max(0,Math.Min(65535,ny));

        INPUT[] input=new INPUT[1];
        input[0].type=INPUT_MOUSE;
        input[0].U.mi.dx=nx;
        input[0].U.mi.dy=ny;
        input[0].U.mi.dwFlags=MOUSEEVENTF_MOVE|MOUSEEVENTF_ABSOLUTE|MOUSEEVENTF_VIRTUALDESK;
        if(SendInput(1,input,Marshal.SizeOf(typeof(INPUT)))==1)return true;
        return SetCursorPos(p.X,p.Y);
    }

    void SendNativeButton(MouseButtons button,bool down) {
        uint flag;
        uint data=0;

        if(button==MouseButtons.Right) flag=down?MOUSEEVENTF_RIGHTDOWN:MOUSEEVENTF_RIGHTUP;
        else if(button==MouseButtons.Middle) flag=down?MOUSEEVENTF_MIDDLEDOWN:MOUSEEVENTF_MIDDLEUP;
        else if(button==MouseButtons.XButton1) {
            flag=down?MOUSEEVENTF_XDOWN:MOUSEEVENTF_XUP;
            data=XBUTTON1;
        }
        else if(button==MouseButtons.XButton2) {
            flag=down?MOUSEEVENTF_XDOWN:MOUSEEVENTF_XUP;
            data=XBUTTON2;
        }
        else flag=down?MOUSEEVENTF_LEFTDOWN:MOUSEEVENTF_LEFTUP;

        INPUT[] input=new INPUT[1];
        input[0].type=INPUT_MOUSE;
        input[0].U.mi.dwFlags=flag;
        input[0].U.mi.mouseData=data;
        SendInput(1,input,Marshal.SizeOf(typeof(INPUT)));
    }

    void BeginNativeSourceClick(POINT p,MouseButtons button,bool taskbarInteraction) {
        if(nativeInteraction) EndNativeInteraction();

        POINT old;
        if(!GetCursorPos(out old)) old=MapSourceToDestination(p);

        nativeReturnPoint=old;
        nativeStartForeground=GetForegroundWindow();
        nativeStarted=DateTime.UtcNow;
        nativeLastShellSeen=nativeStarted;
        nativeTaskbarInteraction=taskbarInteraction;
        nativeInteraction=true;
        lastZone=taskbarInteraction?"NATIVE_TASKBAR":"NATIVE_CAPTION";

        SendAbsoluteMouseMove(p);
        Application.DoEvents();
        Thread.Sleep(20);

        SendNativeButton(button,true);
        Thread.Sleep(20);
        SendNativeButton(button,false);
    }

    void EndNativeInteraction() {
        if(!nativeInteraction)return;

        POINT current;
        POINT destination=nativeReturnPoint;
        if(GetCursorPos(out current)&&IsSourcePoint(current))
            destination=MapSourceToDestination(current);

        nativeInteraction=false;
        nativeTaskbarInteraction=false;
        nativeStartForeground=IntPtr.Zero;
        nativeStarted=DateTime.MinValue;
        nativeLastShellSeen=DateTime.MinValue;

        SendAbsoluteMouseMove(destination);
        lastZone="DESKTOP";
    }

    void UpdateNativeInteraction() {
        if(!nativeInteraction)return;

        var now=DateTime.UtcNow;
        double elapsed=(now-nativeStarted).TotalMilliseconds;

        POINT p;
        if(!GetCursorPos(out p))return;
        if(!IsSourcePoint(p)) {
            nativeInteraction=false;
            nativeTaskbarInteraction=false;
            return;
        }

        if(!nativeTaskbarInteraction) {
            if(elapsed>=140)EndNativeInteraction();
            return;
        }

        IntPtr under=WindowFromPoint(p);
        bool shell=IsTaskbarZone(p)||IsShellWindow(under);
        if(shell)nativeLastShellSeen=now;

        IntPtr foreground=GetForegroundWindow();
        bool foregroundChanged=foreground!=IntPtr.Zero&&foreground!=nativeStartForeground;
        if(elapsed>=140&&foregroundChanged&&!IsShellWindow(foreground)) {
            EndNativeInteraction();
            return;
        }

        if(elapsed>=650&&!shell&&(now-nativeLastShellSeen).TotalMilliseconds>=220) {
            EndNativeInteraction();
            return;
        }

        if(elapsed>=30000)EndNativeInteraction();
    }

    void BeginWindowMove(IntPtr target,POINT sp) {
        IntPtr root=GetAncestor(target,GA_ROOT);
        if(root==IntPtr.Zero)return;
        int ht=HitTest(root,sp);
        if(ht==HTCAPTION&&!IsZoomed(root)) {
            RECT r;
            if(GetWindowRect(root,out r)) {
                movingWindow=true;
                movingRoot=root;
                moveStartMouse=sp;
                moveStartRect=r;
            }
        }
    }

    void UpdateWindowMove(POINT sp) {
        if(!movingWindow||movingRoot==IntPtr.Zero)return;
        int dx=sp.X-moveStartMouse.X;
        int dy=sp.Y-moveStartMouse.Y;
        SetWindowPos(movingRoot,IntPtr.Zero,moveStartRect.left+dx,moveStartRect.top+dy,0,0,
            SWP_NOSIZE|SWP_NOZORDER|SWP_NOACTIVATE);
    }

    void EndWindowMove(){movingWindow=false;movingRoot=IntPtr.Zero;}

    bool HandleNonClientLeftDown(IntPtr target,POINT sp) {
        IntPtr root=GetAncestor(target,GA_ROOT);
        if(root==IntPtr.Zero)return false;
        int ht=HitTest(root,sp);
        if(ht==HTCLIENT)return false;
        SetForegroundWindow(root);

        if(ht==HTCAPTION) {
            if(IsDoubleClick(root,sp)) {
                if(IsZoomed(root))ShowWindow(root,SW_RESTORE);
                else ShowWindow(root,SW_MAXIMIZE);
                movingWindow=false; movingRoot=IntPtr.Zero;
                return true;
            }
            BeginWindowMove(target,sp);
            return true;
        }

        SendMessage(root,WM_NCLBUTTONDOWN,new IntPtr(ht),LP(sp.X,sp.Y));
        return true;
    }

    bool HandleNonClientLeftUp(IntPtr target,POINT sp) {
        IntPtr root=GetAncestor(target,GA_ROOT);
        if(root==IntPtr.Zero)return false;
        int ht=HitTest(root,sp);
        if(ht==HTCLIENT)return false;
        SendMessage(root,WM_NCLBUTTONUP,new IntPtr(ht),LP(sp.X,sp.Y));
        return true;
    }

    protected override void OnMouseMove(MouseEventArgs e) {
        MarkInput();
        lastClient=e.Location;
        lastMapped=Map(e.Location);

        if(inputMode=="portal") {
            lastZone=portalActive?"PORTAL":"PORTAL READY";
            if(!portalActive) BeginPortalInput(e.Location,lastMapped);
            base.OnMouseMove(e);
            return;
        }

        if(inputMode=="captured") {
            lastZone=capturedInput?"CAPTURED":"ARMED";
            base.OnMouseMove(e);
            return;
        }

        lastZone=IsTaskbarZone(lastMapped)?"TASKBAR":"DESKTOP";

        if(inputEnabled) {
            if(movingWindow)UpdateWindowMove(lastMapped);
            else if(!IsTaskbarZone(lastMapped)) {
                IntPtr target=dragTarget!=IntPtr.Zero?dragTarget:WindowFromPoint(lastMapped);
                Post(target,WM_MOUSEMOVE,mouseButtons,lastMapped);
            }
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseDown(MouseEventArgs e) {
        MarkInput();
        lastClient=e.Location;
        lastMapped=Map(e.Location);

        if(inputMode=="portal") {
            lastZone=portalActive?"PORTAL":"PORTAL READY";
            if(!portalActive && BeginPortalInput(e.Location,lastMapped)) {
                Application.DoEvents();
                Thread.Sleep(10);
                SendNativeButton(e.Button,true);
            }
            base.OnMouseDown(e);
            return;
        }

        if(inputMode=="captured") {
            lastZone=capturedInput?"CAPTURED":"ARMED";
            capturePending=!capturedInput;
            capturePendingButton=e.Button;
            base.OnMouseDown(e);
            return;
        }

        lastZone=IsTaskbarZone(lastMapped)?"TASKBAR":"DESKTOP";

        if(!inputEnabled){base.OnMouseDown(e);return;}

        pendingNativeClick=false;
        pendingCaptionCommand=false;
        pendingCaptionRoot=IntPtr.Zero;
        pendingCaptionHit=HTCLIENT;

        if(IsTaskbarZone(lastMapped)) {
            pendingNativeClick=true;
            pendingNativeButton=e.Button;
            pendingNativeTaskbar=true;
            base.OnMouseDown(e);
            return;
        }

        IntPtr target=WindowFromPoint(lastMapped);
        if(target==IntPtr.Zero){base.OnMouseDown(e);return;}

        if(e.Button==MouseButtons.Left) {
            IntPtr root=GetAncestor(target,GA_ROOT);
            if(root!=IntPtr.Zero)SetForegroundWindow(root);

            int ht=HitTest(root,lastMapped);
            if(IsCaptionButtonHit(ht)) {
                pendingCaptionCommand=true;
                pendingCaptionRoot=root;
                pendingCaptionHit=ht;
                base.OnMouseDown(e);
                return;
            }

            if(ht==HTCLIENT&&IsCustomCaptionButtonZone(target,lastMapped)) {
                pendingNativeClick=true;
                pendingNativeButton=e.Button;
                pendingNativeTaskbar=false;
                base.OnMouseDown(e);
                return;
            }

            if(HandleNonClientLeftDown(target,lastMapped)){
                base.OnMouseDown(e); return;
            }

            dragTarget=target;
            mouseButtons|=MK_LBUTTON;
            bool dbl=IsDoubleClick(root,lastMapped);
            Post(target,dbl?WM_LBUTTONDBLCLK:WM_LBUTTONDOWN,mouseButtons,lastMapped);
        }
        else if(e.Button==MouseButtons.Right) {
            dragTarget=target; mouseButtons|=MK_RBUTTON;
            Post(target,WM_RBUTTONDOWN,mouseButtons,lastMapped);
        }
        else if(e.Button==MouseButtons.Middle) {
            dragTarget=target; mouseButtons|=MK_MBUTTON;
            Post(target,WM_MBUTTONDOWN,mouseButtons,lastMapped);
        }
        else if(e.Button==MouseButtons.XButton1) {
            dragTarget=target; mouseButtons|=MK_XBUTTON1;
            int wp=(XBUTTON1<<16)|(mouseButtons&0xffff);
            PostMessage(target,WM_XBUTTONDOWN,new IntPtr(wp),LPClient(target,lastMapped));
        }
        else if(e.Button==MouseButtons.XButton2) {
            dragTarget=target; mouseButtons|=MK_XBUTTON2;
            int wp=(XBUTTON2<<16)|(mouseButtons&0xffff);
            PostMessage(target,WM_XBUTTONDOWN,new IntPtr(wp),LPClient(target,lastMapped));
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e) {
        MarkInput();
        lastClient=e.Location;
        lastMapped=Map(e.Location);

        if(inputMode=="portal") {
            lastZone=portalActive?"PORTAL":"PORTAL READY";
            base.OnMouseUp(e);
            return;
        }

        if(inputMode=="captured") {
            lastZone=capturedInput?"CAPTURED":"ARMED";
            bool acquire=capturePending&&!capturedInput&&e.Button==capturePendingButton;
            capturePending=false;
            if(acquire)BeginCapturedClick(lastMapped,e.Button);
            base.OnMouseUp(e);
            return;
        }

        lastZone=IsTaskbarZone(lastMapped)?"TASKBAR":"DESKTOP";

        if(!inputEnabled){
            pendingNativeClick=false;
            pendingCaptionCommand=false;
            pendingCaptionRoot=IntPtr.Zero;
            base.OnMouseUp(e);
            return;
        }

        if(pendingCaptionCommand&&e.Button==MouseButtons.Left) {
            IntPtr root=pendingCaptionRoot;
            int expectedHit=pendingCaptionHit;
            pendingCaptionCommand=false;
            pendingCaptionRoot=IntPtr.Zero;
            pendingCaptionHit=HTCLIENT;

            IntPtr hover=WindowFromPoint(lastMapped);
            IntPtr hoverRoot=hover==IntPtr.Zero?IntPtr.Zero:GetAncestor(hover,GA_ROOT);
            int actualHit=hoverRoot==root?HitTest(root,lastMapped):HTCLIENT;
            if(actualHit==expectedHit)ExecuteCaptionCommand(root,expectedHit);

            base.OnMouseUp(e);
            return;
        }

        if(pendingNativeClick&&e.Button==pendingNativeButton) {
            bool taskbar=pendingNativeTaskbar;
            pendingNativeClick=false;

            if(taskbar) {
                if(IsTaskbarZone(lastMapped))
                    BeginNativeSourceClick(lastMapped,e.Button,true);
            }
            else {
                IntPtr hover=WindowFromPoint(lastMapped);
                if(hover!=IntPtr.Zero&&IsCustomCaptionButtonZone(hover,lastMapped))
                    BeginNativeSourceClick(lastMapped,e.Button,false);
            }

            base.OnMouseUp(e);
            return;
        }

        if(IsTaskbarZone(lastMapped)){base.OnMouseUp(e);return;}

        if(e.Button==MouseButtons.Left&&movingWindow) {
            EndWindowMove();
            base.OnMouseUp(e);
            return;
        }

        IntPtr target=dragTarget!=IntPtr.Zero?dragTarget:WindowFromPoint(lastMapped);

        if(e.Button==MouseButtons.Left&&target!=IntPtr.Zero) {
            if(HandleNonClientLeftUp(target,lastMapped)){
                base.OnMouseUp(e); return;
            }
        }

        if(target!=IntPtr.Zero) {
            if(e.Button==MouseButtons.Left) {
                mouseButtons&=~MK_LBUTTON;
                Post(target,WM_LBUTTONUP,mouseButtons,lastMapped);
            }
            else if(e.Button==MouseButtons.Right) {
                mouseButtons&=~MK_RBUTTON;
                Post(target,WM_RBUTTONUP,mouseButtons,lastMapped);
            }
            else if(e.Button==MouseButtons.Middle) {
                mouseButtons&=~MK_MBUTTON;
                Post(target,WM_MBUTTONUP,mouseButtons,lastMapped);
            }
            else if(e.Button==MouseButtons.XButton1) {
                mouseButtons&=~MK_XBUTTON1;
                int wp=(XBUTTON1<<16)|(mouseButtons&0xffff);
                PostMessage(target,WM_XBUTTONUP,new IntPtr(wp),LPClient(target,lastMapped));
            }
            else if(e.Button==MouseButtons.XButton2) {
                mouseButtons&=~MK_XBUTTON2;
                int wp=(XBUTTON2<<16)|(mouseButtons&0xffff);
                PostMessage(target,WM_XBUTTONUP,new IntPtr(wp),LPClient(target,lastMapped));
            }
        }

        if(mouseButtons==0)dragTarget=IntPtr.Zero;
        base.OnMouseUp(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e) {
        MarkInput();
        lastClient=e.Location;
        lastMapped=Map(e.Location);

        if(inputMode=="portal") {
            lastZone=portalActive?"PORTAL":"PORTAL READY";
            if(!portalActive&&BeginPortalInput(e.Location,lastMapped)) {
                Application.DoEvents();
                Thread.Sleep(10);
                SendNativeWheel(e.Delta);
            }
            base.OnMouseWheel(e);
            return;
        }

        if(inputMode=="captured") {
            lastZone=capturedInput?"CAPTURED":"ARMED";
            if(!capturedInput&&AcquireCapturedInput(lastMapped)) {
                Application.DoEvents();
                Thread.Sleep(10);
                SendNativeWheel(e.Delta);
            }
            base.OnMouseWheel(e);
            return;
        }

        lastZone=IsTaskbarZone(lastMapped)?"TASKBAR":"DESKTOP";

        if(inputEnabled&&!IsTaskbarZone(lastMapped)) {
            IntPtr target=WindowFromPoint(lastMapped);
            if(target!=IntPtr.Zero) {
                int wp=(e.Delta<<16)|(mouseButtons&0xffff);
                PostMessage(target,WM_MOUSEWHEEL,new IntPtr(wp),LP(lastMapped.X,lastMapped.Y));
            }
        }
        base.OnMouseWheel(e);
    }

    protected override void OnPaintBackground(PaintEventArgs e){}
    protected override void OnPaint(PaintEventArgs e){ DrawFrame(); }

    public static string FindSource() {
        foreach(var m in Monitors()) if(m.Rect.Width==2560&&m.Rect.Height==1440)return m.Name;
        return null;
    }

    public static string FindDestination() {
        foreach(var m in Monitors()) if(m.Rect.Width==1920&&m.Rect.Height==1080)return m.Name;
        return null;
    }
}

public static class LauncherV0141
{
    [STAThread]
    public static void Run(string src,string dst,string mode,string inputMode,int cursorHz) {
        MirrorV0141.EnableDpi();
        if(String.IsNullOrWhiteSpace(src))src=MirrorV0141.FindSource();
        if(String.IsNullOrWhiteSpace(dst))dst=MirrorV0141.FindDestination();

        if(src==null)throw new Exception("No 2560x1440 source found.");
        if(dst==null)throw new Exception("No 1920x1080 destination found.");

        Console.WriteLine("Source: "+src);
        Console.WriteLine("Destination: "+dst);
        Console.WriteLine("Render mode: "+mode);
        Console.WriteLine("Input mode: "+inputMode);
        Console.WriteLine("Cursor polling: "+cursorHz+" Hz");

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MirrorV0141(src,dst,mode,inputMode,cursorHz));
    }
}
'@

    if ($startupLabel) {
        $startupLabel.Text = "Compiling mirror engine..."
        [System.Windows.Forms.Application]::DoEvents()
    }

    Add-Type -TypeDefinition $code -Language CSharp -ReferencedAssemblies "System.Windows.Forms","System.Drawing"

    if ($startupLabel) {
        $startupLabel.Text = "Detecting displays and starting Portal..."
        [System.Windows.Forms.Application]::DoEvents()
    }

    if ($startupForm -and -not $startupForm.IsDisposed) {
        $startupForm.Close()
        $startupForm.Dispose()
        $startupForm = $null
    }

    [LauncherV0141]::Run($SourceDevice,$DestinationDevice,$Mode,$InputMode,$CursorHz)
}
catch {
    try {
        if ($startupForm -and -not $startupForm.IsDisposed) {
            $startupForm.Close()
            $startupForm.Dispose()
        }
    } catch {}

    $message = $_.Exception.Message
    if ([string]::IsNullOrWhiteSpace($message)) {
        $message = $_.Exception.ToString()
    }

    try {
        [System.Windows.Forms.MessageBox]::Show(
            "VirtualQHDMirror could not start.`r`n`r`n" + $message,
            "VirtualQHDMirror v0.14.1",
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Error
        ) | Out-Null
    }
    catch {
        Write-Error $message
    }

    exit 1
}
finally {
    try {
        if ($startupForm -and -not $startupForm.IsDisposed) {
            $startupForm.Close()
            $startupForm.Dispose()
        }
    } catch {}
}
