using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace redfs_v2
{
    public class CFile : CInode
    {
        /*
         * Always examine this inside a lock
         */ 
        public FILE_STATE m_state = FILE_STATE.FILE_DEFAULT;

        public long m_size;
        private int m_associated_fsid = 0; //incore only

        private long m_atime;
        private long m_ctime;
        private long m_mtime;

        int m_inode;
        int m_parent_inode;

        string m_name;
        string m_path;
        long creation_time;
        int mTimeToLive = 100;

        public CDirectory m_parent = null;

        /*
         * Below 4 functions are for CInode interface.
         */
        RedFS_Inode _mywip;

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
            if (name.Equals(m_name)) return true;
            return false;
        }

        string CInode.get_string_rep()
        {
            return (0 + "," + m_inode + "," + m_size + "," + m_atime + "," +
                        m_ctime + "," + m_mtime + "," + m_name);
        }

        string CInode.get_full_path() { return m_path; }
        FileAttributes CInode.gettype() { return FileAttributes.Normal; }
        string CInode.get_iname() { return m_name; }
        int CInode.get_inode_number() { return m_inode; }
        int CInode.get_parent_inode_number() { return m_parent_inode; }

        public void rename_file(string newname)
        {
            m_name = newname;
            if (m_parent != null) //only null when a file is cloned and does not have a parent.
            {
                m_parent.set_dirty();
            }
        }

        void CInode.unmount(bool inshutdown)
        {
            long curr = DateTime.Now.ToUniversalTime().Ticks;
            int seconds = (int)((curr - creation_time) / 10000000);

            DEFS.DEBUG("UNMOUNT", "CFile (" + m_inode + ") umnount : " + m_name + 
                " inshutdown flag = " + inshutdown + " is _mywip null = " + (_mywip == null) +
                " secs = " + seconds);

            if (inshutdown == false && timeoutcheck() == false && m_state == FILE_STATE.FILE_IN_DOKAN_IO)
            {
                return;
            }

            /*
             * We cannot unmount a dirty wip directly, it must first be cleaned, so we
             * dont do this here. The next sync iteration will clean the wip, and then
             * we are good to unmount. If we are being shutdown, then we sync() here itself.
             */
            if ((inshutdown == false) && ((_mywip == null)))// || _mywip.is_dirty == false)) 
            {
                DEFS.ASSERT(m_state != FILE_STATE.FILE_IN_DOKAN_IO, "Cannot be dokan io when _mywip = NULL");
                return;
            }

            /*
             * _mywip is not null and dirty, or we are shutting down.
             */
            lock (this)
            {
                DEFS.ASSERT(m_state != FILE_STATE.FILE_ORPHANED, "We are in sync path can cannot have an orphaned file");
                if (_mywip != null)
                {
                    REDDY.ptrRedFS.sync(_mywip);
                    REDDY.ptrRedFS.flush_cache(_mywip, inshutdown);
                }
                lock (REDDY.FSIDList[m_associated_fsid])
                {
                    if (_mywip != null)
                    {
                        RedFS_Inode inowipX = REDDY.FSIDList[m_associated_fsid].get_inode_file_wip("Umount file iwp:" + m_name);
                        OPS.Checkin_Wip(inowipX, _mywip, m_inode);
                        DEFS.ASSERT(m_state != FILE_STATE.FILE_DELETED, "Wrong state detected222!");
                        REDDY.FSIDList[m_associated_fsid].sync_internal();
                        REDDY.FSIDList[m_associated_fsid].set_dirty(true);
                        _mywip = null;
                    }
                    m_state = FILE_STATE.FILE_UNMOUNTED;
                }
            }
        }

        bool CInode.is_unmounted()
        {
            lock (this)
            {
                bool ret = (_mywip == null) ? true : false;
                DEFS.ASSERT(!ret || m_state != FILE_STATE.FILE_IN_DOKAN_IO, "Cannot be dokan io when unmounted");
                return ret && m_state == FILE_STATE.FILE_UNMOUNTED;
            }
        }

        public int get_ibflag()
        {
            open_file(false);
            return _mywip.get_ibflag();
        }
        public void set_ibflag(int c)
        {
            open_file(false);
            _mywip.set_ibflag(c);
        }

        /*
         * We dont expect a write to come before opening because, cdirectory would
         * call a open_file() before inserting into the DIR CACHE. We shouldnt call
         * this with cfile-lock held.
         */ 
        public bool open_file(bool justcreated)
        {
            if (m_state == FILE_STATE.FILE_DELETED)
            {
                return false;
            }
            else if (m_state == FILE_STATE.FILE_IN_DOKAN_IO)
            {
                DEFS.ASSERT(_mywip != null, "My wip cannot be null when dokan_io flag is set in open_file");
                return true;
            }

            touch();
            if (_mywip == null)
            {
                lock (this)
                {

                    if (_mywip != null)
                    { 
                        /*
                        * It could be the case that someone already opend it, maybe previous call
                        * that was locked in open_file(), just bail out.
                        */
                        DEFS.ASSERT(m_state != FILE_STATE.FILE_IN_DOKAN_IO, "Suddendly cannot be in dokan io when it was just null");
                        return true;
                    }

                    lock (REDDY.FSIDList[m_associated_fsid])
                    {
                        _mywip = new RedFS_Inode(WIP_TYPE.REGULAR_FILE, m_inode, m_parent_inode);
                        long oldsize = _mywip.get_filesize();

                        RedFS_Inode inowip = REDDY.FSIDList[m_associated_fsid].get_inode_file_wip("OF:" + m_name);
                        DEFS.DEBUG("F(_mywip)", "Loaded ino= " + m_inode + "wip from disk, size = " + _mywip.get_filesize());

                        bool ret = OPS.Checkout_Wip2(inowip, _mywip, m_inode);

                        if (ret)
                        {
                            DEFS.DEBUG("FILE", "Loaded ino= " + m_inode + "wip from disk, size = " + _mywip.get_filesize());
                        }
                        else
                        {
                            DEFS.DEBUG("FILE", "Loaded ino = " + m_inode + " (new) size = " + _mywip.get_filesize());
                            _mywip.set_ino(m_parent_inode, m_inode);
                        }

                        DEFS.ASSERT(m_size == _mywip.get_filesize(), "File size should match, irrespecitive of weather its " +
                            " from disk, (=0) then, or inserted from an existing dir load, >= 0 in that case, msize:" + m_size +
                            " _mywip.size:" + _mywip.get_filesize() + " fname =" + m_name + " ino=" + m_inode + " beforeread size = " +
                            oldsize + " contents : " + _mywip.get_string_rep2() + " ret = " + ret);

                        if (justcreated)
                        {
                            DEFS.ASSERT(ret == false, "This should be a new file " + _mywip.get_filesize() + " fname =" + m_name +
                                            " ino=" + m_inode + " beforeread size = " + oldsize + " contents : " + _mywip.get_string_rep2());
                            _mywip.setfilefsid_on_dirty(m_associated_fsid);
                            _mywip.is_dirty = true; //this must make it to disk.
                        }
                        REDDY.FSIDList[m_associated_fsid].sync_internal();
                        m_state = FILE_STATE.FILE_DEFAULT;
                    }
                }
            }
            return true;
        }

        public bool set_dokanio_flag2(bool flag)
        {
            if (m_state == FILE_STATE.FILE_DELETED)
            {
                return false;
            }

            if (_mywip != null)
            {
                lock (this)
                {
                    if (_mywip == null) return false; //probably unmounted or deleted.
                }

                DEFS.ASSERT(_mywip != null, "_mywip cannot be null when setting dokan io flag");
                if (flag)
                {
                    DEFS.ASSERT(m_state == FILE_STATE.FILE_IN_DOKAN_IO || m_state == FILE_STATE.FILE_DEFAULT, "Incorrect mstate = " + m_state);
                    m_state = FILE_STATE.FILE_IN_DOKAN_IO;
                }
                else
                {
                    if (m_state != FILE_STATE.FILE_UNMOUNTED)
                    {
                        m_state = FILE_STATE.FILE_DEFAULT;
                    }
                }
                return true;
            }
            return false;
        }

        public bool read(byte[] buffer, int bufoffset, int buflen, long fileoffset)
        {
            if (m_state == FILE_STATE.FILE_DELETED)
            {
                return false;
            }
            lock (this)
            {
                if (_mywip == null || (fileoffset >= m_size))
                {
                    OPS.BZERO(buffer);
                    return false;
                }

                long request_end_offset = fileoffset + buflen;
                if (request_end_offset > m_size)
                {
                    int old_buflen = buflen;
                    long true_end_offset = m_size;
                    DEFS.DEBUG("ERROR", "Trying to read beyond EOF = " + m_size + "  (start_offset, end_offset) = " +
                            fileoffset + "," + (fileoffset + buflen));
                    buflen = (int)(true_end_offset - fileoffset);
                    DEFS.ASSERT(old_buflen >= buflen, "Something wrong in calculation");
                    for (int i = (bufoffset + buflen); i < (bufoffset + old_buflen); i++)
                    {
                        buffer[i] = 0;
                    }
                }

                REDDY.ptrRedFS.redfs_read(_mywip, fileoffset, buffer, bufoffset, buflen);

                /*
                * VLC and office apps tries to read beyond EOF, and we end up growing the file, this happens 
                * with filesize blowing up infinitely.
                */
                m_size = _mywip.get_filesize();
                touch();
                return true;
            }
        }

        public FILE_IO_CODE write(byte[] buffer, int bufoffset, int buflen, long fileoffset)
        {
            if (m_state == FILE_STATE.FILE_DELETED)
            {
                return FILE_IO_CODE.ERR_FILE_DELETED;
            }

            lock (this)
            {
                if (_mywip == null)
                {
                    return FILE_IO_CODE.ERR_NULL_WIP;
                }
                touch();

                REDDY.ptrRedFS.redfs_write(_mywip, fileoffset, buffer, bufoffset, buflen);

                m_size = _mywip.get_filesize();
                _mywip.is_dirty = true;

                m_mtime = DateTime.Now.Ticks;
                //m_parent.set_dirty();

                _mywip.setfilefsid_on_dirty(m_associated_fsid);

                touch();
                return FILE_IO_CODE.OKAY;
            }
        }

        public bool dodedupe(int fbn, int currdbn, int donordbn)
        {
            if (m_state == FILE_STATE.FILE_DELETED)
            {
                return false;
            }

            lock (this)
            {
                if (_mywip == null)
                {
                    return false;
                }
                touch();
                DEFS.ASSERT(_mywip != null, "Cannot be null when calling dodedupe");
                touch();

                _mywip.is_dirty = true;
                m_mtime = DateTime.Now.Ticks;
                m_parent.set_dirty();
                _mywip.setfilefsid_on_dirty(REDDY.FSIDList[m_associated_fsid].get_fsid());
                long old_file_size = _mywip.get_filesize();
                REDDY.ptrRedFS.redfs_do_blk_sharing(_mywip, fbn, currdbn, donordbn);
                DEFS.ASSERT(old_file_size == _mywip.get_filesize(), "dedupe failed since size has changed");
                _mywip.setfilefsid_on_dirty(REDDY.FSIDList[m_associated_fsid].get_fsid());

                return true;
            }
        }

        private void touch()
        {
            creation_time = DateTime.Now.ToUniversalTime().Ticks;
        }

        private bool timeoutcheck()
        {
            long curr = DateTime.Now.ToUniversalTime().Ticks;
            int seconds = (int)((curr - creation_time) / 10000000);

            if (seconds > mTimeToLive) return true;
            else return false;
        }
        bool CInode.is_time_for_clearing()
        {
            return timeoutcheck();
        }

        public void sync()
        {
            lock (this)
            {
                if (_mywip != null)
                {
                    lock (REDDY.FSIDList[m_associated_fsid])
                    {
                        RedFS_Inode inowip = REDDY.FSIDList[m_associated_fsid].get_inode_file_wip("GC");

                        DEFS.DEBUG("SYNC", "CFile (" + m_inode + ") -mywip.size = " + _mywip.get_filesize());
                        REDDY.ptrRedFS.sync(_mywip);
                        OPS.Checkin_Wip(inowip, _mywip, m_inode);
                        _mywip.is_dirty = false;

                        REDDY.FSIDList[m_associated_fsid].sync_internal();
                        REDDY.ptrRedFS.redfs_commit_fsid(REDDY.FSIDList[m_associated_fsid]);
                    }
                }
                else
                {
                    DEFS.DEBUG("FSID", "inserted/unsyncd : " + m_name);
                }
            }
        }

        long CInode.get_size()
        {
            return m_size;
        }

        public void remove_ondisk_data2()
        {
            open_file(false);
            touch();

            if (m_state == FILE_STATE.FILE_DELETED) return;
            m_state = FILE_STATE.FILE_DELETED;

            DEFS.ASSERT(_mywip != null, "Cannot be null in remove() after calling open()");


            lock (this)
            {
                lock (REDDY.FSIDList[m_associated_fsid])
                {
                    DEFS.ASSERT(_mywip != null, "Unmount couldnt have worked on this");
                    RedFS_Inode inowip = REDDY.FSIDList[m_associated_fsid].get_inode_file_wip("DF:" + m_name);

                    //REDDY.ptrRedFS.sync(_mywip);
                    REDDY.ptrRedFS.flush_cache(_mywip, false);
                    REDDY.ptrRedFS.redfs_delete_wip(m_associated_fsid, _mywip, true);

                    DEFS.ASSERT(_mywip.get_filesize() == 0, "After delete, all the wip contents must be cleared off");
                    for (int i = 0; i < 16; i++)
                    {
                        DEFS.ASSERT(_mywip.get_child_dbn(i) == DBN.INVALID, "dbns are not set after delete wip " +
                            i + "  " + _mywip.get_child_dbn(i));
                    }
                    OPS.CheckinZerodWipData(inowip, m_inode);
                    REDDY.FSIDList[m_associated_fsid].sync_internal();

                    _mywip = null;
                }
            }
            DEFS.DEBUG("IFSD", "<<<< DELETED FILE >>>> " + m_name);
            
        }

        /*
         * We are opening with certain fsid, this may not be its createdfsid!.
         */ 
        public CFile(int fsid, int ino, int pino, string name, string path)
        {
            m_associated_fsid = fsid;
            m_inode = ino;
            m_parent_inode = pino;
            m_name = name;
            m_path = path;
            touch();

            m_atime = m_ctime = m_mtime = DateTime.Now.ToUniversalTime().Ticks;
            m_size = 0;
            creation_time = DateTime.Now.ToUniversalTime().Ticks;
        }

        public CFile(int fsid, int ino, int pino, string name, string path, long size, long atime, long ctime, long mtime)
        {
            m_associated_fsid = fsid;
            m_inode = ino;
            m_parent_inode = pino;
            m_name = name;
            m_path = path;
            touch();

            m_atime = atime;
            m_ctime = ctime;
            m_mtime = mtime;
            m_size = size;
            creation_time = DateTime.Now.ToUniversalTime().Ticks;
        }

        /*
         * This is way too complex!.
         */ 
        public int set_eof(long length)
        {
            if (m_state == FILE_STATE.FILE_DELETED)
            {
                return -1;
            }

            open_file(false);

            lock (this)
            {
                if (_mywip == null)
                {
                    // || _mywip.get_filesize() == 0) 
                    return -1;
                }

                m_parent.set_dirty();
                if (_mywip.get_filesize() == length)
                {
                    return 0;
                }
                else
                {
                    mTimeToLive = 10;
                    REDDY.ptrRedFS.sync(_mywip);
                    touch();
                    REDDY.ptrRedFS.redfs_resize_wip(_mywip, length, true);
                    touch();
                    m_size = _mywip.get_filesize();
                    _mywip.is_dirty = true;
                    mTimeToLive = 4;
                    return 0;
                }
            }
        }

        /*
         * This file dups itself into another file, the old version is not deleted.
         * More like a new reincarnate without loosing old data. The responsibility
         * of updating path/name is left to CDirectory, as there are only incore data
         * from the POV of CFile.
         */ 
        public CFile dup_file(int newfsid, int inode, int pino, string newpath)
        {
            if (m_state == FILE_STATE.FILE_DELETED)
            {
                return null;
            }
            open_file(false);

            /*
             * It is okay to not lock cfnew since nobody actually know that this is
             * on the system. As nobody else touch this, we can do without locks for the new file.
             */
            lock (this)
            {
                if (_mywip == null)
                {
                    DEFS.ASSERT(m_state == FILE_STATE.FILE_DELETED, "Should be marked deleted, or else some workflow issue");
                    return null;
                }

                REDDY.ptrRedFS.sync(_mywip);
                REDDY.ptrRedFS.flush_cache(_mywip, true);

                //should we write the source inodefile to disk also?
                CFile cfnew = new CFile(newfsid, inode, pino, m_name, newpath);
                cfnew.open_file(true);

                REDDY.ptrRedFS.redfs_dup_file(_mywip, cfnew._mywip);

                cfnew.m_atime = m_atime;
                cfnew.m_ctime = m_ctime;
                cfnew.m_mtime = m_mtime;
                cfnew.m_size = _mywip.get_filesize();
                cfnew.set_ibflag(_mywip.get_ibflag());
                cfnew.touch();

                //int numblocks = OPS.FSIZETONUMBLOCKS(m_clist[i].get_size());
                int numblocks = OPS.NUML0(m_size) + OPS.NUML1(m_size) + OPS.NUML2(m_size);
                REDDY.FSIDList[newfsid].diff_upadate_logical_data(numblocks * 4096); //not the correct figure per-se
                REDDY.FSIDList[newfsid].set_dirty(true);
                DEFS.DEBUGYELLOW("INCRCNT", "Grew fsid usage by  " + numblocks + " (file)");

                cfnew.sync();
                return cfnew;
            }
        }
    }
}
