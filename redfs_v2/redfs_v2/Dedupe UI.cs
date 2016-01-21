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
