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
using System.Threading;
using Dokan;
using System.IO;
using System.Collections;
using System.Xml.Serialization;
using System.Diagnostics;

namespace redfs_v2
{
    public partial class RedFS_Drive : Form
    {
        private int m_curr_row = -1; //for first tab
        private int m_curr_row3 = -1; //for backup
        public RedFS_Drive()
        {
            InitializeComponent();
            //init_lun_items();
        }

        private void Reload_BackupTask_Display()
        {
            DEFS.DEBUG("DELTA", "Loading backup-tasks information for dataGridView");
            dataGridView3.Rows.Clear();

            Inode_Info[] inodes = REDDY.ptrIFSDMux.FindFilesInternalAPI(2, "\\");

            if (inodes == null) return;

            for (int i = 0; i < inodes.Length; i++)
            {
                //dataGridView3.Rows[i].Cells[0].Value = inodes[i].name;
                dataGridView3.Rows.Add(new string[] {inodes[i].name, inodes[i].CreationTime.ToLongDateString(), "-","-","-" });
            }
            dataGridView3.Invalidate();               
        }

        private void Reload_FSinfo_Display()
        {
            DEFS.DEBUG("FSID", "Loading fs information for dataGridView");
            int num = 0;

            for (int i = 0; i < 1024; i++)
            {
                RedFS_FSID fs = REDDY.ptrRedFS.redfs_load_fsid(i, false);
                string[] strx = (fs != null) ? fs.get_data_for_datagridview() : null;

                if (strx != null)
                {
                    DEFS.DEBUG("FSID", "Found " + i + " fsinfo during boot/reload");
                    dataGridView1.Rows.Add(strx);
                    dataGridView1.Rows[dataGridView1.RowCount - 1].HeaderCell.ContextMenuStrip = contextMenuStrip2;
                    //dataGridView1.Rows[dataGridView1.RowCount - 1].DefaultCellStyle.BackColor = Color.LightPink; 
                    REDDY.FSIDList[i] = fs;
                    num++;
                }
                else
                {
                    REDDY.FSIDList[i] = null;
                }
            }
            DEFS.DEBUG("FSID", "Loaded " + num + " fsinfo's from the disk");
            dataGridView1.Invalidate();        
        }

        public void UpdateThread()
        {
            while (true)
            {
                Thread.Sleep(1000);
                Update_DGV();
                //Reload_LUNinfo_Display(0);
                //update_PictureBox();
                if (REDDY.ptrRedFS == null || REDDY.ptrIFSDMux == null)
                {
                    update_Summary_statistics(0, 0, 0);
                    continue;
                }
                else
                {
                    ulong freeBytesAvailable = 0;
                    ulong totalBytes = 0;
                    ulong totalFreeBytes = 0;
                    long logicaldata = 0;
                    long vsize = 0;
                    REDDY.ptrIFSDMux.GetDiskFreeSpace(ref freeBytesAvailable, ref totalBytes, ref totalFreeBytes, ref vsize, ref logicaldata);
                    update_Summary_statistics((long)vsize, (long)freeBytesAvailable, logicaldata);
                }
            }
        }
        
        private void update_Summary_statistics(long vsize, long freespace, long ldata)
        {
            if (InvokeRequired)
            {
                this.BeginInvoke(new Action<long, long, long>(update_Summary_statistics), new object[] { vsize, freespace, ldata });
                return;
            }

            if (vsize == 0) 
            {
                stat_vdsize.Text = "-";
                stat_freespace.Text = "-";
                stat_logicaldata.Text = "-";
                stat_physicaldata.Text = "-";
                stat_dedupesavings.Text = "-";
                stat_exsize.Text = "-";

                Graphics g = this.pictureBox2.CreateGraphics();
                Brush br = new SolidBrush(Color.AntiqueWhite);
                g.FillRectangle(br, 0, 0, 323, 42);
            }
            else
            {

                stat_vdsize.Text = DEFS.getDataInStringRep(vsize);
                stat_freespace.Text = DEFS.getDataInStringRep(freespace);
                stat_logicaldata.Text = DEFS.getDataInStringRep(ldata);
                stat_physicaldata.Text = DEFS.getDataInStringRep(vsize - freespace);
                stat_dedupesavings.Text = DEFS.getDataInStringRep(ldata - (vsize - freespace));
                stat_exsize.Text = DEFS.getDataInStringRep(ldata + freespace);

                long total = ldata + freespace;
                int l1 = (int)(freespace*323/ total);
                int l2 = (int)((vsize - freespace)*323/ total);
                int l3 = (int) (323 - l1 -l2);

                Graphics g = this.pictureBox2.CreateGraphics();
                Brush br = new SolidBrush(Color.WhiteSmoke);
                g.FillRectangle(br, 0, 0, l1, 42);

                Brush br2 = new SolidBrush(Color.DeepSkyBlue);
                g.FillRectangle(br2, l1, 0, l2, 42);

                Brush br3 = new SolidBrush(Color.LightBlue);
                g.FillRectangle(br3, l1 + l2, 2, l3, 38);

                Pen pred = new Pen(Color.Red, 2);
                g.DrawRectangle(pred, 3, 3, 319, 36);

                Pen pen = new Pen(Color.Black, 2);
                g.DrawRectangle(pen, 1, 1, l1 + l2 - 1, 40);
            }
        }

        public void Update_DGV()
        {
            if (REDDY.ptrRedFS == null || REDDY.ptrIFSDMux == null) return;

            int numrows = dataGridView1.RowCount;
            for (int i = 0; i < numrows; i++) 
            {
                string backingdrive = dataGridView1.Rows[i].Cells[1].Value.ToString();
                int bnumber = Int32.Parse((dataGridView1.Rows[i].Cells[0].Value.ToString()));
                if (REDDY.FSIDList[bnumber] == null) continue;

                dataGridView1.Rows[i].Cells[6].Value = DEFS.getDataInStringRep(REDDY.FSIDList[bnumber].get_logical_data());
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            DEFS.INIT_LOGGING();
            /*
            dataGridView1.Rows.Clear();
            dataGridView1.AllowUserToAddRows = false;

            DEFS.INIT_LOGGING();
            DEFS.ASSERT(REDDY.pDOKAN == null, "pDOKAN must be null during form loading");
            DEFS.ASSERT(REDDY.mountedidx == -1, "pFSID must be null during form loading");

            DEFS.DEBUG("TRACE", "Before creating RedFSDrive");

            REDDY.ptrRedFS = new RedFSDrive();
            Reload_FSinfo_Display();
            */
            bMountVD.Enabled = false;
            bUnmountVD.Enabled = false;
            bStartDedupe.Enabled = false;
            panel1.BackColor = Color.Snow;
            panel2.BackColor = Color.Snow;
            panel3.BackColor = Color.Snow;
            panel5.BackColor = Color.Snow;

            ProgramX2.StartPuttyListener();

            Thread oThread = new Thread(new ThreadStart(UpdateThread));
            oThread.Start();

            tools_load_init();

            createnewbackuptask.Enabled = false;
        }

        private void refcountUpdateToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Exit_Saving es = new Exit_Saving();
            es.Show();

            try
            {
                DokanNet.DokanUnmount('X'); //do this anyway XXX
            }
            catch (Exception ext)
            {
                DEFS.DEBUG("UI", "exception " + ext.Message);
            }

            DEFS.DEBUG("FORM", "Closing form, sync and save all data");

            if (REDDY.pDOKAN != null)
            {
                DokanNet.DokanUnmount('X');
                REDDY.pDOKAN.shut_down();
                REDDY.pDOKAN = null;
                REDDY.mountedidx = -1;
            }
            DEFS.DEBUG("FORM", "closing ptrRedFS");

            if (REDDY.ptrRedFS != null)
            {
                REDDY.ptrRedFS.shut_down();
                REDDY.ptrRedFS = null;
            }
            
            es.Close();
            DEFS.STOP_LOGGING();
            DEFS.DEBUG("FORM", "closing ptrRedFS DONE");
            System.Environment.Exit(0);
        }

        private void dataGridView1_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right && e.RowIndex >= 0)
            {
                dataGridView1.Rows[e.RowIndex].Selected = true;
                m_curr_row = e.RowIndex;
                contextMenuStrip2.Show(MousePosition);
            }
        }

        private void cloneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (REDDY.mountedidx != -1)
            { 
                MessageBox.Show("You cannot clone a drive right now!. You must unmount any " +
                    "mounted drives before you take a clone");
                return;
            }

            string backingdrive = dataGridView1.Rows[m_curr_row].Cells[1].Value.ToString();
            int bnumber = Int32.Parse((dataGridView1.Rows[m_curr_row].Cells[0].Value.ToString()));

            if (bnumber == 1)
            { 
                MessageBox.Show("NOTE: This will only create a clone of all your NTFS/FAT32 virtual disks. The " + 
                    "cloned volume that you create will show these virtual NTFS/FAT32 disks as regular files. However" + 
                    " you can still clone this to keep a point in time copy of your NTFS/FAT32 drives. You can also " + 
                   " restore these drives individually to the LUN drive and then mount them with the REDFS_Initiator");
            }
            else if (bnumber == 2)
            {
                MessageBox.Show("NOTE: This will only create a clone of your Backup Drive, The folder listing will show you" +
                    " all the backup tasks that are present including each backup job. If you need to recover any of your backups " +
                    " or keep a copy of backup of you backups, you can clone this. Deleting this cloned volume will not affect your " +
                    " existing backups");
            }

            string newdrive = backingdrive + "_c" + bnumber;

            Create_New_Drive f = new Create_New_Drive(backingdrive, newdrive);
            f.ShowDialog();

            if (f.set_value_true)
            {
                REDDY.ptrIFSDMux.unmount(bnumber);

                RedFS_FSID backingfsinfo = REDDY.ptrRedFS.redfs_load_fsid(bnumber, false);
                RedFS_FSID newfsinfo = REDDY.ptrRedFS.redfs_dup_fsid(backingfsinfo);

                newfsinfo.set_drive_name(f.newdrive, backingdrive);
                newfsinfo.update_comment(f.comments);

                dataGridView1.Rows.Add(newfsinfo.get_data_for_datagridview());
                dataGridView1.Rows[dataGridView1.RowCount - 1].HeaderCell.ContextMenuStrip = contextMenuStrip2;

                REDDY.ptrRedFS.redfs_commit_fsid(newfsinfo);
                DEFS.ASSERT(REDDY.FSIDList[newfsinfo.get_fsid()] == null, "wrong in duping");
   
                REDDY.FSIDList[newfsinfo.get_fsid()] = newfsinfo;
                REDDY.FSIDList[newfsinfo.get_fsid()].rootdir = new CDirectory(newfsinfo.get_fsid(), 0, -1, null, null, true);
            }
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {

        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (REDDY.mountedidx != -1)
            {
                MessageBox.Show("You cannot delete a drive right now!. You must unmount any " +
                    "mounted drives before you delele some drive");
                return;
            }

            string backingdrive = dataGridView1.Rows[m_curr_row].Cells[1].Value.ToString();
            int bnumber = Int32.Parse((dataGridView1.Rows[m_curr_row].Cells[0].Value.ToString()));

            if (bnumber == 0)
            {
                MessageBox.Show("You cannot delete the ZeroDrive.");
                return;
            }
            else if (bnumber == 1) 
            {
                MessageBox.Show("You cannot delete the LUN drive");
                return;
            }
            else if (bnumber == 2)
            {
                MessageBox.Show("You cannot delete the Backup Drive. If you want to delete your backups, then please " +
                    "use the third tab in the UI to select and remove backups individually");
                return;
            }
            REDDY.ptrRedFS.redfs_delete_fsid(REDDY.ptrRedFS.redfs_load_fsid(bnumber, false));
            DEFS.DEBUG("FSID", "Finished marking " + bnumber + " fsid for deletion");
            dataGridView1.Rows.Clear();

            Reload_FSinfo_Display();
        }

        private void propertiesToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void unmountToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /*
             * First check if this is mounted, else display message
             */
            string backingdrive = dataGridView1.Rows[m_curr_row].Cells[1].Value.ToString();
            int bnumber = Int32.Parse((dataGridView1.Rows[m_curr_row].Cells[0].Value.ToString()));

            if (REDDY.mountedidx != -1 && REDDY.FSIDList[REDDY.mountedidx] != null && REDDY.FSIDList[REDDY.mountedidx].get_fsid() == bnumber)
            {
                dataGridView1.Rows[m_curr_row].Cells[7].Value = " - ";

                DokanNet.DokanUnmount('X');
                REDDY.pDOKAN.shut_down();
                REDDY.pDOKAN = null;

                //Also take care of ifsdmux
                REDDY.ptrIFSDMux.unmount(REDDY.mountedidx);
                REDDY.mountedidx = -1;
            }
            else
            {
                MessageBox.Show("This drive is not mounted!");
            }
        }

        private void mountToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string backingdrive = dataGridView1.Rows[m_curr_row].Cells[1].Value.ToString();
            int bnumber = Int32.Parse((dataGridView1.Rows[m_curr_row].Cells[0].Value.ToString()));

            if (bnumber == 0)
            {
                MessageBox.Show("You cannot mount the ZeroDrive!. This drive is a template " +
                   "for you to create new empty drives for your data");
                return;
            }
            else if (bnumber == 1)
            {
                MessageBox.Show("You cannot mount the lun drive, these contain filedisks of your NTFS/FAT32 drives!");
                return;
            }
            else if (bnumber == 2)
            {
                MessageBox.Show("You cannot mount the Backup drive (for now!). If you want to recover some data from your " +
                    " incremental backups, then, create a clone of this drive, and look for your data there. You may delete " +
                    "that clone without affecting the contents of this drive");
                //return;
            }

            if (REDDY.mountedidx != -1)
            {
                MessageBox.Show("You cannot clone a drive right now!. You must unmount any " +
                    "mounted drives before you mount this one");
                return;
            }

            if (REDDY.mountedidx == bnumber) return;

            dataGridView1.Rows[m_curr_row].Cells[7].Value = "Mounted";

            REDDY.mountedidx = bnumber;
            REDDY.pDOKAN = new DokanEntryPt(bnumber, 'X');

            Thread oThread = new Thread(new ThreadStart(REDDY.pDOKAN.W));
            oThread.Start();
        }

        private void refreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /*
             * First check if this is mounted, else display message
             */
            if (m_curr_row < 0 || m_curr_row > dataGridView1.RowCount) return;

            string backingdrive = dataGridView1.Rows[m_curr_row].Cells[1].Value.ToString();
            int bnumber = Int32.Parse((dataGridView1.Rows[m_curr_row].Cells[0].Value.ToString()));

            if (REDDY.mountedidx != -1 && REDDY.FSIDList[REDDY.mountedidx] != null && REDDY.FSIDList[REDDY.mountedidx].get_fsid() == bnumber)
            {
                dataGridView1.Rows[m_curr_row].Cells[6].Value = DEFS.getDataInStringRep(REDDY.FSIDList[REDDY.mountedidx].get_logical_data());
            }
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            string folderPath = "";
            FolderBrowserDialog folderBrowserDialog1 = new FolderBrowserDialog();
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                folderPath = folderBrowserDialog1.SelectedPath;
                cpath.Text = folderPath;

                CONFIGERROR error = CONFIG.CheckIfValidFolder(folderPath);

                if (error == CONFIGERROR.OKAY)
                {
                    bMountVD.Enabled = true;
                    bCreateVD.Enabled = false;
                }
                else
                {
                    switch (error)
                    { 
                        case CONFIGERROR.HASH_CHECK_FAILED:
                            MessageBox.Show("Config hash mismatch detected. Did you move these files from elsewhere?");
                            break;
                        case CONFIGERROR.FILE_MISSING:
                            //MessageBox.Show("Some files are missing. Did you delete by mistake?");
                            break;
                    }
                    bMountVD.Enabled = false;
                    bCreateVD.Enabled = true;
                }
            }
        }

        /*
         * For the redfs initiator.
         */
        ParticipantComm mParticipant;// = new ParticipantComm();
           
        private void bMountVD_Click(object sender, EventArgs e)
        {
            dataGridView1.Rows.Clear();
            dataGridView1.AllowUserToAddRows = false;
            string path = cpath.Text;

            DEFS.ASSERT(REDDY.pDOKAN == null, "pDOKAN must be null during form loading");
            DEFS.ASSERT(REDDY.mountedidx == -1, "pFSID must be null during form loading");
            DEFS.ASSERT(CONFIG.CheckIfValidFolder(path) == CONFIGERROR.OKAY, "This path has no proper virtualdisk files");

            DEFS.DEBUG("TRACE", "Before creating RedFSDrive");

            CONFIG.LoadConfigForVirtualDisk(path);

            REDDY.ptrRedFS = new REDFSDrive();
            Reload_FSinfo_Display();

            REDDY.ptrIFSDMux = new IFSD_Mux();
            REDDY.ptrIFSDMux.init();

            bMountVD.Enabled = false;
            bUnmountVD.Enabled = true;
            bStartDedupe.Enabled = true;
            panel2.BackColor = Color.Thistle;
            panel1.BackColor = Color.Thistle;
            panel3.BackColor = Color.Thistle;
            panel5.BackColor = Color.Thistle;

            button1.Enabled = false;
            button3.Enabled = false;
            button4.Enabled = false;

            mStateLabel.Text = "DISK MOUNTED";

            tools_done_mountingdisk();

            DEFS.ASSERT(mParticipant == null, "mParticipant should be null here");
            mParticipant = new ParticipantComm();
            mParticipant.init();
            Reload_LUNinfo_Display(0);
            Reload_BackupTask_Display();

            createnewbackuptask.Enabled = true;
        }

        private void bCreateVD_Click(object sender, EventArgs e)
        {
            if (cpath.Text != "")
            {
                DialogResult dr = MessageBox.Show("This folder does not contain any Virtual disk. Would you like to create one here? " + cpath.Text,
                          "New Disk", MessageBoxButtons.YesNo);
                switch (dr)
                {
                    case DialogResult.Yes:
                        {
                            NewConfigurationUI ui = new NewConfigurationUI();
                            ui.selectedfolder = cpath.Text;
                            ui.ShowDialog();
                            
                            DialogResult dri = ui.DialogResult;
                            switch (dri)
                            {
                                case DialogResult.OK:
                                    {
                                        cpath.Text = ui.selectedfolder;
                                        break;
                                    }
                                case DialogResult.Cancel:
                                    return;
                            }
                             
                            break;
                        }
                    case DialogResult.No: return;
                }
            }
            else
            {
                NewConfigurationUI ui = new NewConfigurationUI();
                ui.selectedfolder = null;
                ui.ShowDialog();

                DialogResult dr = ui.DialogResult;
                switch (dr)
                {
                    case DialogResult.OK:
                        {
                            cpath.Text = ui.selectedfolder;
                            break;
                        }
                    case DialogResult.Cancel:
                        return;              
                }
            }

            bCreateVD.Enabled = false;
            bMountVD.Enabled = true;
            bUnmountVD.Enabled = false;
            bStartDedupe.Enabled = false;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            cpath.Text = "";
            bMountVD.Enabled = false;
            bCreateVD.Enabled = true;
        }

        private void bUnmountVD_Click(object sender, EventArgs e)
        {
            DEFS.ASSERT(mParticipant != null, "mParticipant should not be null here");
            mParticipant = null;
            /*XXX do this correctly *?
             */ 
            if (REDDY.pDOKAN != null)
            {
                DokanNet.DokanUnmount('X');
                REDDY.pDOKAN.shut_down();
                REDDY.pDOKAN = null;
                REDDY.mountedidx = -1;
            }

            if (REDDY.ptrIFSDMux != null)
            {
                REDDY.ptrIFSDMux.shut_down();
                REDDY.ptrIFSDMux = null;

                for (int i = 0; i < 1024; i++)
                {
                    if (REDDY.FSIDList[i] != null) 
                    { 
                        DEFS.ASSERT(REDDY.FSIDList[i].get_dirty_flag() == false, "Should not be dirty");
                        REDDY.FSIDList[i] = null;
                    }
                }
            }

            if (REDDY.ptrRedFS != null)
            {
                REDDY.ptrRedFS.shut_down();
                REDDY.ptrRedFS = null;
            }

            CONFIG.ClearIncoreConfigInformation();

            bMountVD.Enabled = true;
            bCreateVD.Enabled = false;
            bStartDedupe.Enabled = false;
            bUnmountVD.Enabled = false;
            panel2.BackColor = Color.Snow;
            panel1.BackColor = Color.Snow;
            panel3.BackColor = Color.Snow;
            panel5.BackColor = Color.Snow;

            dataGridView1.Rows.Clear();
            dataGridView1.AllowUserToAddRows = false;
            dataGridView2.Rows.Clear();
            dataGridView2.AllowUserToAddRows = false;

            button1.Enabled = true;
            button3.Enabled = true;
            button4.Enabled = true;

            mStateLabel.Text = "-";

            tools_done_umountdisk();

            createnewbackuptask.Enabled = false;
        }

        private void bStartDedupe_Click(object sender, EventArgs e)
        {
            if (REDDY.mountedidx != -1)
            {
                MessageBox.Show("You cannot run dedupe right now!. You must unmount any " +
                    "mounted drives before you start this operation.");
                return;
            }

            Dedupe_UI di = new Dedupe_UI();
            di.ShowDialog();
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {

        }

        /*
         * Below functions are for the tools section tab.
         */

        private void tools_done_umountdisk()
        {
            toolsb_change1_Click(null, null);
            toolsb_change2_Click(null, null);
            toolsb_display1.Enabled = false;
            toolsb_display2.Enabled = false;
        }
        private void tools_done_mountingdisk()
        {
            toolsb_change1_Click(null, null);
            toolsb_change2_Click(null, null);
            toolsb_display1.Enabled = true;
            toolsb_display2.Enabled = true;
        }

        private void tools_load_init()
        {
            toolsb_change1.Enabled = false;
            toolsb_change2.Enabled = false;
            toolsb_display1.Enabled = false;
            toolsb_display2.Enabled = false;
        }

        private void setflag_cross_drive_clone(bool flag)
        {
            if (flag)
            {
                tools_clone_lr.Enabled = true;
                tools_clone_rl.Enabled = true;
            }
            else
            {
                tools_clone_lr.Enabled = false;
                tools_clone_rl.Enabled = false;
            }
        }

        private void tools_listView1_MouseClick(object sender, MouseEventArgs e)
        {

        }

        private void toolsb_display1_Click(object sender, EventArgs e)
        {
            int id = 0, idother = 0;
            try {
                id = Int32.Parse(tools_fsid1_text.Text);
                if (toolsb_change2.Enabled == true)
                {
                    idother = Int32.Parse(tools_fsid2_text.Text);
                }
            } 
            catch (Exception ex) 
            {
                DEFS.DEBUG("EXP", "Exception in toolsb_display1_Click " + ex.Message);
            }

            if (id == 0 || REDDY.FSIDList[id] == null || (id == idother)) {
                MessageBox.Show("This fsid is not valid");
                return;
            }

            if (toolsb_change2.Enabled == true)
            {
                setflag_cross_drive_clone(true);
            }

            tools_fsid1_text.Enabled = false;
            toolsb_display1.Enabled = false;
            toolsb_change1.Enabled = true;
            
            tools_move1.Enabled = true;
            tools_delete1.Enabled = true;
            tools_clone1.Enabled = true;
            tools_vault1.Enabled = true;
            tools_rename1.Enabled = true;
            tools_newfolder1.Enabled = true;

            tools_path1.Text = "\\";
            do_display_work(1);
        }

        private void toolsb_display2_Click(object sender, EventArgs e)
        {
            int id = 0, idother = 0;
            try
            {
                id = Int32.Parse(tools_fsid2_text.Text);
                if (toolsb_change1.Enabled == true)
                {
                    idother = Int32.Parse(tools_fsid1_text.Text);
                }
            }
            catch (Exception ex) 
            {
                DEFS.DEBUG("EXP", "Exception in toolsb_display2_Click " + ex.Message);
            }

            if (id == 0 || REDDY.FSIDList[id] == null || (id == idother))
            {
                MessageBox.Show("This fsid is not valid");
                return;
            }

            if (toolsb_change1.Enabled == true)
            {
                setflag_cross_drive_clone(true);
            }

            tools_fsid2_text.Enabled = false;
            toolsb_display2.Enabled = false;
            toolsb_change2.Enabled = true;
            
            tools_move2.Enabled = true;
            tools_delete2.Enabled = true;
            tools_clone2.Enabled = true;
            tools_vault2.Enabled = true;
            tools_rename2.Enabled = true;
            tools_newfolder2.Enabled = true;

            tools_path2.Text = "\\";
            do_display_work(2);
        }

        private void toolsb_change1_Click(object sender, EventArgs e)
        {
            tools_fsid1_text.Enabled = true;
            toolsb_display1.Enabled = true;
            toolsb_change1.Enabled = false;

            tools_path1.Text = "";
            tools_listView1.Clear();

            tools_move1.Enabled = false;
            tools_delete1.Enabled = false;
            tools_clone1.Enabled = false;
            tools_vault1.Enabled = false;
            tools_rename1.Enabled = false;
            tools_newfolder1.Enabled = false;

            setflag_cross_drive_clone(false);
        }

        private void toolsb_change2_Click(object sender, EventArgs e)
        {
            tools_fsid2_text.Enabled = true;
            toolsb_display2.Enabled = true;
            toolsb_change2.Enabled = false;

            tools_path2.Text = "";
            tools_listView2.Clear();

            tools_move2.Enabled = false;
            tools_delete2.Enabled = false;
            tools_clone2.Enabled = false;
            tools_vault2.Enabled = false;
            tools_rename2.Enabled = false;
            tools_newfolder2.Enabled = false;

            setflag_cross_drive_clone(false);
        }

        private void do_display_work(int windowid)
        { 
            TextBox pathbox = (windowid == 1)? tools_path1 : tools_path2;
            TextBox fsidbox = (windowid == 1)? tools_fsid1_text : tools_fsid2_text;
            ListView listview = (windowid == 1) ? tools_listView1 : tools_listView2;

            string currpath = pathbox.Text;
            int fsid  = Int32.Parse(fsidbox.Text);

            Inode_Info[] entries = REDDY.ptrIFSDMux.FindFilesInternalAPI(fsid, currpath);

            if (entries != null)
            {
                listview.Clear();
                for (int i = 0; i < entries.Length; i++) 
                {
                    if (entries[i].isfile)
                    {
                        listview.Items.Add("    " + entries[i].name);
                    }
                    else
                    {
                        listview.Items.Add("[d] " + entries[i].name);
                    }
                }
            }
        }

        private void handle_listview_doubleclick(int windowid)
        {
            TextBox pathbox = (windowid == 1) ? tools_path1 : tools_path2;
            TextBox fsidbox = (windowid == 1) ? tools_fsid1_text : tools_fsid2_text;
            ListView listview = (windowid == 1) ? tools_listView1 : tools_listView2;


            if (listview.SelectedItems.Count > 0)
            {
                string unparsed = listview.SelectedItems[0].Text;
                if (string.Compare("[d] ", 0, unparsed, 0, 4) == 0)
                {
                    string dirname = unparsed.Substring(4);
                    string currpath = pathbox.Text;
                    string newpath = currpath + "\\" + dirname;

                    pathbox.Text = newpath;
                    do_display_work(windowid);
                }
            }       
        }

        private void tools_listView1_DoubleClick(object sender, EventArgs e)
        {
            handle_listview_doubleclick(1);
        }

        private void tools_listView2_DoubleClick(object sender, EventArgs e)
        {
            handle_listview_doubleclick(2);
        }

        private void handle_upbutton_work(int windowid)
        {
            TextBox pathbox = (windowid == 1) ? tools_path1 : tools_path2;

            String path = pathbox.Text;
            char[] sep = { '\\' };
            String[] list = path.Split(sep, StringSplitOptions.RemoveEmptyEntries);
            if (list.Length == 0) return;
            else if (list.Length == 1)
            {
                pathbox.Text = "\\";
                do_display_work(windowid);
                return;
            }
            else
            {
                String ppath = "\\";
                for (int i = 0; i < list.Length - 1; i++)
                {
                    ppath += list[i] + "\\";
                }
                pathbox.Text = ppath;
                do_display_work(windowid);
                return;
            }
        }

        private void tools_up1_Click(object sender, EventArgs e)
        {
            handle_upbutton_work(1);
        }

        private void tools_up2_Click(object sender, EventArgs e)
        {
            handle_upbutton_work(2);
        }

        private void do_delete_work(int windowid)
        {
            TextBox pathbox = (windowid == 1) ? tools_path1 : tools_path2;
            TextBox fsidbox = (windowid == 1) ? tools_fsid1_text : tools_fsid2_text;
            ListView listview = (windowid == 1) ? tools_listView1 : tools_listView2;

            int fsid = Int32.Parse(fsidbox.Text);

            if (listview.SelectedItems.Count > 0)
            {
                string unparsed = listview.SelectedItems[0].Text;
                if (string.Compare("[d] ", 0, unparsed, 0, 4) == 0)
                {
                    string dirname = unparsed.Substring(4);
                    string currpath = pathbox.Text;
                    string newpath = currpath + "\\" + dirname;
                    REDDY.ptrIFSDMux.DeleteDirectory(fsid, newpath, null);
                }
                else
                { 
                    string fstr = unparsed.Substring(4);
                    string currpath = pathbox.Text;
                    string filename = currpath + "\\" + fstr;
                    REDDY.ptrIFSDMux.DeleteFile(fsid, filename, null);
                }
            }
            do_display_work(windowid);
        }

        private void tools_delete1_Click(object sender, EventArgs e)
        {
            do_delete_work(1);
        }

        private void tools_delete2_Click(object sender, EventArgs e)
        {
            do_delete_work(2);
        }

        private void do_renaming_work(int windowid)
        {
            TextBox pathbox = (windowid == 1) ? tools_path1 : tools_path2;
            TextBox fsidbox = (windowid == 1) ? tools_fsid1_text : tools_fsid2_text;
            ListView listview = (windowid == 1) ? tools_listView1 : tools_listView2;

            int fsid = Int32.Parse(fsidbox.Text);

            if (listview.SelectedItems.Count > 0)
            {
                string unparsed = listview.SelectedItems[0].Text;
                string inodetype = (string.Compare("[d] ", 0, unparsed, 0, 4) == 0) ? "DIRECTO" : "FILE";

                string iname = unparsed.Substring(4);
                string currpath = pathbox.Text;
                string newpath = currpath + "\\" + iname;

                UIToolsCmd ui = new UIToolsCmd("RENAME", inodetype, iname, iname + "_new", fsid, fsid);
                ui.ShowDialog();
                if (ui.get_result() != null)
                {
                    //check that its not a subset or we end up in recursion problem.
                    //REDDY.ptrIFSDMux.CloneDirectory(fsid, newpath, fsid, ui.get_result());
                    REDDY.ptrIFSDMux.RenameInode2a(fsid, newpath, ui.get_result());
                    do_display_work(windowid);
                }
            }        
        
        }
        private void tools_rename1_Click(object sender, EventArgs e)
        {
            do_renaming_work(1);
        }

        private void tools_rename2_Click(object sender, EventArgs e)
        {
            do_renaming_work(2);
        }

        private void do_local_clone_work(int windowid)
        {
            TextBox pathbox = (windowid == 1) ? tools_path1 : tools_path2;
            TextBox fsidbox = (windowid == 1) ? tools_fsid1_text : tools_fsid2_text;
            ListView listview = (windowid == 1) ? tools_listView1 : tools_listView2;

            int fsid = Int32.Parse(fsidbox.Text);

            if (listview.SelectedItems.Count > 0)
            {
                string unparsed = listview.SelectedItems[0].Text;
                if (string.Compare("[d] ", 0, unparsed, 0, 4) == 0)
                {
                    string dirname = unparsed.Substring(4);
                    string currpath = pathbox.Text;
                    string newpath = currpath + "\\" + dirname;
                    //MessageBox.Show("Only file clone is implimented as of now");
                    UIToolsCmd ui = new UIToolsCmd("CLONE", "DIRECTORY", newpath, newpath + ".clone", fsid, fsid);
                    ui.ShowDialog();
                    if (ui.get_result() != null)
                    {
                        //check that its not a subset or we end up in recursion problem.
                        REDDY.ptrIFSDMux.CloneDirectoryTlock(fsid, newpath, fsid, ui.get_result());
                        do_display_work(windowid);
                    }
                }
                else
                {
                    string fstr = unparsed.Substring(4);
                    string currpath = pathbox.Text;
                    string filename = currpath + "\\" + fstr;
                    UIToolsCmd ui = new UIToolsCmd("CLONE", "REGULAR FILE", filename, filename + ".clone", fsid, fsid);
                    ui.ShowDialog();
                    if (ui.get_result() != null)
                    {
                        REDDY.ptrIFSDMux.CloneFileTLock(fsid, filename, fsid, ui.get_result());
                        do_display_work(windowid);
                    }
                }
            }
        }

        private void tools_clone1_Click(object sender, EventArgs e)
        {
            do_local_clone_work(1);
        }

        private void tools_clone2_Click(object sender, EventArgs e)
        {
            do_local_clone_work(2);
        }

        private void do_local_move_work(int windowid)
        {
            TextBox pathbox = (windowid == 1) ? tools_path1 : tools_path2;
            TextBox fsidbox = (windowid == 1) ? tools_fsid1_text : tools_fsid2_text;
            ListView listview = (windowid == 1) ? tools_listView1 : tools_listView2;

            int fsid = Int32.Parse(fsidbox.Text);

            if (listview.SelectedItems.Count > 0)
            {
                string unparsed = listview.SelectedItems[0].Text;
                if (string.Compare("[d] ", 0, unparsed, 0, 4) == 0)
                {
                    string dirname = unparsed.Substring(4);
                    string currpath = pathbox.Text;
                    UIToolsCmd ui = new UIToolsCmd("MOVE", "DIRECTORY", currpath, currpath, fsid, fsid);
                    ui.ShowDialog();
                }
                else
                {
                    string fstr = unparsed.Substring(4);
                    string currpath = pathbox.Text;
                    UIToolsCmd ui = new UIToolsCmd("MOVE", "REGULAR FILE", currpath, currpath, fsid, fsid);
                    ui.ShowDialog();
                }
            }        
        }

        private void tools_move1_Click(object sender, EventArgs e)
        {
            do_local_move_work(1);
        }

        private void tools_move2_Click(object sender, EventArgs e)
        {
            do_local_move_work(2);
        }

        private void tools_newfolder_work(int windowid)
        {
            TextBox pathbox = (windowid == 1) ? tools_path1 : tools_path2;
            TextBox fsidbox = (windowid == 1) ? tools_fsid1_text : tools_fsid2_text;

            int fsid = -1;
            try
            {
                fsid = Int32.Parse(fsidbox.Text);
            }
            catch (Exception e) 
            {
                DEFS.DEBUG("EXP", "Exception in tools_newfolder_work " + e.Message);
                return; 
            }

            UIToolsNewDir ui = new UIToolsNewDir();
            ui.ShowDialog();

            if (ui.newfoldername != null)
            {
                string currpath = pathbox.Text;
                string newpath = currpath + "\\" + ui.newfoldername;
                REDDY.ptrIFSDMux.CreateDirectory(fsid, newpath, null);
            }
            do_display_work(windowid);
        }

        private void tools_newfolder1_Click(object sender, EventArgs e)
        {
            tools_newfolder_work(1);
        }

        private void tools_newfolder2_Click(object sender, EventArgs e)
        {
            tools_newfolder_work(2);
        }

        private void tools_vault1_Click(object sender, EventArgs e)
        {
            MessageBox.Show("This will be enabled only after Backup portion is coded");
        }

        private void tools_vault2_Click(object sender, EventArgs e)
        {
            MessageBox.Show("This will be enabled only after Backup portion is coded");
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string backingdrive = dataGridView1.Rows[m_curr_row].Cells[1].Value.ToString();
            int bnumber = Int32.Parse((dataGridView1.Rows[m_curr_row].Cells[0].Value.ToString()));

            if (REDDY.mountedidx != -1 && REDDY.FSIDList[REDDY.mountedidx] != null && 
                    REDDY.FSIDList[REDDY.mountedidx].get_fsid() == bnumber)
            {
                System.Diagnostics.Process.Start("explorer.exe", @"X:\");
            }
        }

        private void do_crossdrive_clone_work(bool lefttoright)
        {
            int fsid1, fsid2;

            if (lefttoright)
            {
                fsid1 = Int32.Parse(tools_fsid1_text.Text);
                fsid2 = Int32.Parse(tools_fsid2_text.Text);

                if (tools_listView1.SelectedItems.Count > 0)
                {
                    string unparsed = tools_listView1.SelectedItems[0].Text;
                    if (string.Compare("[d] ", 0, unparsed, 0, 4) == 0)
                    {
                        string dirname = unparsed.Substring(4);
                        string currpath = tools_path1.Text;
                        string newpath = currpath + "\\" + dirname;

                        string destpath = tools_path2.Text;
                        UIToolsCmd ui = new UIToolsCmd("CLONE-CROSS DRIVE", "DIRECTORY", newpath, destpath + "\\" + dirname, fsid1, fsid2);
                        ui.ShowDialog();
                        if (ui.get_result() != null)
                        {
                            //check that its not a subset or we end up in recursion problem.
                            REDDY.ptrIFSDMux.CloneDirectoryTlock(fsid1, newpath, fsid2, ui.get_result());
                            do_display_work(1);
                            do_display_work(2);
                        }
                    }
                    else
                    {
                        string fstr = unparsed.Substring(4);
                        string currpath = tools_path1.Text;
                        string filename = currpath + "\\" + fstr;

                        string destpath = tools_path2.Text;
                        UIToolsCmd ui = new UIToolsCmd("CLONE-CROSS DRIVE", "REGULAR FILE", filename, destpath + "\\" + fstr, fsid1, fsid2);
                        ui.ShowDialog();
                        if (ui.get_result() != null)
                        {
                            REDDY.ptrIFSDMux.CloneFileTLock(fsid1, filename, fsid2, ui.get_result());
                            do_display_work(1);
                            do_display_work(2);
                        }
                    }
                }
            }
            else
            {
                fsid1 = Int32.Parse(tools_fsid2_text.Text);
                fsid2 = Int32.Parse(tools_fsid1_text.Text);

                if (tools_listView2.SelectedItems.Count > 0)
                {
                    string unparsed = tools_listView2.SelectedItems[0].Text;
                    if (string.Compare("[d] ", 0, unparsed, 0, 4) == 0)
                    {
                        string dirname = unparsed.Substring(4);
                        string currpath = tools_path2.Text;
                        string newpath = currpath + "\\" + dirname;

                        string destpath = tools_path1.Text;
                        UIToolsCmd ui = new UIToolsCmd("CLONE-CROSS DRIVE", "DIRECTORY", newpath, destpath + "\\" + dirname, fsid1, fsid2);
                        ui.ShowDialog();
                        if (ui.get_result() != null)
                        {
                            //check that its not a subset or we end up in recursion problem.
                            REDDY.ptrIFSDMux.CloneDirectoryTlock(fsid1, newpath, fsid2, ui.get_result());
                            do_display_work(1);
                            do_display_work(2);
                        }
                    }
                    else
                    {
                        string fstr = unparsed.Substring(4);
                        string currpath = tools_path2.Text;
                        string filename = currpath + "\\" + fstr;

                        string destpath = tools_path1.Text;
                        UIToolsCmd ui = new UIToolsCmd("CLONE-CROSS DRIVE", "REGULAR FILE", filename, destpath + "\\" + fstr, fsid1, fsid2);
                        ui.ShowDialog();
                        if (ui.get_result() != null)
                        {
                            REDDY.ptrIFSDMux.CloneFileTLock(fsid1, filename, fsid2, ui.get_result());
                            do_display_work(1);
                            do_display_work(2);
                        }
                    }
                }
            }
        

        }

        private void tools_clone_lr_Click(object sender, EventArgs e)
        {
            do_crossdrive_clone_work(true);
        }

        private void tools_clone_rl_Click(object sender, EventArgs e)
        {
            do_crossdrive_clone_work(false);
        }

        private void dataGridView2_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void Reload_LUNinfo_Display(int id)
        {
            if (InvokeRequired)
            {
                this.BeginInvoke(new Action<int>(Reload_LUNinfo_Display), new object[] { id });
                return;
            }

            if (mParticipant == null) return;

            DEFS.DEBUG("FSID", "Loading LUN information for dataGridView");
            dataGridView2.Rows.Clear();

            lock (mParticipant.m_lunlist)
            {
                for (int i = 0; i < mParticipant.m_lunlist.Count; i++)
                {
                    Lun_Item li = mParticipant.m_lunlist.ElementAt(i);
                    string[] strx = li.get_data_for_datagridview();
                    dataGridView2.Rows.Add(strx);
                }
            }
            dataGridView2.Invalidate();
        }

        private void dataGridView2_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right && e.RowIndex >= 0) 
            {
                dataGridView2.Rows[e.RowIndex].Selected = true;
                m_curr_row = e.RowIndex;
                contextMenuStripLUN.Show(MousePosition);
            }
        }

        private void newlun_Click(object sender, EventArgs e)
        {
            if (mParticipant == null) 
            {
                MessageBox.Show("You must first mount a redfs disk. Please see the first TAB of the UI");
                return;
            }

            Create_New_NTFSorFAT32_Drive f = new Create_New_NTFSorFAT32_Drive();
            f.ShowDialog();
            int did = f.drive_id;
            bool alreadymounted = false; //no use for us
            long drivesize = 0; //no use again.

            if (mParticipant.check_if_driveid_exists(f.drive_id, ref alreadymounted, ref drivesize)) {
                MessageBox.Show("A drive with the given ID already exists. Please try again!");
                return;
            }

            if (f.drive_size == 0 || f.drive_size > 64) {
                MessageBox.Show("Given drive size is not valid. The size must be a minimun of 1GB to a max of 64GB");
                return;
            }

            string fname = did.ToString();
            REDDY.ptrIFSDMux.CreateFile(1, fname, FileAccess.ReadWrite, FileShare.ReadWrite, FileMode.OpenOrCreate, FileOptions.RandomAccess, null);
            REDDY.ptrIFSDMux.SetEndOfFile(1, fname, (long)1024 * 1024 * 1024 * f.drive_size, null);

            //now reload the lun info.
            mParticipant.load_lun_list(false);

            //now display on the UI.
            Reload_LUNinfo_Display(0);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            New_Backup_Task nbj = new New_Backup_Task();
            nbj.ShowDialog();

            if (nbj.result_okay)
            {
                dataGridView3.Rows.Add(new string[] {nbj.task_name, "today","-","0 B", "-" });
                //write to disk.
                REDDY.ptrIFSDMux.CreateDirectory(2, "\\" + nbj.task_name, null);
                BackupTask bt = new BackupTask();
                bt.BackupPaths = nbj.mlist;
                bt.TaskName = nbj.task_name;
                bt.JobCount = 0;
              
                Backup_Utility.save_task(nbj.task_name, bt);
            }
        }

        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {

        }

        private void toolStripMenuItem7_Click(object sender, EventArgs e)
        {
            string name = (string)dataGridView3.Rows[m_curr_row3].Cells[0].Value.ToString();
            BackupTask bt = Backup_Utility.load_task(name);
            if (bt != null)
            {
                BackupJobDetailsUI ui = new BackupJobDetailsUI(name, bt.BackupPaths, bt.JobHistory);
                ui.ShowDialog();
            }
        }

        private void dataGridView3_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right && e.RowIndex >= 0)
            {
                dataGridView3.Rows[e.RowIndex].Selected = true;
                m_curr_row3 = e.RowIndex;
                contextMenuStripBackup.Show(MousePosition);
            }
        }

        private void contextMenuStripBackup_Opening(object sender, CancelEventArgs e)
        {

        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            /*
             * create an entry in the task.xml file also. this can be corelated to the folder structure later.
             */
            string name = (string)dataGridView3.Rows[m_curr_row3].Cells[0].Value.ToString();
            BackupTask bt = Backup_Utility.load_task(name);
            if (bt != null)
            {
                New_Job_Name njn = new New_Job_Name(bt.TaskName, bt.JobCount);
                njn.ShowDialog();

                if (njn.result_okay == false)
                {
                    return;
                }

                job_item ji = new job_item();
                ji.JobID = bt.JobCount++;
                ji.JobName = ji.JobID.ToString() +  "_" + njn.job_name;

                string thisjobname = ji.JobName;
                Backup_Worker bwui = new Backup_Worker(bt, thisjobname);
                bwui.ShowDialog();

                ji.NewCopiedData = bwui.newdatacopied;
                bt.JobHistory.Add(ji);
                bt.JobCount++;

                Backup_Utility.save_task(name, bt);
            }
        }

        private bool IsProcessOpen(string name)
        {
            foreach (Process clsProcess in Process.GetProcesses())
            {
                //Console.WriteLine(clsProcess.ProcessName);
                if (clsProcess.ProcessName.Contains(name))
                {
                    return true;
                }
            }
            return false;
        }

        private void mountToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            /*
             * Check if its already there. if not there, then start.
             */
            if (IsProcessOpen("redfs_initiator") == false)
            {
                Process.Start("redfs_initiator.exe");
            }
        }

        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {

        }
    }
}
