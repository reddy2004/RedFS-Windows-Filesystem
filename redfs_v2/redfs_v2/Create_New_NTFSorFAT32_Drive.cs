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
    public partial class Create_New_NTFSorFAT32_Drive : Form
    {
        public int drive_id = 0;
        public int drive_size = 0;
        public bool entered = false;

        public Create_New_NTFSorFAT32_Drive()
        {
            InitializeComponent();
        }

        private void newlun_ok_Click(object sender, EventArgs e)
        {
            try
            {
                drive_id = Int32.Parse(drive_id_tb.Text);
                drive_size = Int32.Parse(drive_size_tb.Text);
            }
            catch (Exception exx)
            {
                DEFS.DEBUG("lun ui", exx.Message);
                MessageBox.Show("Error in parsing values. Please enter digits only");
                drive_id = drive_size = 0;
                return;
            }
            entered = true;
            this.Close();
        }

        private void newlun_cancel_Click(object sender, EventArgs e)
        {
            entered = false;
            this.Close();
        }

        private void Create_New_NTFSorFAT32_Drive_Load(object sender, EventArgs e)
        {

        }
    }
}
