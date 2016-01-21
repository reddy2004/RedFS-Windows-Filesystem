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
    public partial class New_Backup_Task : Form
    {
        public bool result_okay = false;
        public string task_name = "";
        public List<backup_pair> mlist = new List<backup_pair>();

        public New_Backup_Task()
        {
            InitializeComponent();
        }

        private void New_Backup_Job_Load(object sender, EventArgs e)
        {

        }

        private void button4_Click(object sender, EventArgs e)
        {
            result_okay = false;
            this.Close();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            SelectFolderPair sfp = new SelectFolderPair();
            sfp.ShowDialog();

            if (sfp.result_okay == true)
            { 
                //verify sanity..
                backup_pair bi = new backup_pair();
                bi.IsFile = sfp.isfile;
                bi.SourcePath = sfp.srcpath;
                bi.DestinationPath = sfp.destpath;
                mlist.Add(bi);
                dgv_backup_pairs.Rows.Add(new string[] { sfp.srcpath, sfp.destpath });
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            result_okay = true;
            task_name = textBox3.Text;
            this.Close();
        }
    }
}
