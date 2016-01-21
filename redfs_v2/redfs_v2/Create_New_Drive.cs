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
    public partial class Create_New_Drive : Form
    {
        public string newdrive;
        public string comments = "";

        public bool set_value_true = false;

        public Create_New_Drive(string backingdrive, string newdrive)
        {
            InitializeComponent();
            newlabel1.Text = newdrive;
            backinglabel.Text = backingdrive;
        }

        private void Create_New_Drive_Load(object sender, EventArgs e)
        {

        }

        private void newlabel1_TextChanged(object sender, EventArgs e)
        {
            if (newlabel1.Text.Length > 128)
                newlabel1.Text = newlabel1.Text.Substring(0, 128);
            newdrive = newlabel1.Text;
        }

        private void cancelb_Click(object sender, EventArgs e)
        {
            set_value_true = false;
            this.Close();
        }

        private void saveexit_Click(object sender, EventArgs e)
        {
            set_value_true = true;
            this.Close();
        }

        private void newdrivecomments_TextChanged(object sender, EventArgs e)
        {
            if (newdrivecomments.Text.Length > 1024)
                newdrivecomments.Text = newdrivecomments.Text.Substring(0, 1024);
            comments = newdrivecomments.Text;
        }
    }
}
