using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;

namespace redfs_v2
{
    [Serializable()]
    public class backup_pair
    {
        private bool isfile;
        private string srcpath;
        private string destpath;

        public bool IsFile
        {
            get { return isfile; }
            set { isfile = value; }
        }

        public string SourcePath
        {
            get { return srcpath; }
            set { srcpath = value; }
        }

        public string DestinationPath
        {
            get { return destpath; }
            set { destpath = value; }
        }
    }

    [Serializable()]
    public class job_item
    {
        private int jobid;
        private string jobname;
        private string starttime;
        private long copieddata;

        public int JobID
        {
            get { return jobid; }
            set { jobid = value; }
        }

        public string JobName
        {
            get { return jobname; }
            set { jobname = value; }
        }

        public string StartTime
        {
            get { return starttime; }
            set { starttime = value; }
        }

        public long NewCopiedData
        {
            get { return copieddata; }
            set { copieddata = value; }
        }
    }

    [Serializable()]
    public class BackupTask
    {
        private int lastjobnumber;
        public int JobCount
        {
            get { return lastjobnumber; }
            set { lastjobnumber = value; }
        }

        private string taskname;
        public string TaskName
        {
            get { return taskname; }
            set { taskname = value; }
        }

        private List<backup_pair> settings = new List<backup_pair>();
        public List<backup_pair> BackupPaths
        {
            get { return settings; }
            set { settings = value; }
        }

        private List<job_item> jobhistory = new List<job_item>();
        public List<job_item> JobHistory
        {
            get { return jobhistory; }
            set { jobhistory = value; }
        }

        public string fetch_job_name(int jobid) 
        {
            for (int i = 0; i < jobhistory.Count; i++) 
            {
                job_item ji = jobhistory.ElementAt(i);
                if (ji.JobID == jobid) return ji.JobName;
            }
            return null;
        }
    }

    public class Backup_Utility
    {
        public static BackupTask load_task(string tname)
        {
            Inode_Info[] inodes = REDDY.ptrIFSDMux.FindFilesInternalAPI(2, "\\" + tname);

            for (int i = 0; i < inodes.Length; i++)
            {
                if (inodes[i].name.Equals("task.xml"))
                {

                    byte[] buffer = new byte[inodes[i].size];
                    uint readsize = 0;
                    REDDY.ptrIFSDMux.ReadFile(2, "\\" + tname + "\\task.xml", buffer, ref readsize, 0, null);
                    MemoryStream ms = new MemoryStream(buffer);
                    ms.Seek(0, SeekOrigin.Begin);
                    XmlSerializer SerializerObj = new XmlSerializer(typeof(BackupTask));
                    BackupTask bt = (BackupTask)SerializerObj.Deserialize(ms);
                    return bt;
                }
            }
            return null;
        }

        public static void save_task(string tname, BackupTask bt)
        {
            MemoryStream ms = new MemoryStream();
            XmlSerializer SerializerObj = new XmlSerializer(typeof(BackupTask));
            SerializerObj.Serialize(ms, bt);

            byte[] buf = ms.ToArray();
            uint writesize = 0;

            REDDY.ptrIFSDMux.CreateFile(2, "\\" + tname + "\\task.xml", FileAccess.ReadWrite,
                        FileShare.ReadWrite, FileMode.OpenOrCreate, FileOptions.None, null);
            REDDY.ptrIFSDMux.WriteFile(2, "\\" + tname + "\\task.xml", buf, ref writesize, 0, null);
            REDDY.ptrIFSDMux.CloseFile(2, "\\" + tname + "\\task.xml", null);
            DEFS.DEBUGYELLOW("TK", "Wrote file size = " + buf.Length);
        }
    }
}
