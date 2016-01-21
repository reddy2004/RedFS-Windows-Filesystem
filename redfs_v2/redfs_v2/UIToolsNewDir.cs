using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace redfs_v2
{
    public partial class UIToolsNewDir : Form
    {
        public string newfoldername;

        public UIToolsNewDir()
        {
            InitializeComponent();
        }

        private void UIToolsNewDir_Load(object sender, EventArgs e)
        {
            newfoldername = "New Folder";
        }

        private void button2_Click(object sender, EventArgs e)
        {
            newfoldername = null;
            Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            newfoldername = textBox1.Text;
            Close();
        }
    }
}
