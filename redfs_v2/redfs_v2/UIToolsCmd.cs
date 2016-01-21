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
    public partial class UIToolsCmd : Form
    {
        private string optype;
        private string inodetype;
        private string mkey1;
        private string mkey2;

        private int fsid1number = 0;
        private int fsid2number = 0;

        public bool result = false;

        public string get_result()
        {
            return mkey2;
        }

        public UIToolsCmd(string op, string itype, string key1, string key2, int f1, int f2)
        {
            InitializeComponent();
            optype = op;
            inodetype = itype;
            mkey1 = key1;
            mkey2 = key2;
            fsid1number = f1;
            fsid2number = f2;
        }

        private void UIToolsCmd_Load(object sender, EventArgs e)
        {
            textBox1.Text = optype;
            textBox2.Text = inodetype;
            textBox1.Enabled = false;
            textBox2.Enabled = false;
            textBox3.Text = mkey1;
            textBox3.Enabled = false;
            textBox4.Text = mkey2;
            fsid1.Text = fsid1number.ToString();
            fsid2.Text = fsid2number.ToString();

            fsid1.Enabled = false;
            fsid2.Enabled = false;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            mkey2 = null;
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            mkey2 = textBox4.Text;
            this.Close();
        }
    }
}
