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
using System.IO;
using System.Threading;

namespace redfs_v2
{
    public partial class NewConfigurationUI : Form
    {
        public string selectedfolder = null;

        BackgroundWorker worker;

        int f1blks = 0;
        int f2blks = 0;
        int f3blks = 0;

        public NewConfigurationUI()
        {
            InitializeComponent();
            progressLabel.Text = "Waiting..";
        }

        private void newConfigCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void disable_all_ui_items()
        {
            button1.Enabled = false;
            newConfigCancel.Enabled = false;
            textBox2.Enabled = false;
            comboBox1.Enabled = false;
            button3.Enabled = false;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog1 = new FolderBrowserDialog();
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                string path = folderBrowserDialog1.SelectedPath;
                if (CONFIG.CheckIfValidFolder(path) == CONFIGERROR.OKAY)
                {
                    MessageBox.Show("This folder already contains a valid virtual disk!", "Error", MessageBoxButtons.OK);
                }
                else
                {
                    selectedfolder = folderBrowserDialog1.SelectedPath;
                    textBox1.Text = selectedfolder;
                }
            }
        }
        void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            button1.Enabled = true;
        }

        void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            int total_blks = f1blks + f2blks + f3blks;
            int complete_blks = 0;
            byte[] buffer = new byte[4096];
            byte[] encbuf = new byte[4096];

            FileStream f1 = new FileStream(selectedfolder + "\\disk2", FileMode.OpenOrCreate);
            for (int i = 0; i < f1blks; i++)
            {
                f1.Write(buffer, 0, 4096);
                complete_blks++;
                worker.ReportProgress(100*complete_blks / total_blks, "Creating disk : " + selectedfolder + "\\disk2");
            }

            FileStream f2 = new FileStream(selectedfolder + "\\RFI2.dat", FileMode.OpenOrCreate);
            CONFIG.GenerateXORBuf(selectedfolder, encbuf);
            for (int i = 0; i < f2blks; i++)
            {
                f2.Write(encbuf, 0, 4096);
                complete_blks++;
                worker.ReportProgress(100*complete_blks / total_blks, "Creating refcount file : " + selectedfolder + "\\RFI2.dat");
            }

            FileStream f3 = new FileStream(selectedfolder + "\\allocationmap", FileMode.OpenOrCreate);
            for (int i = 0; i < f3blks; i++)
            {
                f3.Write(buffer, 0, 4096);
                complete_blks++;
                worker.ReportProgress(100*complete_blks / total_blks, "Creating AMap : " + selectedfolder + "\\allocationmap");
            }
            f1.Flush();
            f2.Flush();
            f3.Flush();
            f1.Close();
            f2.Close();
            f3.Close();

            CONFIG.CreateConfigInformation2(selectedfolder);
            worker.ReportProgress(-1, "finish!!");
        }

        void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage == -1)
            {
                Close();
                DialogResult = System.Windows.Forms.DialogResult.OK;
                return;
            }
            progressBar1.Value = e.ProgressPercentage;
            progressLabel.Text = (string)e.UserState;
            Update();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            bool error = false;
            string errstr = "";

            if (textBox1.Text == "")
            {
                error = true;
                errstr = "Please choose a valid folder";
            }
            else if (!(comboBox1.Text != "GB" && comboBox1.Text != "TB"))
            {
                int value = 9999;
                try
                {
                    value = Int32.Parse(textBox2.Text);
                }
                catch (Exception ex) 
                {
                    Console.WriteLine(ex.Message);
                    errstr = "Please choose digits only for Virtual Disk Size";
                    textBox2.Text = "0";
                    error = true;
                }
                
                if (!error && comboBox1.Text == "GB" && value <= 8192 && value >= 1)
                {
                    f1blks = value * (262144);
                    f2blks = (value * (262144)) / 512;
                    f3blks = OPS.OffsetToFBN((f1blks / 8)) + 1;
                }
                else if (!error && comboBox1.Text == "TB" && value <= 8 && value >= 1)
                {
                    f1blks = value * (262144) * 1024;
                    f2blks = ((value * (262144)) /512) * 1024;
                    f3blks = OPS.OffsetToFBN((f1blks / 8)) + 1;
                }
                else if (!error)
                {
                    error = true;
                    errstr = "Please choose size withing specified limits";
                }
            }

            if (error) 
            {
                MessageBox.Show(errstr, "Check VDSize");
                return;
            }

            disable_all_ui_items();
            
            worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.ProgressChanged += worker_ProgressChanged;
            worker.DoWork += worker_DoWork;
            worker.RunWorkerCompleted += worker_RunWorkerCompleted;

            worker.RunWorkerAsync();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void NewConfigurationUI_Load(object sender, EventArgs e)
        {
            if (selectedfolder != null)
            {
                textBox1.Text = selectedfolder;
                button3.Enabled = false;
            }
        }
    }
}
