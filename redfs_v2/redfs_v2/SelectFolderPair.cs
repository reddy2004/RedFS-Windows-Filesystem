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
    public partial class SelectFolderPair : Form
    {
        public bool isfile = false;
        public bool result_okay = false;
        public string srcpath = "";
        public string destpath = "";

        public SelectFolderPair()
        {
            InitializeComponent();
        }

        private String ADFN(String path)
        {
            char[] sep = { '\\' };
            String[] list = path.Split(sep, StringSplitOptions.RemoveEmptyEntries);
            return list[list.Length - 1]; //last entry
        }

        private void button4_Click(object sender, EventArgs e)
        {
            FileDialog fd = new OpenFileDialog();
            if (fd.ShowDialog() == DialogResult.OK)
            {
                string filepath = fd.FileName;
                sfp_srcfolder.Text = filepath;
                sfp_destfolder.Text = ADFN(filepath);
                isfile = true;
            }
            else
            {
                return;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog1 = new FolderBrowserDialog();
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                string folderPath = folderBrowserDialog1.SelectedPath;
                sfp_srcfolder.Text = folderPath;
                sfp_destfolder.Text = ADFN(folderPath);
                isfile = false;
            }
            else
            {
                return;
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            result_okay = false;
            this.Close();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            result_okay = true;
            srcpath = sfp_srcfolder.Text;
            destpath = sfp_destfolder.Text;
            this.Close();
        }
    }
}
