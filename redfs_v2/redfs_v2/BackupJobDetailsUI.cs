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
    public partial class BackupJobDetailsUI : Form
    {
        public BackupJobDetailsUI(string name, List<backup_pair> pairs, List<job_item> joblist)
        {
            InitializeComponent();
            textBox3.Text = name;

            if (pairs != null)
            {
                for (int i = 0; i < pairs.Count; i++)
                {
                    backup_pair bp = pairs.ElementAt(i);
                    dgv_fldlist.Rows.Add(new string[] { bp.SourcePath, bp.DestinationPath, (bp.IsFile)? "Y": "N" });
                }
                dgv_fldlist.Invalidate();
            }

            if (joblist != null)
            {
                for (int i = 0; i < joblist.Count; i++) 
                {
                    job_item ji = joblist.ElementAt(i);
                    dgv_joblist.Rows.Add(new string[] {ji.JobID.ToString(), ji.JobName, ji.StartTime, DEFS.getDataInStringRep(ji.NewCopiedData) });
                }
                dgv_joblist.Invalidate();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void BackupJobDetailsUI_Load(object sender, EventArgs e)
        {

        }
    }
}
