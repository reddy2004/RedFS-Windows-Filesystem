using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Threading;
using Dokan;

namespace redfs_v2
{
    public class CDirectory : CInode
    {
        private int m_associated_fsid = 0;

        private long m_size;
        private long m_atime;
        private long m_ctime;
        private long m_mtime;

        public int m_inode;
        public int m_parent_inode;

        private string m_name;
        private string m_path;

        private DIR_STATE m_state;
        private List<CInode> m_clist;

        private long creation_time;
        private int cache_time_secs = 0;

        private CDirectory m_parent = null;

        private RedFS_Inode _mywip;

        private ReaderWriterLockSlim _dirOpLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        /*
         * Below 4 functions are for CInode interface.
         */
        DateTime CInode.get_atime() { return new DateTime(m_atime); }
        DateTime CInode.get_ctime() { return new DateTime(m_ctime); }
        DateTime CInode.get_mtime() { return new DateTime(m_mtime); }

        int CInode.get_fsid_of_inode() { return m_associated_fsid; }

        void CInode.set_acm_times(DateTime a, DateTime c, DateTime m)
        {
            m_atime = a.Ticks;
            m_ctime = c.Ticks;
            m_mtime = m.Ticks;
        }

        bool CInode.checkname(string name)
        {
            return (name.Equals(m_name)) ? true : false;
        }

        long CInode.get_size()
        {
            return m_size;
        }

        string CInode.get_string_rep()
        {
            return (1 + "," + m_inode + "," + m_size + "," + m_atime + "," +
                        m_ctime + "," + m_mtime + "," + m_name);
        }
        string CInode.get_full_path() { return m_path; }
        FileAttributes CInode.gettype() { return FileAttributes.Directory; }
        string CInode.get_iname() { return m_name; }
        int CInode.get_inode_number() { return m_inode; }
        int CInode.get_parent_inode_number() { return m_parent_inode; }

        /*
         * Not using locks here. ideally we should have some kind of exclusive lock.
         */
        public void rename_directory(string newname)
        {
            m_name = newname;
            if (m_parent != null) //only null when a file is cloned and does not have a parent.
            {
                m_parent.set_dirty();
            }
        }

        /*
        * Below two functions are called with rootdir lock.
        */
        bool CInode.is_unmounted()
        {
            return (m_state == DIR_STATE.DIR_TERMINATED) ? true : false;
        }

        void CInode.unmount(bool inshutdown)
        {
            _dirOpLock.EnterWriteLock();
            try
            {
                bool do_unmount = false;
                DEFS.ASSERT(m_state != DIR_STATE.DIR_DELETED, "Cannot unmount deleted directory");

                switch (m_state)
                {
                    case DIR_STATE.DIR_TERMINATED:
                        break;

                    case DIR_STATE.NOT_LOADED:
                        DEFS.ASSERT(m_clist.Count == 0, "Count must be zero for NOT_LOADED dir, cnt = " +
                            m_clist.Count + " ino = " + m_inode);
                        m_clist.Clear();
                        break;

                    case DIR_STATE.DIR_IS_SKELETON:
                    case DIR_STATE.DIR_CONTENTS_INCORE:
                        do_unmount = true;
                        break;

                    case DIR_STATE.DIRFILE_DIRTY:
                        {
                            DEFS.ASSERT(REDDY.FSIDList[m_associated_fsid] != null, "Cannot unmount correctly because REDDY.pFSID is not present");
                            write_dir_todisk();
                        }

                        do_unmount = true;
                        break;
                }

                if (do_unmount)
                {
                    DEFS.ASSERT((m_state == DIR_STATE.DIR_CONTENTS_INCORE ||
                            m_state == DIR_STATE.DIR_IS_SKELETON), "One of the condition must be true");

                    for (int i = 0; i < m_clist.Count; i++)
                    {
                        m_clist[i].unmount(inshutdown);
                        DEFS.ASSERT(inshutdown == true, "Must be true for directory unmounts. otherwise gc should work.");
                        DEFS.ASSERT(m_clist[i].is_unmounted(), m_clist[i].get_full_path() + " should've been unmounted by now!");
                    }
                }

                if (m_name == null)
                {
                    m_state = DIR_STATE.NOT_LOADED;
                }
                else
                {
                    m_state = DIR_STATE.DIR_TERMINATED;
                }
                m_clist.Clear();
                DEFS.DEBUG("IFSDMux", "Finished unmounting directory " + m_inode + " name = " + m_name);
            }
            finally
            {
                _dirOpLock.ExitWriteLock();
            }
        }

        /*
         * It is always called from the GC thread. GC Thread can potentially modify
         * the incore contents of the directory and hence an exclusive lock is required.
         */
        public void gc()
        {
            _dirOpLock.EnterWriteLock();
          
            try
            {
                if (m_state != DIR_STATE.DIR_CONTENTS_INCORE &&
                        m_state != DIR_STATE.DIR_IS_SKELETON)
                {
                    return;
                }

                for (int i = 0; i < m_clist.Count; i++)
                {
                    DEFS.DEBUG("IFSD", "gc.. : " + m_clist[i].get_iname() + "(" + m_clist[i].get_inode_number() + ")" +  m_clist.Count);
                    if (m_clist[i].is_time_for_clearing())
                    {
                        DEFS.DEBUG("IFSD", "gc : " + m_clist[i].get_iname() + "(" + m_clist[i].get_inode_number() + ") done " +
                        _dirOpLock.CurrentReadCount + "," + _dirOpLock.IsWriteLockHeld + "," + _dirOpLock.WaitingReadCount);

                        if (m_clist[i].gettype() == FileAttributes.Directory)
                        {
                            CDirectory cd = (CDirectory)m_clist[i];
                            cd.gc();
                        }
                        else
                        {
                            m_clist[i].unmount(false);
                        }

                    }
                }

                //DEFS.DEBUG("-->", "GC: now remove unmounted items " + m_clist.Count);
                //Now remove unnecessary entries from m_clist and mark it as skeleton.
                for (int i = 0; i < m_clist.Count; i++)
                {
                    if (m_clist[i].is_unmounted())
                    {
                        DEFS.DEBUG("IFSD", "gc <REMOVE>: " + m_clist[i].get_iname() + "(" + m_clist[i].get_inode_number() + ") done");
                        m_clist.RemoveAt(i);
                        i--;
                    }
                }

                //DEFS.DEBUG("----------", "Leaving gc " + m_name + " , unmounted whatever i could");

                if (m_clist.Count == 0)
                {
                    if (m_inode != 0)
                    {
                        m_clist.Clear();
                        m_state = DIR_STATE.DIR_TERMINATED;
                        //m_clist = null; //not for mux
                    }
                    else
                    {
                        m_state = DIR_STATE.DIR_IS_SKELETON;
                    }
                }
                else
                {
                    m_state = DIR_STATE.DIR_IS_SKELETON;
                }
            }
            finally
            {
                //DEFS.DEBUG("----------", "Leaving gc " + m_name + " release writelock");
                _dirOpLock.ExitWriteLock();
            }
            //DEFS.DEBUG("----------", "Leaving gc " + m_name);
        }


        /* 
         * This could be added from disk, in that case dont load anything unless
         * asked to. This could also be scavenged out anytime.
         * 
         * IF its not from disk, then a user manually added this directory. so we
         * must mark it dirty so that this makes it to disk.
         */
        public CDirectory(int fsid, int ino, int pino, string name, string path, bool fromdisk)
        {
            m_associated_fsid = fsid;
            m_inode = ino;
            m_parent_inode = pino;
            m_name = name;
            m_path = path;

            if (!fromdisk) m_state = DIR_STATE.DIRFILE_DIRTY;
            else m_state = DIR_STATE.NOT_LOADED;
            m_clist = new List<CInode>(1);

            cache_time_secs = (name != null) ? (20) : 100000000;

            m_atime = m_ctime = m_mtime = DateTime.Now.ToUniversalTime().Ticks;
            m_size = 0;
            touch();
        }

        public CDirectory(int fsid, int ino, int pino, string name, string path, long size, long atime, long ctime, long mtime)
        {
            m_associated_fsid = fsid;
            m_inode = ino;
            m_parent_inode = pino;
            m_name = name;
            m_path = path;

            m_clist = new List<CInode>(1);

            cache_time_secs = (name != null) ? (20) : 100000000;

            m_atime = atime;
            m_ctime = ctime;
            m_mtime = mtime;
            m_size = 0;
            touch();
        }

        void touch()
        {
            creation_time = DateTime.Now.ToUniversalTime().Ticks;
        }

        bool CInode.is_time_for_clearing()
        {
            long curr = DateTime.Now.ToUniversalTime().Ticks;
            int seconds = (int)((curr - creation_time) / 10000000);
            if (seconds > cache_time_secs) return true;
            else return false;
        }

        public bool can_delete_dir()
        {
            return (m_state == DIR_STATE.DIR_DELETED) ? true : false;
        }

        /*
         * Earlier it was called only from GC thread, now it can be called from anywhere.
         * No issues if they are called in parallel threads. So Shared lock is good. Also
         * we dont want to hog sync because it affects findfinds etc kind of ops.
         */
        public void sync()
        {
            _dirOpLock.EnterReadLock();
            try
            {
                if (m_state == DIR_STATE.DIR_DELETED) return;

                for (int i = 0; i < m_clist.Count; i++)
                {
                    if (m_clist[i].gettype() == FileAttributes.Normal)
                    {
                        CFile cf = (CFile)m_clist[i];
                        cf.sync();
                    }
                    else
                    {
                        CDirectory cdir = (CDirectory)m_clist[i];
                        cdir.sync();
                    }
                }

                if (m_state != DIR_STATE.DIRFILE_DIRTY) return;
                write_dir_todisk();
                m_state = DIR_STATE.DIR_CONTENTS_INCORE;
            }
            finally
            {
                _dirOpLock.ExitReadLock();
            }
        }

        /*
         * This function will not create or hold any locks. 
         * delete_directory_recursively() is the only caller.
         * Called with XClusive lock held.
         */
        private void remove_ondisk_data()
        {
            create_wip_if_not_exists();

            DEFS.ASSERT(_mywip != null, "My wip should've been loaded in remove() after calling create_ifnot..()");
            lock (REDDY.FSIDList[m_associated_fsid])
            {
                RedFS_Inode inowip = REDDY.FSIDList[m_associated_fsid].get_inode_file_wip("DD:" + m_name);
                REDDY.ptrRedFS.redfs_delete_wip(m_associated_fsid, _mywip, false);

                DEFS.ASSERT(_mywip.get_filesize() == 0, "After delete, all the wip(d) contents must be cleared off");
                for (int i = 0; i < 16; i++)
                {
                    DEFS.ASSERT(_mywip.get_child_dbn(i) == DBN.INVALID, "dbns are not set after delete wip(d) " +
                        i + "  " + _mywip.get_child_dbn(i));
                }
                OPS.CheckinZerodWipData(inowip, m_inode);
                REDDY.FSIDList[m_associated_fsid].sync_internal();
            }
            _mywip = null;
            DEFS.DEBUG("IFSD", "<<<< DELETED DIR >>>> " + m_name);
            m_state = DIR_STATE.DIR_DELETED;
        }

        /*
         * The callers of this function are
         * remove_ondisk_data() - wont lock, but must be called with lock held.
         * read_ondisk_dir()
         * write_dir_todisk()
         * all of which modify the internal m_clist & _mywip, so an exclusive lock is required. The
         * callers must hold the exclusive lock, the function therefore will not lock anything
         * except to get the inowip.
         */
        private void create_wip_if_not_exists()
        {
            if (_mywip == null)
            {
                lock (REDDY.FSIDList[m_associated_fsid])
                {
                    if (_mywip != null)
                    {
                        /*
                         * This can be called from many codepaths with shared/exclusive locks here. so
                         * we must use our own lock here.
                         */
                        return;
                    }
                    _mywip = new RedFS_Inode(WIP_TYPE.DIRECTORY_FILE, m_inode, m_parent_inode);

                    RedFS_Inode inowipt = REDDY.FSIDList[m_associated_fsid].get_inode_file_wip("OD:" + m_name);
                    bool ret = OPS.Checkout_Wip2(inowipt, _mywip, m_inode);
                    if (ret)
                    {
                        DEFS.DEBUG("DIR", "Loaded ino= " + m_inode + "wip from disk, size = " + _mywip.get_filesize());
                    }
                    else
                    {
                        DEFS.DEBUG("DIR", "Loaded ino = " + m_inode + " (new2) size = " + _mywip.get_filesize());
                        _mywip.set_ino(m_parent_inode, m_inode);
                        DEFS.ASSERT(_mywip.get_filesize() == 0, "File size should match for dir ino = " + m_inode + " size2 = " + _mywip.get_filesize());
                    }
                    REDDY.FSIDList[m_associated_fsid].sync_internal();
                    DEFS.DEBUG("@@@@", "Loaded ino= " + m_inode + "wip from disk, size = " + _mywip.get_filesize());
                }
                _mywip.is_dirty = true;
            }
        }

        /*
         * This can be called from the following functions - Prettry much all public interfaces.
         * Hence a fencing lock is required so that all of them dont execute the same code and
         * lead to some corruption.
         */
        private void read_ondisk_dir()
        {
            if (m_state == DIR_STATE.DIR_CONTENTS_INCORE ||
                    m_state == DIR_STATE.DIRFILE_DIRTY ||
                    m_state == DIR_STATE.DIR_TERMINATED ||
                    m_state == DIR_STATE.DIR_DELETED)
            {
                return;
            }

            bool skeleton = (m_state == DIR_STATE.DIR_IS_SKELETON) ? true : false;

            int counter = 0;
            string line;

            create_wip_if_not_exists();

            m_state = DIR_STATE.DIRFILE_LOADING;

            /*
             * This locks acts as a fence.
             */
            lock (m_clist)
            {
                if (m_state == DIR_STATE.DIR_CONTENTS_INCORE ||
                        m_state == DIR_STATE.DIRFILE_DIRTY ||
                        m_state == DIR_STATE.DIR_TERMINATED ||
                        m_state == DIR_STATE.DIR_DELETED)
                {
                    return;
                }

                byte[] bdir = REDDY.ptrRedFS.redfs_read_dirfile(_mywip);
                MemoryStream ms = new MemoryStream(bdir);
                StreamReader sr1 = new StreamReader(ms);

                while ((line = sr1.ReadLine()) != null)
                {
                    if (counter == 0)
                    {
                        counter = 1;
                        DEFS.DEBUG("Cirector", "Read dir contents of " + line + " ( " + m_path + " ), state = " + m_state);
                        continue;
                    }
                    counter++;

                    DEFS.DEBUG("IFSD", line);
                    char[] sep = { ',' };
                    string[] list = line.Split(sep, 8);
                    int type = Int32.Parse(list[0]);
                    int ino = Int32.Parse(list[1]);

                    long size = Int64.Parse(list[2]);
                    long atime = Int64.Parse(list[3]);
                    long ctime = Int64.Parse(list[4]);
                    long mtime = Int64.Parse(list[5]);

                    bool to_insert = (skeleton) ? ((inode_exists_skeleton(list[6]) == null) ? true : false) : (true);

                    if (to_insert)
                    {
                        if (type == 0)
                        {
                            insert_new_ondisk_file(ino, list[6], size, atime, ctime, mtime);
                        }
                        else if (type == 1)
                        {
                            insert_new_ondisk_directory(ino, list[6], size, atime, ctime, mtime);
                        }
                    }
                    counter++;
                }
                sr1.Close();
                ms.Close();
                m_state = DIR_STATE.DIR_CONTENTS_INCORE;
                touch();
            }
        }

        /*
         * This function can be called from
         * unmount()            XClusive
         * sync()               Shared
         * dup_directory()      XClusive
         * So no need to take any explicit locks here, also this is private function.
         */
        private void write_dir_todisk()
        {
            DEFS.ASSERT(m_state == DIR_STATE.DIRFILE_DIRTY, "Cannot write-dir-to disk which is not dirty");
            create_wip_if_not_exists();

            MemoryStream b = new MemoryStream();
            StreamWriter sw = new StreamWriter(b);

            DEFS.DEBUG("IFSD", "Flushing " + m_name + " to disk WIP BASED");

            if (m_name == null) sw.WriteLine("_rootdir");
            else sw.WriteLine(m_path);

            for (int i = 0; i < m_clist.Count; i++)
            {
                sw.WriteLine(m_clist[i].get_string_rep());
                DEFS.DEBUG("IFSD-", m_clist[i].get_string_rep());
            }
            sw.Flush();

            _mywip.setfilefsid_on_dirty(m_associated_fsid);
            REDDY.ptrRedFS.redfs_write_dirfile(m_associated_fsid, _mywip, b.ToArray());
            REDDY.ptrRedFS.sync(_mywip); //caused me huge headaches!!.

            lock (REDDY.FSIDList[m_associated_fsid])
            {
                RedFS_Inode inowip = REDDY.FSIDList[m_associated_fsid].get_inode_file_wip("write dir:" + m_name);
                OPS.Checkin_Wip(inowip, _mywip, m_inode);
                REDDY.FSIDList[m_associated_fsid].sync_internal();
                REDDY.FSIDList[m_associated_fsid].set_dirty(true);
            }
            m_state = DIR_STATE.DIR_CONTENTS_INCORE;
            sw.Close();
        }

        private CDirectory get_directory(string name)
        {
            DEFS.ASSERT(m_state != DIR_STATE.DIR_DELETED || m_state == DIR_STATE.DIR_TERMINATED ||
                m_state == DIR_STATE.DIRFILE_LOADING, "Dir state in get-dir cannot be deleted/terminted/loading");

            //if (m_state == DIR_STATE.DIR_IS_SKELETON)
            //{
            //    CInode ci = inode_exists_skeleton(name);
            //    if (ci != null && ci.gettype() == FileAttributes.Directory)
            //        return (CDirectory)ci;
            //}
            //
            //if (DIR_STATE.NOT_LOADED == m_state ||
            //        m_state == DIR_STATE.DIR_IS_SKELETON)
            //{
            //    read_ondisk_dir();
            //}
            open_directory();

            for (int i = 0; i < m_clist.Count; i++)
            {
                if (m_clist[i].checkname(name) == true &&
                    m_clist[i].gettype() == FileAttributes.Directory)
                    return (CDirectory)m_clist[i];
            }
            touch();
            return null;
        }

        /*
         * Can be called from get_directory (Shared lock)
         * read_ondisk_dir() -> which inturn may be called by many functions with S/X locks.
         */
        private CInode inode_exists_skeleton(string name)
        {
            for (int i = 0; i < m_clist.Count; i++)
            {
                if (m_clist[i].checkname(name) == true)
                    return m_clist[i];
            }
            touch();
            return null;
        }

        /*
         * Always call internally with a lock. so no issues here.
         */
        private CInode inode_exists(string name)
        {
            if (DIR_STATE.NOT_LOADED == m_state ||
                    m_state == DIR_STATE.DIR_IS_SKELETON)
            {
                read_ondisk_dir();
            }
            else if (m_state == DIR_STATE.DIRFILE_LOADING)
            {
                return null;
            }

            for (int i = 0; i < m_clist.Count; i++)
            {
                if (m_clist[i].checkname(name) == true)
                {
                    return m_clist[i];
                }
            }
            touch();
            return null;
        }

        /*
         * Caller must have an exclusive lock.
         */
        private bool delete_file(string name)
        {
            if (inode_exists(name) == null)
            {
                DEFS.DEBUG("IFSD", "Cannot delete " + name + " into " + m_name + " as its not exist!");
                return false;
            }

            if (m_state == DIR_STATE.NOT_LOADED ||
                    m_state == DIR_STATE.DIR_IS_SKELETON)
            {
                read_ondisk_dir();
            }

            for (int i = 0; i < m_clist.Count; i++)
            {
                if (m_clist[i].checkname(name) == true &&
                    m_clist[i].gettype() == FileAttributes.Normal)
                {
                    CFile t = (CFile)m_clist[i];
                    m_clist.RemoveAt(i);
                    t.remove_ondisk_data2();
                    m_state = DIR_STATE.DIRFILE_DIRTY;
                    touch();
                    return true;
                }
            }
            return false;
        }

        /*
         * Caller must have XClusive lock.
         */ 
        private bool delete_directory(string name)
        {
            if (inode_exists(name) == null)
            {
                DEFS.DEBUG("IFSD", "Cannot delete " + name + " in " + m_name + " as its not exist!");
                return false;
            }

            if (m_state == DIR_STATE.NOT_LOADED ||
                    m_state == DIR_STATE.DIR_IS_SKELETON)
            {
                read_ondisk_dir();
            }

            for (int i = 0; i < m_clist.Count; i++)
            {
                if (m_clist[i].checkname(name) == true &&
                    m_clist[i].gettype() == FileAttributes.Directory)
                {
                    CDirectory t = (CDirectory)m_clist[i];

                    if (t.can_delete_dir() == false)
                    {
                        DEFS.DEBUG("IFSD", "Cannot delete " + name + " dir coz of some error");
                        return false;
                    }

                    m_clist.RemoveAt(i);
                    m_state = DIR_STATE.DIRFILE_DIRTY;
                    touch();
                    return true;
                }
            }
            return false;
        }

        /*
         * Requires modification to the clist and other internal states.
         * Caller must have X lock.
         */
        private bool insert_new_file(int ino, string name, bool fromdisk)
        {
            if (inode_exists(name) != null)
            {
                DEFS.DEBUG("IFSD", "Cannot insert " + name + " into " + m_name + " as it already exists!");
                return false;
            }

            read_ondisk_dir();

            /*
            * This is a new file, so we open_file() so that the wip is loaded, and can be written back
            * with the correct inode number - even if we never do any io to it.
            */
            CFile cf = new CFile(m_associated_fsid, ino, m_inode, name, (m_path + "\\" + name));
            cf.open_file(true);

            cf.m_parent = this;
            m_clist.Add(cf);

            if (!fromdisk)
            {
                m_state = DIR_STATE.DIRFILE_DIRTY;
            }

            touch();
            return true;
        }

        /*
         * Called from read_ondisk_dir, which inturn is called from either shared or exlusive lock.
         * So this function will not take any lock.
         */
        private bool insert_new_ondisk_file(int ino, string name, long size, long atime, long ctime, long mtime)
        {
            if (inode_exists(name) != null)
            {
                DEFS.DEBUG("IFSD", "Cannot insert " + name + " into " + m_name + " as it already exists2!");
                return false;
            }

            CFile cf = new CFile(m_associated_fsid, ino, m_inode, name, (m_path + "\\" + name), size, atime, ctime, mtime);
            cf.m_parent = this;
            m_clist.Add(cf);
            touch();
            return true;
        }

        //must fix 
        private bool insert_new_file_cloned(CFile cfclone)
        {
            string name = ((CInode)cfclone).get_iname();

            if (inode_exists(name) != null)
            {
                DEFS.DEBUG("IFSD", "Cannot insert " + name + " into " + m_name + " as it already exists! ERROR");
                return false;
            }

            read_ondisk_dir();

            cfclone.m_parent = this;
            m_clist.Add(cfclone);
            m_state = DIR_STATE.DIRFILE_DIRTY;

            touch();
            return true;
        }


        private bool insert_new_directory_cloned(CDirectory cdclone)
        {
            string name = ((CInode)cdclone).get_iname();

            if (inode_exists(name) != null)
            {
                DEFS.DEBUG("IFSD", "Cannot insert " + name + " into " + m_name + " as it already exists! ERROR");
                return false;
            }

            read_ondisk_dir();

            cdclone.m_parent = this;
            m_clist.Add(cdclone);
            m_state = DIR_STATE.DIRFILE_DIRTY;

            touch();
            return true;
        }

        /*
         * This is called from read_dir, and we know that some lock is held on both _dirlock and m_clist.
         */
        private bool insert_new_ondisk_directory(int ino, string name, long size, long atime, long ctime, long mtime)
        {
            if (inode_exists(name) != null)
            {
                DEFS.DEBUG("IFSD", "Cannot insert " + name + " into " + m_name + " as it already exists3!");
                return false;
            }

            CDirectory cd = new CDirectory(m_associated_fsid, ino, m_inode, name, (m_path + "\\" + name), size, atime, ctime, mtime);
            m_clist.Add(cd);
            touch();
            return true;
        }

        private bool insert_new_directory(int ino, string name, bool fromdisk)
        {
            if (inode_exists(name) != null)
            {
                DEFS.DEBUG("IFSD", "Cannot insert " + name + " into " + m_name + " as it already exists!");
                return false;
            }
            if (!fromdisk)
            {
                read_ondisk_dir();
            }

            CDirectory cd = new CDirectory(m_associated_fsid, ino, m_inode, name, (m_path + "\\" + name), fromdisk);
            cd.m_parent = this;
            m_clist.Add(cd);
            if (!fromdisk)
            {
                m_state = DIR_STATE.DIRFILE_DIRTY;
            }
            touch();
            return true;
        }

        /*
         * Open a directory, i.e load the contents incore.
         * Must always be called with an XClusive lock held.
         */
        private bool open_directory()
        {
            DEFS.ASSERT(m_state != DIR_STATE.DIR_DELETED, "Cannot open-dir " + m_path);
            switch (m_state)
            {
                case DIR_STATE.NOT_LOADED:
                case DIR_STATE.DIR_IS_SKELETON:
                    read_ondisk_dir();
                    return true;

                case DIR_STATE.DIR_CONTENTS_INCORE:
                    return true;

                case DIR_STATE.DIR_TERMINATED:
                    return false;

                case DIR_STATE.DIRFILE_DIRTY:
                    return true;
            }
            return false;
        }

        public CInode check_if_inode_incore(int ino, bool loadandcheck)
        {
            if (m_inode == ino) return this;
            else if (m_clist == null || m_state != DIR_STATE.DIR_CONTENTS_INCORE)
            {
                if (!loadandcheck) return null;

                _dirOpLock.EnterWriteLock();
                try
                {
                    open_directory();
                }
                finally
                {
                    _dirOpLock.ExitWriteLock();
                }
            }

            _dirOpLock.EnterReadLock();
            try
            {
                for (int i = 0; i < m_clist.Count; i++)
                {
                    if (m_clist[i].get_inode_number() == ino) return m_clist[i];

                    if (m_clist[i].gettype() == FileAttributes.Directory)
                    {
                        CInode ci = ((CDirectory)m_clist[i]).check_if_inode_incore(ino, loadandcheck);
                        if (ci != null)
                        {
                            return ci;
                        }
                    }
                }
                return null;
            }
            finally
            {
                _dirOpLock.ExitReadLock();
            }
        }

        private Inode_Info[] find_files()
        {
            read_ondisk_dir();

            Inode_Info[] entries = new Inode_Info[m_clist.Count];

            for (int c = 0; c < entries.Length; c++)
            {
                entries[c] = new Inode_Info();
                entries[c].name = m_clist[c].get_iname();
                entries[c].isfile = (((CInode)m_clist[c]).gettype() == FileAttributes.Normal) ? true : false;
                entries[c].size = m_clist[c].get_size();
                entries[c].LastAccessTime = m_clist[c].get_atime();
                entries[c].LastWriteTime = m_clist[c].get_mtime();
                entries[c].CreationTime = m_clist[c].get_ctime();
                entries[c].fa = m_clist[c].gettype();
            }
            return entries;
        }

        /*
         * If some file is modified, we must mark this as dirty.
         * This is done so that filesize, attrs etc are captured in this dirfile.
         * 
         * XXXXXX. This is always called from a child, i.e parent->set_dirty() where this
         * directory happens to be the parent. We already have some kind of lock on this dir,
         * and so no need for another lock.
         */
        public void set_dirty()
        {
            if (m_state == DIR_STATE.DIR_TERMINATED ||
                        m_state == DIR_STATE.DIRFILE_DIRTY ||
                        m_state == DIR_STATE.DIR_DELETED)
            {
                return;
            }

            read_ondisk_dir();
            m_state = DIR_STATE.DIRFILE_DIRTY;
        }

        /*
         * Note that no locks are needed here. This function is called on an orphan
         * directory which is not a part of any filesystem/fsid tree.
         */ 
        public void remove_orphan_directory()
        {
            delete_directory_recursively();
        }

        /*
         * Delete the subdirs recursively and remove the ondisk dirfile
         * corresponding to this directory. Mark dir as deleted afterwards.
         * 
         * Exclusive lock is required.
         */
        private void delete_directory_recursively()
        {
            //DEFS.ASSERT(m_state != DIR_STATE.DIR_DELETED, "Cannot recursive-del on an already deleted dir " + m_path);
            read_ondisk_dir();

            for (int i = 0; i < m_clist.Count; i++)
            {
                if (m_clist[i].gettype() == FileAttributes.Directory)
                {
                    ((CDirectory)m_clist[i]).delete_directory_recursively();
                }
                else
                {
                    ((CFile)m_clist[i]).remove_ondisk_data2();
                }
            }

            m_clist.Clear();
            remove_ondisk_data();
            m_clist = null;
            m_state = DIR_STATE.DIR_DELETED;
        }

        /*
         * Returns number of children. Must have shared lock i guess.
         */
        private int get_total_children_including_self()
        {
            if (m_state == DIR_STATE.DIR_DELETED) return 0;

            open_directory();

            int counter = 0;
            for (int i = 0; i < m_clist.Count; i++)
            {
                if (m_clist[i].gettype() == FileAttributes.Directory)
                {
                    counter += ((CDirectory)m_clist[i]).get_total_children_including_self();
                }
                else
                {
                    counter++;
                }
                DEFS.DEBUG("CTR", "get_total_children_including_self = " + counter + "/" + m_clist.Count + " name = " + m_clist[i].get_iname());
                touch();
            }
            return counter + 1;
        }

        /*
        * This Directory dups itself into another directory tree, the old version is not deleted.
        * More like a new reincarnate without loosing old data. The responsibility
        * of updating path/name is left to the parent CDirectory, as there are only incore data
        * from the POV of CDirectory.
         * 
         * First make dup dir and then clone each file.
         * lock(rootdir) must be held on source, destination dir is not in dir_tree.
         * Lock (FSID_List[newfsid]) is required.
        */
        private CDirectory dup_directory(int newfsid, List<int> inonumbers, int pino, string basepath)
        {
            DEFS.ASSERT(inonumbers.Count > 0, "We cannot proceed cloning here");

            if (m_state == DIR_STATE.DIR_DELETED) return null;

            open_directory();

            if (m_state == DIR_STATE.DIRFILE_DIRTY)
            {
                write_dir_todisk();
            }

            DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE, "Incorrect state after read");

            REDDY.ptrRedFS.flush_cache(_mywip, false);
            touch();

            //should we write the source inodefile to disk also?
            int mynewinode = inonumbers.ElementAt(0);
            inonumbers.RemoveAt(0);
            CDirectory cdnew = new CDirectory(newfsid, mynewinode, pino, m_name, basepath + "\\" + m_name, false);

            //not a good idea, need locks so that this does not get scavenged out.
            for (int i = 0; i < m_clist.Count; i++)
            {
                if (m_clist[i].gettype() == FileAttributes.Directory)
                {
                    //int numblocks = OPS.FSIZETONUMBLOCKS(((CDirectory)m_clist[i])._mywip.get_filesize());
                    int numblocks = OPS.NUML0(((CDirectory)m_clist[i])._mywip.get_filesize());
                    REDDY.FSIDList[newfsid].diff_upadate_logical_data(numblocks * 4096);
                    REDDY.FSIDList[newfsid].set_dirty(true);
                    DEFS.DEBUGYELLOW("INCRCNT", "Grew fsid usage by  " + numblocks + " (dir)");
                    CDirectory cd = ((CDirectory)m_clist[i]).dup_directory(newfsid, inonumbers, mynewinode, cdnew.m_path);
                    cd.m_parent = cdnew;
                    cdnew.m_clist.Add(cd);
                }
                else
                {
                    int inoc = inonumbers.ElementAt(0);
                    inonumbers.RemoveAt(0);
                    CFile cf = ((CFile)m_clist[i]).dup_file(newfsid, inoc, mynewinode, cdnew.m_path);
                    cf.m_parent = cdnew;
                    cdnew.m_clist.Add(cf);
                }
                touch();
            }
            cdnew.m_state = DIR_STATE.DIRFILE_DIRTY;
            cdnew.write_dir_todisk();
            REDDY.FSIDList[newfsid].set_dirty(true);
            return cdnew;
        }

        /*
         * ADDED after the fucking crash where i lost everything!
         */
        private bool is_root_path(String path)
        {
            char[] sep = { '\\' };
            String[] list = path.Split(sep, StringSplitOptions.RemoveEmptyEntries);
            if (list.Length == 0)
            {
                return true;
            }
            return false;
        }

        private string[] resolve_pathstring(string path)
        {
            char[] sep = { '\\' };
            String[] list = path.Split(sep, StringSplitOptions.RemoveEmptyEntries);

            DEFS.ASSERT(list.Length != 0, "lenght cannot resolve to zero in resolve_pathstring:" + path);
            if (list.Length == 1)
            {
                string[] retval = new string[2];
                retval[0] = null;
                retval[1] = list[0];
                return retval;
            }
            else
            {
                string[] retval = new string[2];
                retval[0] = list[0];
                retval[1] = "";                
                for (int i = 1; i < list.Length; i++)
                {
                    retval[1] += "\\" + list[i];
                }
                return retval;
            }
        }

        public bool create_file_tlock(int ino, string absoluteFilePath)
        {
            DEFS.DEBUG("TLOCK", "Entering create_file_tlock(" + absoluteFilePath + ")");

            DEFS.ASSERT(is_root_path(absoluteFilePath) == false, "Cannot be root path in create_file_tlock, " + absoluteFilePath);
            string[] result = resolve_pathstring(absoluteFilePath);
            if (result[0] == null)
            {
                _dirOpLock.EnterWriteLock();
                try
                {
                    return insert_new_file(ino, result[1], false);
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func create_file_tlock(1)");
                    _dirOpLock.ExitWriteLock();
                }
            }
            else
            {
                _dirOpLock.EnterReadLock();
                try
                {
                    CDirectory cdir = get_directory(result[0]);
                    return (cdir != null)? cdir.create_file_tlock(ino, result[1]) : false;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                            m_state + " in func create_file_tlock(2)");
                    _dirOpLock.ExitReadLock();
                }
            }
        }

        public bool create_directory_tlock(int ino, string absoluteDirectoryPath)
        {
            DEFS.DEBUG("TLOCK", "Entering create_directory_tlock(" + absoluteDirectoryPath + ")");

            if (is_root_path(absoluteDirectoryPath)) 
            {
                return false;
            }

            string[] result = resolve_pathstring(absoluteDirectoryPath);
            if (result[0] == null)
            {
                _dirOpLock.EnterWriteLock();
                try
                {
                    return insert_new_directory(ino, result[1], false);
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func create_directory_tlock(1)");
                    _dirOpLock.ExitWriteLock();
                }
            }
            else
            {
                _dirOpLock.EnterReadLock();
                try
                {
                    CDirectory cdir = get_directory(result[0]);
                    return (cdir != null) ? cdir.create_directory_tlock(ino, result[1]) : false;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func create_directory_tlock(2)");
                    _dirOpLock.ExitReadLock();
                }
            }        
        }

        public bool open_directory_tlock(string absoluteDirectoryPath)
        {
            DEFS.DEBUG("TLOCK", "Entering open_directory_tlock(" + absoluteDirectoryPath +  ")");

            if (is_root_path(absoluteDirectoryPath))
            {
                _dirOpLock.EnterWriteLock();
                try
                {
                    open_directory();
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                            m_state + " in func open_directory_tlock(1)");
                    _dirOpLock.ExitWriteLock();
                }
                return true;
            }
            else
            {
                DEFS.ASSERT(is_root_path(absoluteDirectoryPath) == false, "Cannot be root path in open_directory_tlock," + absoluteDirectoryPath);
                string[] result = resolve_pathstring(absoluteDirectoryPath);

                if (result[0] == null)
                {
                    _dirOpLock.EnterWriteLock();
                    try
                    {
                        CDirectory cdir = get_directory(result[1]);
                        return (cdir != null) ? cdir.open_directory() : false;
                    }
                    finally
                    {
                        DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                            m_state + " in func open_directory_tlock(2)");
                        _dirOpLock.ExitWriteLock();
                    }
                }
                else
                {
                    _dirOpLock.EnterReadLock();
                    try
                    {
                        CDirectory cdir = get_directory(result[0]);
                        return (cdir != null) ? cdir.open_directory_tlock(result[1]) : false;
                    }
                    finally
                    {
                        DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                                m_state + " in func open_directory_tlock(3)");
                        _dirOpLock.ExitReadLock();
                    }
                }               
            }
        }

        public bool rename_inode_tlock(string absolutePath, string newname)
        {
            DEFS.DEBUG("TLOCK", "Entering rename_inode_tlock(" + absolutePath + "," + newname  + ")");

            DEFS.ASSERT(is_root_path(absolutePath) == false, "Cannot be root path in rename_inode_tlock, " + absolutePath + "," + newname);
            string[] result = resolve_pathstring(absolutePath);

            if (result[0] == null)
            {
                _dirOpLock.EnterWriteLock();
                try
                {
                    CInode ci = inode_exists(result[1]);
                    if (ci == null) return false;

                    if (ci.gettype() == FileAttributes.Normal) ((CFile)ci).rename_file(newname);
                    else ((CDirectory)ci).rename_directory(newname);
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                     m_state + " in func rename_inode_tlock(1)");
                    _dirOpLock.ExitWriteLock();
                }
                return true;
            }
            else
            {
                _dirOpLock.EnterReadLock();
                try
                {
                    CDirectory cdir = get_directory(result[0]);
                    return (cdir != null) ? cdir.rename_inode_tlock(result[1], newname) : false;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                    m_state + " in func rename_inode_tlock(2)");
                    _dirOpLock.ExitReadLock();
                }
            }
        }

        public bool delele_file_tlock(string absoluteFilePath)
        {
            DEFS.DEBUG("TLOCK", "Entering delele_file_tlock(" + absoluteFilePath + ")");

            DEFS.ASSERT(is_root_path(absoluteFilePath) == false, "Cannot be root path in delele_file_tlock, " + absoluteFilePath);
            string[] result = resolve_pathstring(absoluteFilePath);

            if (result[0] == null)
            {
                _dirOpLock.EnterWriteLock();
                try
                {
                    return delete_file(result[1]);
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func delele_file_tlock(1)");
                    _dirOpLock.ExitWriteLock();
                }
            }
            else
            {
                _dirOpLock.EnterReadLock();
                try
                {
                    CDirectory cdir = get_directory(result[0]);
                    return (cdir != null) ? cdir.delele_file_tlock(result[1]) : false;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func delele_file_tlock(2)");
                    _dirOpLock.ExitReadLock();
                }
            }           
        }

        public bool delete_directory_tlock(string absoluteDirectoryPath)
        {
            DEFS.DEBUG("TLOCK", "Entering delete_directory_tlock(" + absoluteDirectoryPath + ")");

            DEFS.ASSERT(is_root_path(absoluteDirectoryPath) == false, "Cannot be root path in delete_directory_tlock, " + absoluteDirectoryPath);
            string[] result = resolve_pathstring(absoluteDirectoryPath);

            if (result[0] == null)
            {
                _dirOpLock.EnterWriteLock();
                try
                {
                    CDirectory cdir = get_directory(result[1]);
                    if (cdir != null)
                    {
                        cdir.delete_directory_recursively();
                        return delete_directory(result[1]);
                    }
                    else
                    {
                        return false;
                    }
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func delete_directory_tlock(1)");
                    _dirOpLock.ExitWriteLock();
                }
            }
            else
            {
                _dirOpLock.EnterReadLock();
                try
                {
                    CDirectory cdir = get_directory(result[0]);
                    return (cdir != null) ? cdir.delete_directory_tlock(result[1]) : false;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func delete_directory_tlock(2)");
                    _dirOpLock.ExitReadLock();
                }
            }              
        }

        public Inode_Info get_inodeinfo_tlock(string absoluteInodePath, string caller, bool getcount, bool populate_bkupflag)
        {
           
            if (is_root_path(absoluteInodePath))
            {
                _dirOpLock.EnterReadLock();
                try
                {
                    open_directory();
                    Inode_Info entrie2 = new Inode_Info();
                    entrie2.name = null;
                    entrie2.isfile = false;
                    entrie2.size = 0;
                    entrie2.LastAccessTime = new DateTime(m_atime);
                    entrie2.LastWriteTime = new DateTime(m_mtime);
                    entrie2.CreationTime = new DateTime(m_ctime);
                    entrie2.fa = FileAttributes.Directory;
                    entrie2.ino = 0;
                    if (getcount) entrie2.nodecount = 1;
                    if (populate_bkupflag) entrie2.backupoffset = -1;
                    return entrie2;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                            m_state + " in func get_inodeinfo_tlock(1)");
                    _dirOpLock.ExitReadLock();
                }
            }
            else
            {
                DEFS.ASSERT(is_root_path(absoluteInodePath) == false, "Cannot be root path in get_inodeinfo_tlock, " + absoluteInodePath);
                string[] result = resolve_pathstring(absoluteInodePath);

                if (result[0] == null)
                {
                    _dirOpLock.EnterReadLock();
                    try
                    {
                        CInode ixthisone = inode_exists(result[1]);
                        if (ixthisone == null) return null;

                        Inode_Info entrie = new Inode_Info();
                        entrie.name = ixthisone.get_iname();
                        entrie.isfile = (ixthisone.gettype() == FileAttributes.Normal) ? true : false;
                        entrie.fa = ixthisone.gettype();
                        entrie.size = (ixthisone.gettype() == FileAttributes.Normal) ? ((CFile)ixthisone).m_size : 0;
                        entrie.LastAccessTime = ixthisone.get_atime();
                        entrie.LastWriteTime = ixthisone.get_mtime();
                        entrie.CreationTime = ixthisone.get_ctime();
                        entrie.ino = ixthisone.get_inode_number();

                        if (getcount) {
                            DEFS.DEBUG("$$$", "User wants getcount from this path " + absoluteInodePath + ", type = " + ixthisone.gettype());
                            if (ixthisone.gettype() == FileAttributes.Normal)
                                entrie.nodecount = 1;
                            else
                                entrie.nodecount = ((CDirectory)ixthisone).get_total_children_including_self(); //not right in locking
                        }

                        if (populate_bkupflag && ixthisone.gettype() == FileAttributes.Normal)
                        {
                            entrie.backupoffset = ((CFile)ixthisone).get_ibflag();
                        }
                        DEFS.DEBUG("TLOCK", "Entering get_inodeinfo_tlock (" + absoluteInodePath + ") caller = " + caller + " :: " + entrie.name  +
                                " size = " + entrie.size);

                        return entrie;
                    }
                    finally
                    {
                        DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                             m_state + " in func get_inodeinfo_tlock(2)");
                        _dirOpLock.ExitReadLock();
                    }
                }
                else
                {
                    DEFS.DEBUG("safd", result[0] + "," + result[1]);
                    _dirOpLock.EnterReadLock();
                    try
                    {
                        CDirectory cdir = get_directory(result[0]);
                        return (cdir != null) ? cdir.get_inodeinfo_tlock(result[1], "recur", getcount, populate_bkupflag) : null;
                    }
                    finally
                    {
                        DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                            m_state + " in func get_inodeinfo_tlock(3)");
                        _dirOpLock.ExitReadLock();
                    }
                }
            }            
        }

        public Inode_Info[] find_files_tlock(string absoluteDirectoryPath)
        {
            DEFS.DEBUG("TLOCK", "Entering find_files_tlock(" + absoluteDirectoryPath + ")");
            if (is_root_path(absoluteDirectoryPath))
            {
                _dirOpLock.EnterReadLock();
                try
                {
                    return find_files();
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func find_files_tlock()");
                    _dirOpLock.ExitReadLock();
                }
            }
            else
            {
                DEFS.ASSERT(is_root_path(absoluteDirectoryPath) == false, "Cannot be root path in find_files_tlock, " + absoluteDirectoryPath);
                string[] result = resolve_pathstring(absoluteDirectoryPath);

                if (result[0] == null)
                {
                    _dirOpLock.EnterReadLock();
                    try
                    {
                        CDirectory cdir = get_directory(result[1]);
                        return (cdir != null) ? cdir.find_files() : null;
                    }
                    finally
                    {
                        DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func find_files_tlock(2)");
                        _dirOpLock.ExitReadLock();
                    }
                }
                else
                {
                    _dirOpLock.EnterReadLock();
                    try
                    {
                        CDirectory cdir = get_directory(result[0]);
                        return (cdir != null) ? cdir.find_files_tlock(result[1]) : null;
                    }
                    finally
                    {
                        DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                            m_state + " in func find_files_tlock(3)");
                        _dirOpLock.ExitReadLock();
                    }
                }
            }
        }//find_files_tlock

        /*
         * This api will take care of opening the file, writing to it etc.
         */
        public bool read_file_tlock(string absoluteFilePath, Byte[] buffer, ref uint readBytes,
            long offset, DokanFileInfo info)
        {
            if (is_root_path(absoluteFilePath))
            {
                return false;
            }

            DEFS.ASSERT(is_root_path(absoluteFilePath) == false, "Cannot be root path in write_file," + absoluteFilePath);
            string[] result = resolve_pathstring(absoluteFilePath);

            if (result[0] == null)
            {
                _dirOpLock.EnterReadLock();
                try
                {
                    CInode cix = inode_exists(result[1]);
                    if (cix != null && cix.gettype() == FileAttributes.Normal && ((CFile)cix).open_file(false))
                    {
                        CFile ix = (CFile)cix;
                        ix.open_file(false);
                        ix.set_dokanio_flag2(true);
                        bool ret = ix.read(buffer, 0, buffer.Length, offset);
                        readBytes = (uint)(((offset + buffer.Length) > ix.m_size) ? (ix.m_size - offset) : (buffer.Length));
                        ix.set_dokanio_flag2(false);
                        return true;
                    }
                    else return false;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func get_cfile_tlock(1)");
                    _dirOpLock.ExitReadLock();
                }
            }
            else
            {
                _dirOpLock.EnterReadLock();
                try
                {
                    CDirectory cdir = get_directory(result[0]);
                    return (cdir != null) ? cdir.read_file_tlock(result[1], buffer, ref readBytes, offset, info) : false;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func get_cfile_tlock(2)");
                    _dirOpLock.ExitReadLock();
                }
            }            
        }

        /*
         * This api will take care of opening the file, writing to it etc.
         */
        public bool write_file_tlock(string absoluteFilePath, Byte[] buffer, ref uint writtenBytes,
            long offset, DokanFileInfo info)
        {
            if (is_root_path(absoluteFilePath))
            {
                return false;
            }

            DEFS.ASSERT(is_root_path(absoluteFilePath) == false, "Cannot be root path in write_file," + absoluteFilePath);
            string[] result = resolve_pathstring(absoluteFilePath);

            if (result[0] == null)
            {
                _dirOpLock.EnterReadLock();
                
                try
                {
                    CInode cix = inode_exists(result[1]);
                    if (cix != null && cix.gettype() == FileAttributes.Normal && ((CFile)cix).open_file(false))
                    {
                        CFile ix = (CFile)cix;
                        ix.set_dokanio_flag2(true);
                        FILE_IO_CODE ecode = ix.write(buffer, 0, buffer.Length, offset);
                        ix.set_dokanio_flag2(false);
                        if (ecode == FILE_IO_CODE.OKAY)
                        {
                            if ((offset + buffer.Length) > ix.m_size) ix.m_size = (offset + buffer.Length);
                            writtenBytes = (uint)buffer.Length;
                            set_dirty();
                            return true;
                        }
                        DEFS.DEBUG("as", "Encountered error in write = " + ecode);
                    }
                   return false;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func get_cfile_tlock(1)");
                    _dirOpLock.ExitReadLock();
                }
            }
            else
            {
                _dirOpLock.EnterReadLock();
                try
                {
                    CDirectory cdir = get_directory(result[0]);
                    return (cdir != null) ? cdir.write_file_tlock(result[1], buffer, ref writtenBytes, offset, info) : false;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func get_cfile_tlock(2)");
                    _dirOpLock.ExitReadLock();
                }
            }
        }

        /*
         * If the path represents a directory, then return null.
         */ 
        public CFile get_cfile_tlock(string absoluteFilePath)
        {
            if (is_root_path(absoluteFilePath))
            {
                return null;
            }

            DEFS.ASSERT(is_root_path(absoluteFilePath) == false, "Cannot be root path in get_cfile_tlock," + absoluteFilePath);
            string[] result = resolve_pathstring(absoluteFilePath);

            if (result[0] == null)
            {
                _dirOpLock.EnterReadLock();
                try
                {
                    CInode ci = inode_exists(result[1]);
                    if (ci != null && ci.gettype() == FileAttributes.Normal)
                    {
                        ((CFile)ci).set_dokanio_flag2(true);
                        return (CFile)ci;
                    }
                    else return null;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func get_cfile_tlock(1)");
                    _dirOpLock.ExitReadLock();
                }
            }
            else
            {
                _dirOpLock.EnterReadLock();
                try
                {
                    CDirectory cdir = get_directory(result[0]);
                    return (cdir != null) ? cdir.get_cfile_tlock(result[1]) : null;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func get_cfile_tlock(2)");
                    _dirOpLock.ExitReadLock();
                }
            }
        }

        public bool insert_clonefile_tlock(string destfolder, CFile newfile)
        {
            DEFS.DEBUG("TLOCK", "Entering insert_clonefile_tlock(" + destfolder + ")");

            if (is_root_path(destfolder))
            {
                _dirOpLock.EnterWriteLock();
                try
                {
                    return insert_new_file_cloned(newfile);
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                            m_state + " in func insert_clonefile_tlock(1)");
                    _dirOpLock.ExitWriteLock();
                }
            }
            else
            {
                DEFS.ASSERT(is_root_path(destfolder) == false, "Cannot be root path in open_directory_tlock," + destfolder);
                string[] result = resolve_pathstring(destfolder);

                if (result[0] == null)
                {
                    _dirOpLock.EnterWriteLock();
                    try
                    {
                        CDirectory cdir = get_directory(result[1]);
                        return (cdir != null) ? cdir.insert_new_file_cloned(newfile) : false;
                    }
                    finally
                    {
                        DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                            m_state + " in func insert_clonefile_tlock(2)");
                        _dirOpLock.ExitWriteLock();
                    }
                }
                else
                {
                    _dirOpLock.EnterReadLock();
                    try
                    {
                        CDirectory cdir = get_directory(result[0]);
                        return (cdir != null) ? cdir.insert_clonefile_tlock(result[1], newfile) : false;
                    }
                    finally
                    {
                        DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                                m_state + " in func insert_clonefile_tlock(3)");
                        _dirOpLock.ExitReadLock();
                    }
                }
            }
        }

        /*
        * If the path represents a directory, then return null. Or else dup the file and
         * return, remember that if the caller cannot insert it in the destination then
         * he must free it correctly.
        */
        public CFile clone_file_tlock(string absoluteFilePath, int newfsid, int newino, int newpino, string newpath)
        {
            if (is_root_path(absoluteFilePath))
            {
                return null;
            }

            DEFS.ASSERT(is_root_path(absoluteFilePath) == false, "Cannot be root path in get_cfile_tlock," + absoluteFilePath);
            string[] result = resolve_pathstring(absoluteFilePath);

            if (result[0] == null)
            {
                _dirOpLock.EnterReadLock();
                try
                {
                    CInode ci = inode_exists(result[1]);
                    if (ci != null && ci.gettype() == FileAttributes.Normal)
                    {
                        CFile newfile = ((CFile)ci).dup_file(newfsid, newino, newpino, newpath);
                        return newfile;
                    }
                    else return null;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func clone_file_tlock(1)");
                    _dirOpLock.ExitReadLock();
                }
            }
            else
            {
                _dirOpLock.EnterReadLock();
                try
                {
                    CDirectory cdir = get_directory(result[0]);
                    return (cdir != null) ? cdir.clone_file_tlock(result[1], newfsid, newino,newpino, newpath) : null;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func clone_file_tlock(2)");
                    _dirOpLock.ExitReadLock();
                }
            }
        }

        public CDirectory clone_directory_tlock(string absoluteFilePath, int newfsid, List<int> inolist, int newpino, string newpath)
        {
            if (is_root_path(absoluteFilePath))
            {
                return null;
            }

            DEFS.ASSERT(is_root_path(absoluteFilePath) == false, "Cannot be root path in clone_directory_tlock," + absoluteFilePath);
            string[] result = resolve_pathstring(absoluteFilePath);

            if (result[0] == null)
            {
                _dirOpLock.EnterWriteLock();
                try
                {
                    CInode ci = inode_exists(result[1]);
                    if (ci != null && ci.gettype() == FileAttributes.Directory)
                    {
                        CDirectory newdir = ((CDirectory)ci).dup_directory(newfsid, inolist, newpino, newpath);
                        return newdir;
                    }
                    else return null;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func clone_directory_tlock(1)");
                    _dirOpLock.ExitWriteLock();
                }
            }
            else
            {
                _dirOpLock.EnterReadLock();
                try
                {
                    CDirectory cdir = get_directory(result[0]);
                    return (cdir != null) ? cdir.clone_directory_tlock(result[1], newfsid, inolist, newpino, newpath) : null;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func clone_directory_tlock(2)");
                    _dirOpLock.ExitReadLock();
                }
            }
        }

        public bool insert_clonedirectory_tlock(string destfolder, CDirectory newdir)
        {
            DEFS.DEBUG("TLOCK", "Entering insert_clonedirectory_tlock(" + destfolder + ")");

            if (is_root_path(destfolder))
            {
                _dirOpLock.EnterWriteLock();
                try
                {
                    return insert_new_directory_cloned(newdir);
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                            m_state + " in func insert_clonedirectory_tlock(1)");
                    _dirOpLock.ExitWriteLock();
                }
            }
            else
            {
                DEFS.ASSERT(is_root_path(destfolder) == false, "Cannot be root path in open_directory_tlock," + destfolder);
                string[] result = resolve_pathstring(destfolder);

                if (result[0] == null)
                {
                    _dirOpLock.EnterWriteLock();
                    try
                    {
                        CDirectory cdir = get_directory(result[1]);
                        return (cdir != null) ? cdir.insert_new_directory_cloned(newdir) : false;
                    }
                    finally
                    {
                        DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                            m_state + " in func insert_clonedirectory_tlock(2)");
                        _dirOpLock.ExitWriteLock();
                    }
                }
                else
                {
                    _dirOpLock.EnterReadLock();
                    try
                    {
                        CDirectory cdir = get_directory(result[0]);
                        return (cdir != null) ? cdir.insert_clonedirectory_tlock(result[1], newdir) : false;
                    }
                    finally
                    {
                        DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                                m_state + " in func insert_clonedirectory_tlock(3)");
                        _dirOpLock.ExitReadLock();
                    }
                }
            }
        }

        public int set_eof_tlock(string absoluteFilePath, long size)
        {
            if (is_root_path(absoluteFilePath))
            {
                return -1;
            }

            DEFS.ASSERT(is_root_path(absoluteFilePath) == false, "Cannot be root path in set_eof_tlock," + absoluteFilePath);
            string[] result = resolve_pathstring(absoluteFilePath);

            if (result[0] == null)
            {
                _dirOpLock.EnterWriteLock();
                try
                {
                    CInode ci = inode_exists(result[1]);
                    if (ci != null && ci.gettype() == FileAttributes.Normal)
                    {
                        CFile ix = ((CFile)ci);
                        ix.open_file(false);
                        ix.set_dokanio_flag2(false);
                        int res = ix.set_eof(size);
                        ix.set_dokanio_flag2(false);
                        set_dirty();
                        return res;
                    }
                    else return -1;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func set_eof_tlock(1)");
                    _dirOpLock.ExitWriteLock();
                }
            }
            else
            {
                _dirOpLock.EnterReadLock();
                try
                {
                    CDirectory cdir = get_directory(result[0]);
                    return (cdir != null) ? cdir.set_eof_tlock(result[1], size) : -1;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func set_eof_tlock(2)");
                    _dirOpLock.ExitReadLock();
                }
            }            
        }

        public int close_file_tlock(string absoluteFilePath)
        {
            if (is_root_path(absoluteFilePath))
            {
                return -1;
            }

            DEFS.ASSERT(is_root_path(absoluteFilePath) == false, "Cannot be root path in close_file_tlock," + absoluteFilePath);
            string[] result = resolve_pathstring(absoluteFilePath);

            if (result[0] == null)
            {
                _dirOpLock.EnterWriteLock();
                try
                {
                    CInode ci = inode_exists(result[1]);
                    if (ci != null && ci.gettype() == FileAttributes.Normal)
                    {
                        CFile ix = ((CFile)ci);
                        ix.sync();
                        ix.set_dokanio_flag2(false);
                        ((CInode)ix).unmount(true);
                        set_dirty();
                        return 0;
                    }
                    else return -1;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func close_file_tlock(1)");
                    _dirOpLock.ExitWriteLock();
                }
            }
            else
            {
                _dirOpLock.EnterReadLock();
                try
                {
                    CDirectory cdir = get_directory(result[0]);
                    return (cdir != null) ? cdir.close_file_tlock(result[1]) : -1;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func close_file_tlock(2)");
                    _dirOpLock.ExitReadLock();
                }
            }                        
        }

        public bool do_dedupe_tlock(string absoluteFilePath, int sourcefbn, int currdbn, int donordbn)
        {
            if (is_root_path(absoluteFilePath))
            {
                return false;
            }

            DEFS.ASSERT(is_root_path(absoluteFilePath) == false, "Cannot be root path in do_dedupe_tlock," + absoluteFilePath);
            string[] result = resolve_pathstring(absoluteFilePath);

            if (result[0] == null)
            {
                _dirOpLock.EnterWriteLock();
                try
                {
                    CInode ci = inode_exists(result[1]);
                    if (ci != null && ci.gettype() == FileAttributes.Normal)
                    {
                        CFile ix = ((CFile)ci);
                        ix.open_file(false);
                        ix.set_dokanio_flag2(true);
                        ix.dodedupe(sourcefbn, currdbn, donordbn);
                        ix.set_dokanio_flag2(false);
                        return true;
                    }
                    else return false;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func do_dedupe_tlock(1)");
                    _dirOpLock.ExitWriteLock();
                }
            }
            else
            {
                _dirOpLock.EnterReadLock();
                try
                {
                    CDirectory cdir = get_directory(result[0]);
                    return (cdir != null) ? cdir.do_dedupe_tlock(result[1], sourcefbn, currdbn, donordbn) : false;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func do_dedupe_tlock(2)");
                    _dirOpLock.ExitReadLock();
                }
            }               
        }

        public bool do_dedupe_tlock_batch(string absoluteFilePath, fingerprintDMSG[] msglist)
        {
            if (is_root_path(absoluteFilePath))
            {
                return false;
            }

            DEFS.ASSERT(is_root_path(absoluteFilePath) == false, "Cannot be root path in do_dedupe_tlock_batch," + absoluteFilePath);
            string[] result = resolve_pathstring(absoluteFilePath);

            if (result[0] == null)
            {
                _dirOpLock.EnterWriteLock();
                try
                {
                    CInode ci = inode_exists(result[1]);
                    if (ci != null && ci.gettype() == FileAttributes.Normal)
                    {
                        CFile ix = ((CFile)ci);
                        ix.open_file(false);
                        ix.set_dokanio_flag2(true);
                        for (int i = 0; i < 1024; i++) {
                            if (msglist[i] != null) {
                                ix.dodedupe(msglist[i].fbn, msglist[i].sourcedbn, msglist[i].destinationdbn);
                            }
                        }
                        //ix.dodedupe(sourcefbn, currdbn, donordbn);
                        ix.set_dokanio_flag2(false);
                        return true;
                    }
                    else return false;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func do_dedupe_tlock_batch(1)");
                    _dirOpLock.ExitWriteLock();
                }
            }
            else
            {
                _dirOpLock.EnterReadLock();
                try
                {
                    CDirectory cdir = get_directory(result[0]);
                    return (cdir != null) ? cdir.do_dedupe_tlock_batch(result[1], msglist) : false;
                }
                finally
                {
                    DEFS.ASSERT(m_state == DIR_STATE.DIR_CONTENTS_INCORE || m_state == DIR_STATE.DIRFILE_DIRTY, "Incorrect state " +
                        m_state + " in func do_dedupe_tlock_batch(2)");
                    _dirOpLock.ExitReadLock();
                }
            }
        }
    }
}
