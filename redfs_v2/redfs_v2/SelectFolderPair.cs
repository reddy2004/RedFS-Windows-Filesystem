/*
The license text is further down this page, and you should only download and use the source code 
if you agree to the terms in that text. For convenience, though, I’ve put together a human-readable 
(as opposed to lawyer-readable) non-authoritative interpretation of the license which will hopefully 
answer any questions you have. Basically, the license says that:

1. You can use the code in your own products.
2. You can modify the code as you wish, and use the modified code in your free products.
3. You can redistribute the original, unmodified code, but you have to include the full license text below.
4. You can redistribute the modified code as you wish (without the full license text below).
5. In all cases, you must include a credit mentioning 'Vikrama Reddy' as the original author of the source.
6. I'm not liable for anything you do with the code, no matter what. So be sensible.
7. You can't use my name or other marks to promote your products based on the code.
8. If you agree to all of that, go ahead and download the source. Otherwise, don't.
9. Derived work must have 'redfs' in the title. Ex. RedFS-advanced, Lite-Redfs, XRedfs etc.
*/

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
