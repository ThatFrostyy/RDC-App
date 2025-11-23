using Server.Connection;
using Server.MessagePack;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Server.Handle_Packet
{
    public class HandleSystemInfo
    {
        public void Show(Clients client, MsgPack unpack_msgpack)
        {
            try
            {
                // Build a simple object with fields pulled from MsgPack
                string cpu = unpack_msgpack.ForcePathObject("CPU").AsString;
                string gpu = unpack_msgpack.ForcePathObject("GPU").AsString;
                string ram = unpack_msgpack.ForcePathObject("RAM").AsString;
                string motherboard = unpack_msgpack.ForcePathObject("Motherboard").AsString;
                string disks = unpack_msgpack.ForcePathObject("Disks").AsString;
                string cameras = unpack_msgpack.ForcePathObject("Cameras").AsString;
                string mouse = unpack_msgpack.ForcePathObject("Mouse").AsString;
                string keyboard = unpack_msgpack.ForcePathObject("Keyboard").AsString;
                string headphones = unpack_msgpack.ForcePathObject("Headphones").AsString;

                Program.form1.Invoke((MethodInvoker)(() =>
                {
                    var form = new Server.Forms.FormSystemInfo(client, cpu, gpu, ram, motherboard, disks, cameras, mouse, keyboard, headphones);
                    form.Show(); // non-modal; use ShowDialog() if you prefer modal
                }));
            }
            catch { }
        }
    }
}