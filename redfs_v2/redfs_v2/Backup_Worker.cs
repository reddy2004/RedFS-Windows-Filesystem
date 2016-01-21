using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Security.Cryptography;

namespace redfs_v2
{
    public partial class Backup_Worker : Form
    {
        private bool stop_thread = false;
        private bool thread_stopped = false;
        private BackupTask m_bt;
        private string newjobname = "";

        public long newdatacopied = 0;

        public Backup_Worker(BackupTask bt, string njb)
        {
            InitializeComponent();
            m_bt = bt;
            newjobname = njb;
        }

        private void update_ui(int whatto, int ctr, string msg)
        {
            if (InvokeRequired)
            {
                this.BeginInvoke(new Action<int, int, string>(update_ui), new object[] { whatto, ctr, msg });
                return;
            }

            switch (whatto)
            { 
                case 0:
                    this.Close();
                    break;
                case 1:
                    label_b1.Text = msg;
                    break;
                case 2:
                    label_b2.Text = msg;
                    break;
                case 3:
                    progressBar1.Value = ctr;
                    break;
                case 4:
                    progressBar2.Value = ctr;
                    break;
                case 5:
                    label_b3.Text = msg;
                    break;
            }
        }

        private long scan_folder(string path, ref int count)
        {
            int counter = 0;
            long totaldata = 0;

            try
            {
                string[] list = Directory.GetDirectories(path);
                for (int i = 0; i < list.Length; i++)
                {
                    int c = 0;
                    DirectoryInfo di = new DirectoryInfo(list[i]);
                    //Console.WriteLine(di.Name);
                    totaldata += scan_folder(list[i], ref c);
                    counter += c;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            try
            {
                string[] flist = Directory.GetFiles(path);
                for (int i = 0; i < flist.Length; i++)
                {
                    FileInfo f2 = new FileInfo(flist[i]);
                    totaldata += f2.Length;
                    counter++;
                }
                count = counter;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return totaldata;
        }

        private long fetch_total_data_to_scan()
        {
            long totaldata = 0;
            int count = 0;
            for (int i = 0; i < m_bt.BackupPaths.Count; i++)
            {
                backup_pair bp = m_bt.BackupPaths.ElementAt(i);
                if (bp.IsFile)
                {
                    FileInfo fi = new FileInfo(bp.SourcePath);
                    totaldata += fi.Length;
                }
                else
                {
                    totaldata += scan_folder(bp.SourcePath, ref count);
                }
            }
            return totaldata;
        }

        private void worker_thread()
        {
            /*
            * First check the folder for the last job exists/
            */
            update_ui(1, 0, "Working..");
            string oldjobpath = null;
            for (int id = (m_bt.JobCount - 1); id >= 0; id--)
            {
                string jobname = m_bt.fetch_job_name(id);
                if (jobname == null) continue;
                else 
                {
                    oldjobpath = "\\" + m_bt.TaskName + "\\" + jobname;
                    break;
                }
            }

            string newjobpath = "\\" + m_bt.TaskName + "\\" + newjobname;

            DEFS.DEBUG("BACKUP", "Found current job path = " + oldjobpath);

            //MessageBox.Show("Backup folders " + oldjobpath + "->" + newjobpath);
            if (oldjobpath != null)
            {
                if (REDDY.ptrIFSDMux.CloneDirectoryTlock(2, oldjobpath, 2, newjobpath) != true)
                {
                    MessageBox.Show("failed to create new job dir " + oldjobpath + "->" + newjobpath);
                    thread_stopped = true;
                    update_ui(0, 0, null);
                    return;
                }
            }
            else
            {
                if (REDDY.ptrIFSDMux.CreateDirectory(2, newjobpath, null) != 0) 
                {
                    MessageBox.Show("failed to create new job dir");
                    thread_stopped = true;
                    update_ui(0, 0, null);
                    return;
                }
            }
            update_ui(1, 0 , "[DONE]");

            update_ui(2, 0, "Working..");

            long totaldatasetsize = fetch_total_data_to_scan();

            DEFS.DEBUG("BACKUP", "Inititing target directory, " + newjobpath);

            /*
             * Now create a new checksum file, and start writing new checksums there.
             */
            string oldchecksumfilepath = "\\" + m_bt.TaskName + "\\" + newjobname + "\\checksumfile";
            string newchecksumfilepath = "\\" + m_bt.TaskName + "\\" + newjobname + "\\checksumfile.new";
            REDDY.ptrIFSDMux.CreateFile(2, newchecksumfilepath, FileAccess.ReadWrite, FileShare.ReadWrite, FileMode.CreateNew, FileOptions.None, null);

            bool isfirstjob = (oldjobpath == null);

            if (stop_thread == true) 
            {
                thread_stopped = true;
                update_ui(0, 0, null);
                return;
            }

            int curroffset = 0;

            update_ui(2, 0, "[DONE] " + DEFS.getDataInStringRep(totaldatasetsize));


            for (int i = 0; i < m_bt.BackupPaths.Count; i++)
            {
                backup_pair bp = m_bt.BackupPaths.ElementAt(i);
                if (bp.IsFile)
                {
                    string destfile = "\\" + m_bt.TaskName + "\\" + newjobname + "\\" + bp.DestinationPath;
                    bool ret = backup_file(isfirstjob, oldchecksumfilepath, newchecksumfilepath, ref curroffset, bp.SourcePath, destfile);
                    if (ret == false) {
                        DEFS.DEBUG("BACKUP", "Backup of file " + bp.SourcePath + " failed!");
                        break;
                    }
                }
                else
                {
                    string destfolder = "\\" + m_bt.TaskName + "\\" + newjobname  + "\\" + bp.DestinationPath;
                    backup_folder(isfirstjob, oldchecksumfilepath, newchecksumfilepath, ref curroffset, bp.SourcePath, destfolder);
                }
            }
            REDDY.ptrIFSDMux.DeleteFile(2, oldchecksumfilepath, null);
            REDDY.ptrIFSDMux.RenameInode2a(2, newchecksumfilepath, "checksumfile");
            update_ui(5, 0, "[OKAY]");
            thread_stopped = true;
            update_ui(0, 0, null);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            stop_thread = true;
            int ctr = 5;
            while (thread_stopped == false && ctr-- > 0) Thread.Sleep(500);
            this.Close();
        }

        private bool backup_file(bool firstjob, string oldchecksumfile, string newchecksumfile, ref int curroffset, string sourcefile, string destfile)
        {
            DEFS.DEBUG("BACKUP","Entering backup_file ( " + firstjob + "," + oldchecksumfile + "," + 
                    newchecksumfile + "," + curroffset + "," + sourcefile + "," + destfile);

            MD5 md5 = System.Security.Cryptography.MD5.Create();
            fingerprintBACKUP fptemp1 = new fingerprintBACKUP();
            fingerprintBACKUP fptemp2 = new fingerprintBACKUP();

            if (firstjob)
            {
                FileInfo srcfi = new FileInfo(sourcefile);
                if (srcfi.Exists == false)
                {
                    REDDY.ptrIFSDMux.DeleteFile(2, destfile, null);
                    return false;
                }
                else
                {
                    if (REDDY.ptrIFSDMux.CreateFile(2, destfile, FileAccess.ReadWrite, FileShare.ReadWrite, FileMode.Create, FileOptions.None, null) != 0)
                    {
                        MessageBox.Show("failed to create file 1");
                        return false;                   
                    }

                    if (REDDY.ptrIFSDMux.SetEndOfFile(2, destfile, srcfi.Length, null) != 0)
                    {
                        MessageBox.Show("failed to seteof 1");
                        return false;                   
                    }

                    Inode_Info di = REDDY.ptrIFSDMux.GetFileInfoInternalAPI(2, destfile);
                    REDDY.ptrIFSDMux.SetInternalFlag(2, destfile, 0, curroffset);

                    if (di == null)
                    {
                        MessageBox.Show("failed to get a valid di 1");
                        return false;
                    }
                    int ino = di.ino;

                    byte[] buffer = new byte[4096];
                    byte[] tmpbuf = new byte[((Item)fptemp1).get_size()];
                    uint wrote = 0;

                    int bcount = OPS.NUML0(srcfi.Length);

                    FileStream fs = new FileStream(sourcefile, FileMode.Open);

                    long outfileoffset = 0;
                    byte[] lastchunkbuf = null;

                    for (int i = 0; i < bcount; i++) 
                    {
                        int size = fs.Read(buffer, 0, 4096);
                        if (size < 4096)
                        {
                            lastchunkbuf = new byte[size];
                            for (int kx = size; kx < 4096; kx++)
                            {
                                buffer[kx] = 0;
                            }
                            for (int kx2 = 0; kx2 < size; kx2++) {
                                lastchunkbuf[kx2] = buffer[kx2];
                            }
                        }
                        byte[] hash = md5.ComputeHash(buffer, 0, 4096);

                        fptemp1.inode = ino;
                        fptemp1.fbn = i;
                        for (int k = 0; k < 16; k++) {
                            fptemp1.fp[k] = hash[k];
                        }
                        ((Item)fptemp1).get_bytes(tmpbuf, 0);

                        if (REDDY.ptrIFSDMux.WriteFile(2, newchecksumfile, tmpbuf, ref wrote, curroffset, null) != 0)
                        {
                            MessageBox.Show("write failed, wrote = " + wrote);
                            return false;
                        }
                        if (size > 0)
                        {
                            if (size == 4096)
                            {
                                if (REDDY.ptrIFSDMux.WriteFile(2, destfile, buffer, ref wrote, outfileoffset, null) != 0)
                                {
                                    MessageBox.Show("write failed ee, wrote = " + wrote);
                                    return false;
                                }
                            }
                            else
                            {
                                if (REDDY.ptrIFSDMux.WriteFile(2, destfile, lastchunkbuf, ref wrote, outfileoffset, null) != 0)
                                {
                                    MessageBox.Show("write failed ee2, wrote = " + wrote);
                                    return false;
                                }
                            }
                        }
                        newdatacopied += size;
                        outfileoffset += size;

                        curroffset += ((Item)fptemp1).get_size();
                    }
                    //if (REDDY.ptrIFSDMux.SetEndOfFile(2, destfile, srcfi.Length, null) != 0)
                    //{
                    //    MessageBox.Show("failed to seteof 1a");
                    //    return false;
                    //}
                    fs.Close();
                    REDDY.FSIDList[2].set_dirty(true);
                    return true;
                }
            }
            else
            {
                DEFS.ASSERT(oldchecksumfile != null, "You must pass the oldchecksumfile path");

                FileInfo srcfi = new FileInfo(sourcefile);
                if (srcfi.Exists == false)
                {
                    REDDY.ptrIFSDMux.DeleteFile(2, destfile, null);
                    return false;
                }
                else
                {
                    if (REDDY.ptrIFSDMux.CreateFile(2, destfile, FileAccess.ReadWrite, FileShare.ReadWrite, FileMode.CreateNew, FileOptions.None, null) != 0) 
                    {
                        MessageBox.Show("Createfile has failed");
                        return false;
                    }
                    if (REDDY.ptrIFSDMux.SetEndOfFile(2, destfile, srcfi.Length, null) != 0)
                    {
                        MessageBox.Show("Set eof has failed!");
                        return false;
                    }

                    Inode_Info di = REDDY.ptrIFSDMux.GetFileInfoInternalAPI(2, destfile);
                    int localoffet = di.backupoffset;
                   
                    REDDY.ptrIFSDMux.SetInternalFlag(2, destfile, 0, curroffset);

                    int ino = di.ino;

                    byte[] buffer = new byte[4096];
                    byte[] tmpbuf = new byte[((Item)fptemp1).get_size()];
                    uint wrote = 0;

                    int bcount = OPS.NUML0(srcfi.Length);

                    FileStream fs = new FileStream(sourcefile, FileMode.Open);

                    long outfileoffset = 0;
                    byte[] lastchunkbuf = null;

                    DEFS.DEBUG("--------", bcount + ", ArrangeStartingPosition LOOP ");
                    for (int i = 0; i < bcount; i++)
                    {
                        int size = fs.Read(buffer, 0, 4096);
                        if (size < 4096)
                        {
                            lastchunkbuf = new byte[size];
                            for (int kx = size; kx < 4096; kx++)
                            {
                                buffer[kx] = 0;
                            }
                            for (int kx2 = 0; kx2 < size; kx2++)
                            {
                                lastchunkbuf[kx2] = buffer[kx2];
                            }
                        }
                        byte[] hash = md5.ComputeHash(buffer, 0, 4096);
                        fptemp1.inode = ino;
                        fptemp1.fbn = i;
                        for (int k = 0; k < 16; k++)
                        {
                            fptemp1.fp[k] = hash[k];
                        }

                        byte[] existinghash = new byte[24];
                        uint readsize = 0;

                        if (REDDY.ptrIFSDMux.ReadFile(2, oldchecksumfile, existinghash, ref readsize, localoffet, null) != 0)
                        {
                            MessageBox.Show("read failed, " + readsize + ",path = " + oldchecksumfile);
                            return false;
                        }
                        ((Item)fptemp2).parse_bytes(existinghash, 0);

                        if (!(/* fptemp1.inode == fptemp2.inode &&*/ fptemp1.fbn == fptemp2.fbn &&
                                        is_equal(fptemp1.fp, fptemp2.fp)))
                        {
                            if (size > 0)
                            {
                                if (size == 4096)
                                {
                                    if (REDDY.ptrIFSDMux.WriteFile(2, destfile, buffer, ref wrote, outfileoffset, null) != 0)
                                    {
                                        MessageBox.Show("write failed ee, wrote = " + wrote);
                                        return false;
                                    }
                                }
                                else
                                {
                                    if (REDDY.ptrIFSDMux.WriteFile(2, destfile, lastchunkbuf, ref wrote, outfileoffset, null) != 0)
                                    {
                                        MessageBox.Show("write failed ee2, wrote = " + wrote);
                                        return false;
                                    }
                                }
                            }
                            newdatacopied += size;
                        }

                        ((Item)fptemp1).get_bytes(tmpbuf, 0);
                        if (REDDY.ptrIFSDMux.WriteFile(2, newchecksumfile, tmpbuf, ref wrote, curroffset, null) != 0)
                        {
                            MessageBox.Show("write failed 22, wrote = " + wrote);
                            return false;
                        }

                        curroffset += ((Item)fptemp1).get_size();
                        localoffet += ((Item)fptemp1).get_size();
                        outfileoffset += size;
                        //DEFS.DEBUG("---", bcount + "," + fs.Position);
                    }

                    fs.Close();

                    if (REDDY.ptrIFSDMux.SetEndOfFile(2, destfile, srcfi.Length, null) != 0)
                    {
                        MessageBox.Show("Set eof has failed! 2");
                        return false;
                    }

                    DEFS.DEBUG("--------", bcount + ", ArrangeStartingPosition LOOP sdfsfda");
                    REDDY.FSIDList[2].set_dirty(true);
                    return true;
                }           
            }
            
        }

        private bool is_equal(byte[] h1, byte[] h2)
        {
            //DEFS.DEBUG("H", OPS.HashToString(h2) + "," + OPS.HashToString(h1));
            for (int i = 0; i < 16; i++) 
            {
                if (h1[i] != h2[i]) return false;
            }
            return true;
        }

        private void backup_folder(bool firstjob, string oldchecksumfile, string newchecksumfile, ref int curroffset, string sourcedir, string destdir)
        {
            DirectoryInfo srcdi = new DirectoryInfo(sourcedir);
            Inode_Info di = REDDY.ptrIFSDMux.GetFileInfoInternalAPI(2, destdir);

            if (srcdi.Exists == false && di == null)
            {
                return;
            }
            else if (srcdi.Exists == false && di != null)
            {
                REDDY.ptrIFSDMux.DeleteDirectory(2, destdir, null);
                return;
            }
            else
            {
                if (srcdi.Exists == true && di == null)
                {
                    /*
                     * Create a new directory and recusively restore directories and files.
                     */
                    REDDY.ptrIFSDMux.CreateDirectory(2, destdir, null);

                }
                else if (srcdi.Exists == true && di != null)
                {
                    /*
                     * Restore directory by directory, if dest subdir does not exist, call this recursviely.
                     * do the same for files.
                     */
                }

                string[] flist = get_file_list(sourcedir, destdir);
                for (int i = 0; i < flist.Length; i++) 
                {
                    backup_file(firstjob, oldchecksumfile, newchecksumfile, ref curroffset, sourcedir + "\\" + flist[i], destdir + "\\" + flist[i]);
                    REDDY.FSIDList[2].set_dirty(true);
                }

                string[] dlist = get_dir_list(sourcedir, destdir);
                for (int i = 0; i < dlist.Length; i++) 
                {
                    backup_folder(firstjob, oldchecksumfile, newchecksumfile, ref curroffset, sourcedir + "\\" + dlist[i], destdir + "\\" + dlist[i]);
                    REDDY.FSIDList[2].set_dirty(true);
                } 

            }
        }

        private string[] get_file_list(string sourcedir, string destdir)
        {
            //first restore files.
            string[] flist0 = Directory.GetFiles(sourcedir);
            Inode_Info[] ilistt = REDDY.ptrIFSDMux.FindFilesInternalAPI(2, destdir);

            int counter = 0;
            for (int i = 0; i < ilistt.Length; i++) 
            {
                if (ilistt[i].isfile) counter++;
            }

            string[] file_list = new string[flist0.Length + counter];
            for (int i = 0; i < flist0.Length; i++) 
            { 
                file_list[i] = (new FileInfo(flist0[i])).Name; 
            }
            int idx = flist0.Length;
            for (int i = 0; i < ilistt.Length; i++) 
            { 
                if (ilistt[i].isfile)
                    file_list[idx++] = ilistt[i].name; 
            }
            return file_list;
        }

        private string[] get_dir_list(string sourcedir, string destdir)
        {
            //first restore files.
            string[] dlist0 = Directory.GetDirectories(sourcedir);
            Inode_Info[] ilistt = REDDY.ptrIFSDMux.FindFilesInternalAPI(2, destdir);

            int counter = 0;
            for (int i = 0; i < ilistt.Length; i++)
            {
                if (ilistt[i].isfile == false) counter++;
            }

            string[] dir_list = new string[dlist0.Length + counter];
            for (int i = 0; i < dlist0.Length; i++)
            {
                dir_list[i] = (new DirectoryInfo(dlist0[i])).Name;
            }
            int idx = dlist0.Length;
            for (int i = 0; i < ilistt.Length; i++)
            {
                if (ilistt[i].isfile == false)
                    dir_list[idx++] = ilistt[i].name;
            }
            return dir_list;
        }

        private void Backup_Worker_Load(object sender, EventArgs e)
        {
            Thread t = new Thread(worker_thread);
            t.Start();
        }
    }
}
