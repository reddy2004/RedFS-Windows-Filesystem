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
