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
    public partial class Dedupe_UI : Form
    {
        public Dedupe_UI()
        {
            InitializeComponent();
        }

        public void Update_DedupeUI(DEDUP_ENGINE_STAGE stage, int i)
        {
            if (InvokeRequired)
            {
                this.BeginInvoke(new Action<DEDUP_ENGINE_STAGE, int>(Update_DedupeUI), new object[] { stage, i });
                return;
            }
            switch (stage)
            { 
                case DEDUP_ENGINE_STAGE.NOT_STARTED:
                    break;
                case DEDUP_ENGINE_STAGE.SORT_CLOG_INOFBN:
                    pclog_1.Value = i;
                    break;
                case DEDUP_ENGINE_STAGE.CLEAN_CLOG_INOFBNSTALES:
                    pclog_2.Value = i;
                    break;
                case DEDUP_ENGINE_STAGE.SORT_CLOG_DBN:
                    pclog_3.Value = i;
                    break;
                case DEDUP_ENGINE_STAGE.CLEAN_CLOG_DBNSTALES:
                    pclog_4.Value = i;
                    break;
                case DEDUP_ENGINE_STAGE.SORT_CLOG_FPORDER:
                    pclog_5.Value = i;
                    break;
                case DEDUP_ENGINE_STAGE.CLEAN_FPDB_DBNSTALES:
                    pfpdb_1.Value = i;
                    break;
                case DEDUP_ENGINE_STAGE.SORT_FPDB_FPORDER:
                    pfpdb_2.Value = i;
                    break;
                case DEDUP_ENGINE_STAGE.GENERATE_DEDUPE_SCRIPT:
                    pgen_1.Value = i;
                    break;
                case DEDUP_ENGINE_STAGE.SORT_FPDBT_DBN:
                    pgen_2.Value = i;
                    break;
                case DEDUP_ENGINE_STAGE.SORT_SCRIPT_INOFBN:
                    pgen_3.Value = i;
                    break;
                case DEDUP_ENGINE_STAGE.DEDUPE_MESSAGES:
                    pmsg_1.Value = i;
                    break;
                case DEDUP_ENGINE_STAGE.DEDUPE_DONE:
                    Close();
                    break;
            }
        }

        private void cancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void Dedupe_UI_Load(object sender, EventArgs e)
        {
            DedupeEngine de = new DedupeEngine(this);
            de.start_dedupe_async();
        }
    }
}
