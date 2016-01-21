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
    public partial class New_Job_Name : Form
    {
        public bool result_okay = false;
        public string job_name;

        public New_Job_Name(string taskname, int jc)
        {
            InitializeComponent();
            textBox1.Text = taskname;
            textBox2.Text = jc.ToString();
        }

        private void New_Job_Name_Load(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            result_okay = false;
            job_name = null;
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            result_okay = true;
            job_name = textBox3.Text;
            this.Close();
        }
    }
}
