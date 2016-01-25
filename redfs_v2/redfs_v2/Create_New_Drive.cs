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
    public partial class Create_New_Drive : Form
    {
        public string newdrive;
        public string comments = "";

        public bool set_value_true = false;

        public Create_New_Drive(string backingdrive, string newdrive)
        {
            InitializeComponent();
            newlabel1.Text = newdrive;
            backinglabel.Text = backingdrive;
        }

        private void Create_New_Drive_Load(object sender, EventArgs e)
        {

        }

        private void newlabel1_TextChanged(object sender, EventArgs e)
        {
            if (newlabel1.Text.Length > 128)
                newlabel1.Text = newlabel1.Text.Substring(0, 128);
            newdrive = newlabel1.Text;
        }

        private void cancelb_Click(object sender, EventArgs e)
        {
            set_value_true = false;
            this.Close();
        }

        private void saveexit_Click(object sender, EventArgs e)
        {
            set_value_true = true;
            this.Close();
        }

        private void newdrivecomments_TextChanged(object sender, EventArgs e)
        {
            if (newdrivecomments.Text.Length > 1024)
                newdrivecomments.Text = newdrivecomments.Text.Substring(0, 1024);
            comments = newdrivecomments.Text;
        }
    }
}
