using Server.Connection;
using Server.Properties;
using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Server.Forms
{
    public class FormSystemInfo : Form
    {
        private DataGridView grid;
        private Clients client;

        public FormSystemInfo(Clients client, string cpu, string gpu, string ram, string motherboard, string disks, string cameras, string mouse, string keyboard, string headphones)
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormSystemInfo));

            this.client = client;
            this.Text = $"DarkNET | System Info";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Size = new Size(600, 480);
            this.MinimumSize = new Size(600, 400);
            this.Font = new Font("Segoe UI", 9);

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,              
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    WrapMode = DataGridViewTriState.True,
                    Font = new Font("Segoe UI", 9)
                }
            };

            grid.Columns.Add("Category", "Category");
            grid.Columns.Add("Value", "Value");
            grid.Columns[0].Width = 180;
            grid.Columns[1].Width = 480;

            AddItem("CPU", cpu);
            AddItem("GPU", gpu);
            AddItem("RAM", ram);
            AddItem("Motherboard", motherboard);
            AddItem("Disks", disks);
            AddItem("Cameras", cameras);
            AddItem("Mouse", mouse);
            AddItem("Keyboard", keyboard);
            AddItem("Headphones", headphones);

            Controls.Add(grid);
            AddCopyButton();
        }

        private void AddItem(string category, string value)
        {
            grid.Rows.Add(category, value ?? "N/A");
        }

        private void AddCopyButton()
        {
            var btnCopy = new Button
            {
                Text = "Copy Info",
                Dock = DockStyle.Bottom,
                Height = 30
            };
            btnCopy.Click += (s, e) =>
            {
                var sb = new StringBuilder();
                foreach (DataGridViewRow row in grid.Rows)
                    sb.AppendLine($"{row.Cells[0].Value}: {row.Cells[1].Value}");
                Clipboard.SetText(sb.ToString());
            };
            Controls.Add(btnCopy);
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormSystemInfo));
            this.SuspendLayout();
            // 
            // FormSystemInfo
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "FormSystemInfo";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "DarkNET | System Info";
            this.ResumeLayout(false);

        }
    }
}
