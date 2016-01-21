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
    public partial class Exit_Saving : Form
    {
        public Exit_Saving()
        {
            InitializeComponent();
        }

        private void Exit_Saving_Load(object sender, EventArgs e)
        {
            progressBar1.Maximum = 100;
            progressBar1.Value = 50;
            progressBar1.Invalidate();
        }

        public void setprogress(int value)
        {
            progressBar1.Value = 50;
            progressBar1.Invalidate();
        }

        private void progressBar1_Click(object sender, EventArgs e)
        {

        }
    }
}
