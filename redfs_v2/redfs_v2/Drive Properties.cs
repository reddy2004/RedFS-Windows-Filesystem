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
    public partial class Drive_Properties : Form
    {
        public Drive_Properties(RedFS_FSID fsid)
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Drive_Properties_Load(object sender, EventArgs e)
        {

        }
    }
}
