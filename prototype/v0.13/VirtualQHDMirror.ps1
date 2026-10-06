param(
    [string]$SourceDevice = "",
    [string]$DestinationDevice = "",
    [ValidateSet("adaptive","30","60")]
    [string]$Mode = "adaptive"
)

$ErrorActionPreference = "Stop"

try {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing

    $code = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

public sealed class MirrorV013 : Form
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
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint processId);
    [DllImport("user32.dll")] static extern bool GetIconInfo(IntPtr hIcon,out ICONINFO info);
    [DllImport("user32.dll")] static extern uint SendInput(uint nInputs,INPUT[] inputs,int cbSize);
    [DllImport("user32.dll")] static extern bool GetCursorInfo(ref CURSORINFO info);
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
    const uint WM_HOTKEY=0x0312;

    const int HTCLIENT=1, HTCAPTION=2;
    const int MK_LBUTTON=1, MK_RBUTTON=2, MK_MBUTTON=0x10;
    const int MK_XBUTTON1=0x20, MK_XBUTTON2=0x40;
    const int XBUTTON1=1, XBUTTON2=2;

    const uint GA_ROOT=2;
    const uint MOD_ALT=1, MOD_CONTROL=2;
    const int HK_EXIT=1, HK_INPUT=2, HK_OVERLAY=3, HK_MODE=4;
    const int VK_F9=0x78, VK_F10=0x79, VK_F11=0x7A, VK_F12=0x7B;
    const int WS_EX_NOACTIVATE=0x08000000, WS_EX_TOOLWINDOW=0x80;
    const uint SWP_NOSIZE=0x0001, SWP_NOZORDER=0x0004, SWP_NOACTIVATE=0x0010;

    const int SW_RESTORE=9, SW_MAXIMIZE=3;

    const uint CURSOR_SHOWING=0x00000001;
    const uint DI_NORMAL=0x0003;

    const uint INPUT_MOUSE=0;
    const uint MOUSEEVENTF_LEFTDOWN=0x0002;
    const uint MOUSEEVENTF_LEFTUP=0x0004;
    const uint MOUSEEVENTF_RIGHTDOWN=0x0008;
    const uint MOUSEEVENTF_RIGHTUP=0x0010;
    const uint MOUSEEVENTF_MIDDLEDOWN=0x0020;
    const uint MOUSEEVENTF_MIDDLEUP=0x0040;
    const uint MOUSEEVENTF_XDOWN=0x0080;
    const uint MOUSEEVENTF_XUP=0x0100;

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

    bool inputEnabled=true;
    bool overlay=false;

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

    bool nativeInteraction=false;
    bool nativeTaskbarInteraction=false;
    POINT nativeReturnPoint;
    IntPtr nativeStartForeground=IntPtr.Zero;
    DateTime nativeStarted=DateTime.MinValue;
    DateTime nativeLastShellSeen=DateTime.MinValue;

    public static void EnableDpi(){ try{SetProcessDpiAwarenessContext(PMV2);}catch{} }

    public MirrorV013(string srcName,string dstName,string mode) {
        renderMode=mode;

        foreach(var m in Monitors()) {
            if(String.Equals(m.Name,srcName,StringComparison.OrdinalIgnoreCase)) src=m;
            if(String.Equals(m.Name,dstName,StringComparison.OrdinalIgnoreCase)) dst=m;
        }
        if(src==null) throw new Exception("Source not found: "+srcName);
        if(dst==null) throw new Exception("Destination not found: "+dstName);

        srcDC=CreateDC("DISPLAY",src.Name,null,IntPtr.Zero);
        if(srcDC==IntPtr.Zero) throw new Exception("CreateDC failed: "+src.Name);

        Text="VirtualQHDMirror v0.13";
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
            UpdateNativeInteraction();
            DrawFrame();
        };

        Shown+=delegate{
            Bounds=dst.Rect;
            dstDC=GetDC(Handle);
            if(dstDC==IntPtr.Zero) throw new Exception("GetDC failed for mirror window.");

            SetStretchBltMode(dstDC,HALFTONE);
            SetBrushOrgEx(dstDC,0,0,IntPtr.Zero);

            RegisterHotKey(Handle,HK_EXIT,MOD_CONTROL|MOD_ALT,VK_F12);
            RegisterHotKey(Handle,HK_INPUT,MOD_CONTROL|MOD_ALT,VK_F11);
            RegisterHotKey(Handle,HK_OVERLAY,MOD_CONTROL|MOD_ALT,VK_F10);
            RegisterHotKey(Handle,HK_MODE,MOD_CONTROL|MOD_ALT,VK_F9);

            ApplyInterval(16);
            timer.Start();
        };

        FormClosed+=delegate{
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
        };
    }

    protected override CreateParams CreateParams {
        get { var cp=base.CreateParams; cp.ExStyle|=WS_EX_NOACTIVATE|WS_EX_TOOLWINDOW; return cp; }
    }
    protected override bool ShowWithoutActivation { get { return true; } }

    protected override void WndProc(ref Message m) {
        if(m.Msg==WM_HOTKEY) {
            int id=m.WParam.ToInt32();
            if(id==HK_EXIT){ Close(); return; }
            if(id==HK_INPUT){
                if(nativeInteraction) EndNativeInteraction();
                pendingNativeClick=false;
                inputEnabled=!inputEnabled;
                return;
            }
            if(id==HK_OVERLAY){ overlay=!overlay; return; }
            if(id==HK_MODE){
                renderMode = renderMode=="adaptive" ? "30" : (renderMode=="30" ? "60" : "adaptive");
                lastInput=DateTime.UtcNow;
                return;
            }
        }
        base.WndProc(ref m);
    }

    void ApplyInterval(int ms) {
        if(ms<8) ms=8;
        if(lastInterval==ms) return;
        lastInterval=ms;
        timer.Interval=ms;
    }

    void AdjustInterval() {
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

    void DrawFrame() {
        if(dstDC==IntPtr.Zero) return;

        StretchBlt(
            dstDC,
            0,0,ClientSize.Width,ClientSize.Height,
            srcDC,
            0,0,src.Rect.Width,src.Rect.Height,
            SRCCOPY);

        DrawSourceCursorIfNeeded();

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
            string t=
                "VirtualQHDMirror v0.13\n"+
                "render: "+renderMode+" / "+(1000.0/Math.Max(1,lastInterval)).ToString("0")+" fps target\n"+
                "zone: "+lastZone+"\n"+
                "input: "+(inputEnabled?"ON":"OFF");
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

        SetCursorPos(p.X,p.Y);
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

        SetCursorPos(destination.X,destination.Y);
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
        lastZone=IsTaskbarZone(lastMapped)?"TASKBAR":"DESKTOP";

        if(!inputEnabled){base.OnMouseDown(e);return;}

        pendingNativeClick=false;

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

            if(IsCustomCaptionButtonZone(target,lastMapped)) {
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
        lastZone=IsTaskbarZone(lastMapped)?"TASKBAR":"DESKTOP";

        if(!inputEnabled){
            pendingNativeClick=false;
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

public static class LauncherV013
{
    [STAThread]
    public static void Run(string src,string dst,string mode) {
        MirrorV013.EnableDpi();
        if(String.IsNullOrWhiteSpace(src))src=MirrorV013.FindSource();
        if(String.IsNullOrWhiteSpace(dst))dst=MirrorV013.FindDestination();

        if(src==null)throw new Exception("No 2560x1440 source found.");
        if(dst==null)throw new Exception("No 1920x1080 destination found.");

        Console.WriteLine("Source: "+src);
        Console.WriteLine("Destination: "+dst);
        Console.WriteLine("Render mode: "+mode);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MirrorV013(src,dst,mode));
    }
}
'@

    Add-Type -TypeDefinition $code -Language CSharp -ReferencedAssemblies "System.Windows.Forms","System.Drawing"
    [LauncherV013]::Run($SourceDevice,$DestinationDevice,$Mode)
}
catch {
    Write-Host ""
    Write-Host "ERROR:" -ForegroundColor Red
    Write-Host $_.Exception.ToString() -ForegroundColor Red
    Read-Host "Press Enter to close"
    exit 1
}
