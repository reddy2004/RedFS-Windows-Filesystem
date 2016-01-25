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
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Windows.Forms;
using Dokan;
using System.Collections;

namespace redfs_v2
{
    public class IFSD_Mux
    {
        private bool m_shutdown;
        private bool m_shutdown_done_gc = false;

        private String ADFN(String path)
        {
            char[] sep = { '\\' };
            String[] list = path.Split(sep, StringSplitOptions.RemoveEmptyEntries);
            return list[list.Length - 1]; //last entry
        }

        private string ExtractParentPath(string fullpath)
        {
            char[] sep = { '\\' };
            String[] list = fullpath.Split(sep, StringSplitOptions.RemoveEmptyEntries);
            if (list.Length == 0)
            {
                return null;
            }
            else if (list.Length == 1)
            {
                return "\\";
            }
            else
            {
                string ret = "";
                for (int i = 0; i < list.Length - 1; i++) {
                    ret += "\\" + list[i];
                }
                return ret;
            }
        }

        /*
        * Scans the given 4k buffer, and returns the offset of a free bit.
        * offset can vary between 0 and 4096*8
        */
        private int get_free_bitoffset(int startsearchoffset, byte[] data)
        {
            DEFS.ASSERT(data.Length == 4096 && startsearchoffset < 4096, "get_free_bitoffset input must be a " +
                            " buffer of size 4096, but passed size = " + data.Length);

            for (int offset = startsearchoffset; offset < data.Length; offset++)
            {
                if (data[offset] != (byte)0xFF)
                {
                    int x = OPS.get_first_free_bit(data[offset]);
                    data[offset] = OPS.set_free_bit(data[offset], x);
                    return (offset * 8 + x);
                }
            }
            return -1;
        }

        /*
         * Give a fsid, it looks into the iMapWip and gets a free bit. The fsid block has the
         * largest inode number that is currently used, and the iMapWip itself. I'm not using anylocks
         * for this wip since this operation will never be concurrent. All FS modification code that
         * may use this path already would have a lock on the rootdir. Ex duping, deleting, inserting etc.
         * 
         * XXX: Note that we are never freeing the inode bit once set!. So basically this is a dummy function.
         * We still work because we can afford to wait for 500M inodes to allocated before we do a wrap around!!.
         */ 
        private int find_free_ino_bit(int fsid)
        {
            int max_fbns = 16384;
            int curr_max_inode = REDDY.FSIDList[fsid].get_start_inonumber();

            byte[] buffer = new byte[4096];

            RedFS_Inode iMapWip = REDDY.FSIDList[fsid].get_inodemap_wip();
            int fbn = OPS.OffsetToFBN(curr_max_inode / 8);

            for (int cfbn = fbn; cfbn < max_fbns; cfbn++)
            {
                OPS.BZERO(buffer);
                REDDY.ptrRedFS.redfs_read(iMapWip, (cfbn * 4096), buffer, 0, 4096);

                int startsearchoffset = ((cfbn == fbn) ? (curr_max_inode / 8) : 0) % 4096; 

                int free_bit = get_free_bitoffset(startsearchoffset, buffer);
                if (free_bit != -1)
                {
                    int free_inode = ((cfbn * (4096 * 8)) + free_bit);
                    REDDY.ptrRedFS.redfs_write(iMapWip, (cfbn * 4096), buffer, 0, 4096);

                    REDDY.ptrRedFS.sync(iMapWip);
                    REDDY.FSIDList[fsid].set_inodemap_wip(iMapWip);
                    REDDY.ptrRedFS.flush_cache(iMapWip, true);

                    REDDY.FSIDList[fsid].set_start_inonumber(free_inode + 1);
                    DEFS.DEBUG("IFSDMux", "Found free ino = " + free_inode + " so setting currmaxino = " + curr_max_inode + " for fsid = " + fsid);
                    REDDY.ptrRedFS.redfs_commit_fsid(REDDY.FSIDList[fsid]);
                    return free_inode;
                }
            }

            REDDY.FSIDList[fsid].set_start_inonumber(64);
            REDDY.ptrRedFS.redfs_commit_fsid(REDDY.FSIDList[fsid]); //do we need this regularly?
            DEFS.DEBUG("FSID", "XXXXX VERY RARE EVENT XXXX INODE WRAP AROUND XXXX");
            return find_free_ino_bit(fsid);
        }

        private void init_if_necessary()
        {
            for (int i = 0; i < 1024; i++) 
            {
                if (REDDY.FSIDList[i] != null) 
                {
                    DEFS.ASSERT(REDDY.FSIDList[i].rootdir == null, "Rootdir must be null");
                    REDDY.FSIDList[i].rootdir = new CDirectory(i, 0, -1, null, null, true);

                    int curr_max_inode = curr_max_inode = REDDY.FSIDList[i].get_start_inonumber();
                    if (curr_max_inode == 0)
                    {
                        curr_max_inode = 64;
                        DEFS.DEBUG("IFSDMux", "Found current max inode (new) = " + curr_max_inode);

                    }
                    else
                    {
                        DEFS.DEBUG("IFSDMux", "Found current max inode = " + curr_max_inode);
                        DEFS.ASSERT(curr_max_inode >= 64, "Inode number can start only from 64");
                    }
                    REDDY.FSIDList[i].set_start_inonumber(curr_max_inode);
                    REDDY.ptrRedFS.redfs_commit_fsid(REDDY.FSIDList[i]);
                }
            }
        }

        /*
         * "Allocates or rather find free inode slots and returns. Bulk inode allocation
         * is necessary for cases like dup_dir where we immediately need large number of free
         * slots.
         */ 
        private List<int> NEXT_N_INODE_NUMBERS(int fsid, int count)
        {
            List<int> ilist = new List<int>(count);
            for (int i = 0; i < count; i++) 
            {
                ilist.Add(NEXT_INODE_NUMBER(fsid));
            }
            return ilist;
        }

        private int NEXT_INODE_NUMBER(int fsid)
        {
            int ino = find_free_ino_bit(fsid);
            DEFS.DEBUG("IFSD", "--> Allocated new wip : " + ino + " fsid = " + fsid);
            return ino;
        }


        public int GetDiskFreeSpace(ref ulong freeBytesAvailable, ref ulong totalBytes,
                ref ulong totalFreeBytes, ref long disksizeData, ref long totalLogicalData)
        {
            int disk_blks = 2097152;
            disksizeData = (long)disk_blks * 4096;

            int free_blocks = disk_blks - REDDY.ptrRedFS.get_total_inuse_blocks();

            long total_fs_bytes = 0;
            for (int i = 0; i < 1024; i++)
            {
                if (REDDY.FSIDList[i] != null)
                {
                    total_fs_bytes += REDDY.FSIDList[i].get_logical_data();
                }
            }
            totalLogicalData = total_fs_bytes;

            ulong display_disksize = (ulong)free_blocks * 4096 + (ulong)total_fs_bytes;

            freeBytesAvailable = (ulong)free_blocks * 4096;
            totalBytes = display_disksize;
            totalFreeBytes = display_disksize;
            return 0;
        }

        public IFSD_Mux()
        {
            DEFS.DEBUG("IFSDMux", "Entering constructor if IFSDMux()");
            init_if_necessary();
            DEFS.DEBUG("IFSDMux", "Leaving constructor if IFSDMux()");        
        }

        public void init()
        {
            Thread oThread = new Thread(new ThreadStart(W_GCThreadMux));
            oThread.Start();        
        }

        public void shut_down()
        {
            DEFS.DEBUG("IFSDMux", "Initiating shutdown call");

            m_shutdown = true;
            while (m_shutdown_done_gc == false)
            {
                System.Threading.Thread.Sleep(100);
            }
            DEFS.DEBUG("IFSDMux", "Finished shutdown call");
        }

        private bool create_directory_internal(int fsid, string path)
        {
            if (REDDY.FSIDList[fsid] != null) 
                return REDDY.FSIDList[fsid].rootdir.create_directory_tlock(NEXT_INODE_NUMBER(fsid), path);
            else
                return false;
        }

        private bool delete_directory_internal(int fsid, string path)
        {
            if (REDDY.FSIDList[fsid] != null)
                return REDDY.FSIDList[fsid].rootdir.delete_directory_tlock(path);
            else
                return false;
        }

        /*
         * Will bring the directory contents incore, does not actually modify
         * any other stuff. But lock is still required if this to be MPSafe.
         */ 
        private bool open_directory_internal(int fsid, string path)
        {
            if (REDDY.FSIDList[fsid] != null)
                return REDDY.FSIDList[fsid].rootdir.open_directory_tlock(path);
            else
                return false;
        }

        /*
         * Will return CFile with dokan_io flag set, which means that it cannot be
         * scavenged out until shutdown or until the user explicitly releases
         * control. If the file is already under dokan io we still return since
         * all operations on the file such as read/write/dudupe etc are seriallized
         * internally.
         */ 
        private CFile get_cfile_internal(int fsid, string path)
        {
            if (REDDY.FSIDList[fsid] != null)
                return REDDY.FSIDList[fsid].rootdir.get_cfile_tlock(path);
            else
                return null;
        }

        private bool set_fileeof_internal(int fsid, string path, long size)
        {
            if (REDDY.FSIDList[fsid] != null)
                return (REDDY.FSIDList[fsid].rootdir.set_eof_tlock(path, size) == -1)? false: true;
            else
                return false;            
        }

        private bool create_file_internal(int fsid, string path)
        {
            if (REDDY.FSIDList[fsid] != null)
                return REDDY.FSIDList[fsid].rootdir.create_file_tlock(NEXT_INODE_NUMBER(fsid), path);
            else
                return false;
        }

        private bool delete_file_internal(int fsid, string path)
        {
            if (REDDY.FSIDList[fsid] != null)
                return REDDY.FSIDList[fsid].rootdir.delele_file_tlock(path);
            else
                return false;
        }

        private bool close_file_internal(int fsid, string path)
        {
            if (REDDY.FSIDList[fsid] != null) 
                return (REDDY.FSIDList[fsid].rootdir.close_file_tlock(path) == -1)? false : true;
            else
                return false;
        }
        private bool rename_inode_internal2(int fsid, String path, String newname)
        {
            if (REDDY.FSIDList[fsid] != null)
                return REDDY.FSIDList[fsid].rootdir.rename_inode_tlock(path, newname);
            else
                return false;
        }

        private Inode_Info inode_exists_internal(int fsid, String path, string caller, bool getcount, bool populate_bkupflag)
        {
            if (REDDY.FSIDList[fsid] != null)
                return REDDY.FSIDList[fsid].rootdir.get_inodeinfo_tlock(path, caller, getcount, populate_bkupflag);
            else
                return null;
        }

        private Inode_Info[] find_files_internal(int fsid, String path)
        {
            if (REDDY.FSIDList[fsid] != null)
                return REDDY.FSIDList[fsid].rootdir.find_files_tlock(path);
            else
                return null;
        }



        /*
        * Below are dokan callback function. Have included here so that
        * we can use locks internally for accsssing _rootdir.
        * midx == mounted idx which is the mounted fsid.
        */
        public int CreateFile(int midx, String filename, FileAccess access, FileShare share,
            FileMode mode, FileOptions options, DokanFileInfo info)
        {
            Inode_Info i = inode_exists_internal(midx, filename, "createfile", false, false);
            if (i != null)
            {
                if (i.isfile == false) info.IsDirectory = true;
                return 0;// -DokanNet.ERROR_ALREADY_EXISTS;
            }

            if (mode == FileMode.Open)
            {
                return -1;
            }

            bool status = create_file_internal(midx, filename);
            REDDY.FSIDList[midx].set_dirty(true);
            return (status)? 0 : -DokanNet.DOKAN_ERROR;
        }

        public int DeleteFile(int midx, String filename, DokanFileInfo info)
        {
            bool cf = delete_file_internal(midx, filename);
            if (cf == true)
            {
                REDDY.FSIDList[midx].set_dirty(true);
                return 0;
            }
            return -DokanNet.ERROR_PATH_NOT_FOUND;
        }

        public int OpenDirectory(int midx, String filename, DokanFileInfo info)
        {
            Inode_Info i = inode_exists_internal(midx, filename,"opendir", false, false);
            if (i != null && i.isfile == false)
            {
                info.IsDirectory = true;
                return 0;
            }
            bool status = open_directory_internal(midx, filename);
            return (status)? 0 : -DokanNet.ERROR_PATH_NOT_FOUND;
        }

        public int CreateDirectory(int midx, String filename, DokanFileInfo info)
        {
            bool retval = create_directory_internal(midx, filename);
            if (retval)
            {   
                if(info != null) info.IsDirectory = true;
                REDDY.FSIDList[midx].set_dirty(true);
            }
            return retval ? 0 : -1;
        }

        public int DeleteDirectory(int midx, String filename, DokanFileInfo info)
        {
            bool retval = delete_directory_internal(midx, filename);
            if (retval == true)
            {
                REDDY.FSIDList[midx].set_dirty(true);
                return 0;
            }
            return -DokanNet.ERROR_PATH_NOT_FOUND;
        }

        public int SetInternalFlag(int fsid, string filename, int key, int value)
        {
            CInode ix = get_cfile_internal(fsid, filename);
            if (ix != null)
            {
                REDDY.FSIDList[fsid].set_dirty(true);
                if (ix.gettype() == FileAttributes.Normal)
                {
                    ((CFile)ix).set_ibflag(value);
                    return 0;
                }
            }
            return -1;        
        }

        public int SetFileTime(int midx, String filename, DateTime ctime,
                DateTime atime, DateTime mtime, DokanFileInfo info)
        {
            CInode ix = get_cfile_internal(midx, filename);
            if (ix != null)
            {
                REDDY.FSIDList[midx].set_dirty(true);
                ix.set_acm_times(atime, ctime, mtime);
                if (ix.gettype() == FileAttributes.Normal)
                {
                    ((CFile)ix).set_dokanio_flag2(false);
                }
                else
                {
                    info.IsDirectory = true;
                }
                return 0;
            }
            return -1;
        }

        public int SetEndOfFile(int midx, String filename, long length, DokanFileInfo info)
        {
            if (set_fileeof_internal(midx, filename, length) == false)
            {
                return -1;
            }
            REDDY.FSIDList[midx].set_dirty(true);
            return 0;
        }

        public bool RenameInode2a(int midx, String filename, String newname)
        {
            REDDY.FSIDList[midx].set_dirty(true);
            return rename_inode_internal2(midx, filename, newname); 
        }

        public int ReadFile(int midx, String filename, Byte[] buffer, ref uint readBytes,
            long offset, DokanFileInfo info)
        {
            if (REDDY.FSIDList[midx].rootdir.read_file_tlock(filename, buffer, ref readBytes, offset, info))
            {
                return 0;
            }
            else
            {
                return -1;
            }
        }

        public int WriteFile(int midx, String filename, Byte[] buffer,
            ref uint writtenBytes, long offset, DokanFileInfo info)
        {
            if (REDDY.FSIDList[midx].rootdir.write_file_tlock(filename, buffer, ref writtenBytes, offset, info))
            {
                REDDY.FSIDList[midx].set_dirty(true);
                return 0;
            }
            else
            {
                return -1;
            }
        }

        /*
         * This is called by the internal browser, wanted to avoid creating ArrayList etc
         * as seen in the Dokan interface.
         */ 
        public Inode_Info[] FindFilesInternalAPI(int midx, String folderpath)
        {
            Inode_Info[] entries = find_files_internal(midx, folderpath);
            return entries;
        }

        public int FindFiles(int midx, String filename, ArrayList files, DokanFileInfo info)
        {
            Inode_Info[] entries = find_files_internal(midx, filename);

            if (entries == null)
            {
                return -1;
            }

            foreach (Inode_Info f in entries)
            {
                FileInformation fi = new FileInformation();
                fi.FileName = f.name;
                fi.CreationTime = f.CreationTime;
                fi.LastAccessTime = f.LastAccessTime;
                fi.LastWriteTime = f.LastWriteTime;
                fi.Length = (f.isfile == true) ? f.size : 0;
                fi.Attributes = f.fa;
                files.Add(fi);
            }

            return 0;
        }

        public int FlushFileBuffers(int midx, String filename, DokanFileInfo info)
        {
            CFile ix = get_cfile_internal(midx, filename);
            if (ix == null) return -DokanNet.ERROR_PATH_NOT_FOUND;

            ix.open_file(false);
            ix.sync();
            ix.set_dokanio_flag2(false);
            REDDY.FSIDList[midx].set_dirty(true);
            return 0;
        }

        public int Cleanup(int midx, String filename, DokanFileInfo info)
        {
            return CloseFile(midx, filename, info);
        }

        public int CloseFile(int midx, String filename, DokanFileInfo info)
        {
            if (close_file_internal(midx, filename))
            {
                REDDY.FSIDList[midx].set_dirty(true);
                return 0;
            }
            return -1;
        }

        public Inode_Info GetFileInfoInternalAPI(int fsid, string filename)
        {
            Inode_Info i = inode_exists_internal(fsid, filename, "getfinfo", false, true);
            return i;
        }

        public int GetFileInformation(int midx, String filename, FileInformation fileinfo, DokanFileInfo info)
        {
            Inode_Info i = inode_exists_internal(midx, filename,"getfinfo", false, false);

            if (i != null)
            {
                fileinfo.Attributes = i.fa;
                fileinfo.FileName = i.name;
                fileinfo.CreationTime = i.CreationTime;
                fileinfo.LastAccessTime = i.LastAccessTime;
                fileinfo.LastWriteTime = i.LastWriteTime;
                fileinfo.Length = (i.isfile == true) ? i.size : 0;
                info.IsDirectory = (i.isfile == true) ? false : true;
                return 0;
            }
            else
            {
                return -DokanNet.ERROR_PATH_NOT_FOUND;
            }
        }

        /*
         * The below functions will be used for dedupe/user command prompt etc.
         */
        private int LoadWip_FindPINO(int fsid, int ino, ref WIP_TYPE type)
        {
            lock (REDDY.FSIDList[fsid])
            {
                RedFS_Inode inowip = REDDY.FSIDList[fsid].get_inode_file_wip("Loadinode");
                lock (inowip)
                {
                    RedFS_Inode mywip = new RedFS_Inode(WIP_TYPE.UNDEFINED, ino, -1);

                    bool ret = OPS.Checkout_Wip2(inowip, mywip, ino);
                    REDDY.FSIDList[fsid].sync_internal();
                    type = mywip.get_wiptype();
                    //DEFS.DEBUG("LdIno", "Loaded ino= " + ino + "wip from disk, type = " + type);
                    //DEFS.DEBUG("LdIno", mywip.get_string_rep2());
                    return (ret) ? mywip.get_parent_ino() : -1;
                }
            }
        }

        //Check if incore, otherwise do full reverse lookup. ino could be either dir/file.
        //XXX order of locking is important, 
        //first lock rootdir to modify incore ifsd, the lock FSIDList[] so that inofile is safe
        //Never do the other way round.
        private string Load_Dirtree_Internal(int fsid, int[] list, int length, ref FileAttributes fa)
        {
            string retval = "";
            for (int i = length - 1; i >= 0; i--) 
            {
                CInode ci = REDDY.FSIDList[fsid].rootdir.check_if_inode_incore(list[i], true);

                //DEFS.ASSERT(ci != null, "check_if_inode_incore cannot return null here, ino = " + list[i]);
                //DEFS.ASSERT(ci.gettype() == FileAttributes.Directory || (i == 0), "we should have reached the end and this must be a file, i=" + i);
                //Console.WriteLine("---->" + ci.get_full_path());
                if (ci != null && ci.gettype() == FileAttributes.Directory)
                {
                    //DEFS.DEBUGYELLOW("X", "Calling opendirectory for " + ci.get_full_path());
                    //((CDirectory)ci).open_directory();
                }
                if (i != (length -1)) //ignore first folder
                    retval += "\\" + ci.get_iname();
                if (i == 0) 
                {
                    fa = ci.gettype();
                }
            }
            return retval;
        }

        /*
         * Returns the path of the inode, from here on always use the path
         * to access the inode.
         */ 
        public string Load_Inode(int fsid, int ino, ref FileAttributes fa)
        {
            int[] orderlist = new int[1024];
            int length = 0;

            lock (REDDY.FSIDList[fsid].rootdir)
            {
                //Prepare the path, one by one.
                int parent = -1;
                int curr_ino = ino;
                do
                {
                    //till you find a incore directory or you reach the root node.
                    CInode cix = REDDY.FSIDList[fsid].rootdir.check_if_inode_incore(curr_ino, false);
                    if (cix != null || curr_ino == 0)
                    {
                        orderlist[length++] = curr_ino;
                        string computedpath = Load_Dirtree_Internal(fsid, orderlist, length, ref fa);
                        return (curr_ino == 0)? computedpath : cix.get_full_path() + computedpath;
                    }
                    else
                    {
                        WIP_TYPE type = WIP_TYPE.UNDEFINED;
                        parent = LoadWip_FindPINO(fsid, curr_ino, ref type);
                        //DEFS.DEBUGYELLOW("@", "(ino,pino,type) = (" + curr_ino + "," + parent + "," + type + ")");
                        DEFS.ASSERT(curr_ino == ino || type == WIP_TYPE.DIRECTORY_FILE, "Mismatch in load ino " + curr_ino + "," + ino);
                        if (parent == -1) return null; //inode does not exist.
                        else orderlist[length++] = curr_ino;

                        //move to parent now.
                        curr_ino = parent;
                    }
                } while (length < 1024);
            }
            return null;
        }

        //
        //Prepare a list of fsid's where this inode could've gone, note that the
        //inode may/maynot be created in the given fsid. we look at the inheritance
        //tree to prepare the list. this is required for dedupe.
        //
        private int[] prepare_Inheritance_list(int fsid, int inode)
        {
            bool[] evaluated = new bool[1024];
            int[] knowndecendants = new int[1024];

            knowndecendants[0] = fsid;
            int childcount = 1;
            evaluated[fsid] = true;

            while (true)
            {
                bool found = false;
                for (int i = 0; i < 1024; i++) 
                {
                    if (REDDY.FSIDList[i] != null && evaluated[i] == false)
                    {
                        //check if this guys parent is among our known decendants.
                        bool ancestorpresent = false;
                        for (int j=0;j<childcount;j++) 
                        {
                            if (knowndecendants[j] == REDDY.FSIDList[i].get_parent_fsid())
                            {
                                ancestorpresent = true;
                                break;
                            }
                        }

                        if (ancestorpresent) 
                        {
                            knowndecendants[childcount++] =  i;
                            evaluated[i] = true;
                            found = true;
                        }
                    }
                }

                if (found == false) 
                    break;
            }

            int[] retval = new int[childcount];
            for (int i = 0; i < childcount; i++) { retval[i] = knowndecendants[i]; }
            //get_created_and_mod_fsids()
            return retval;
        }

        public bool DoDedupeBatch(fingerprintDMSG[] fplist)
        {
            int fsid = -1;
            int inode = -1;
            for (int i = 0; i < 1024; i++) 
            {
                if (fplist[i] != null) 
                {
                    //if ((fsid != -1 || fsid != fplist[i].fsid) ||
                    //    (inode != -1 || inode != fplist[i].inode))
                    //{
                    //    DEFS.DEBUG("BATCH", "Error detected in fplist");
                    //    return false;
                    //}
                    fsid = fplist[i].fsid;
                    inode = fplist[i].inode;
                }
            }

            int[] list = prepare_Inheritance_list(fsid, inode);

            //check fsid's
            for (int i = 0; i < list.Length; i++)
            {
                FileAttributes fa = FileAttributes.NotContentIndexed;
                string rpath = Load_Inode(list[i], inode, ref fa);
                if (rpath == null) continue;

                DEFS.ASSERT(fa == FileAttributes.Normal, "Only normal files can be deduped 2");
                REDDY.FSIDList[list[i]].rootdir.do_dedupe_tlock_batch(rpath, fplist);
            }

            return true;
        }

        //Will do dedupe starting from fsid, for all the derivated children by looking up the
        //tree info. inode is always a regular file.
        public bool DoDedupe(int fsid, int inode, int fbn, int currdbn, int donordbn)
        {
            int[] list = prepare_Inheritance_list(fsid, inode);

            //check fsid's
            for (int i = 0; i < list.Length; i++)
            {
                FileAttributes fa = FileAttributes.NotContentIndexed;

                string rpath = Load_Inode(list[i], inode, ref fa);
                if (rpath == null) continue;

                DEFS.ASSERT(fa == FileAttributes.Normal, "Only normal files can be deduped");
                //DEFS.DEBUGYELLOW("-DEDUP-", "[" + list[i] + "] rpath = " + rpath + "fbn = " + fbn + "(" + currdbn + "->" + donordbn + ")");

                REDDY.FSIDList[list[i]].rootdir.do_dedupe_tlock(rpath, fbn, currdbn, donordbn);
            }
            return false;
        }

        private void do_fsid_sync_internal(int id)
        {
            lock (REDDY.FSIDList[id])
            {
                RedFS_Inode inowip = REDDY.FSIDList[id].get_inode_file_wip("GC1");
                REDDY.ptrRedFS.sync(inowip);
                REDDY.ptrRedFS.flush_cache(inowip, true);
                REDDY.FSIDList[id].sync_internal();
                REDDY.ptrRedFS.redfs_commit_fsid(REDDY.FSIDList[id]);
            }

            DEFS.DEBUG("FSIDSYNC", " Calling sync");
            REDDY.FSIDList[id].rootdir.sync();
            DEFS.DEBUG("FSIDSYNC", "Finished sync, calling gc");
            REDDY.FSIDList[id].rootdir.gc();
            DEFS.DEBUG("FSIDSYNC", " Finished gc");

            if (m_shutdown == true)
            {
                ((CInode)REDDY.FSIDList[id].rootdir).unmount(true);
            }

            lock (REDDY.FSIDList[id])
            {
                RedFS_Inode inowip = REDDY.FSIDList[id].get_inode_file_wip("GC2");
                REDDY.ptrRedFS.sync(inowip);
                REDDY.ptrRedFS.flush_cache(inowip, true);
                REDDY.FSIDList[id].sync_internal();
                REDDY.ptrRedFS.redfs_commit_fsid(REDDY.FSIDList[id]);
            }
        }

        /*
         * Long running threads, does GC every 5 seconds approx.
         */ 
        private void W_GCThreadMux()
        {
            DEFS.DEBUG("IFSDMux", "Starting gc/sync thread (Mux)...");
            int next_wait = 5000;
            
            while (true)
            {
                bool shutdownloop = false;
                TimingCounter tctr = new TimingCounter();
                tctr.start_counter();

                if (m_shutdown)
                {
                    shutdownloop = true;
                }
                else
                {
                    Thread.Sleep(next_wait);
                }

                for (int i = 0; i < 1024; i++) 
                {
                    if (REDDY.FSIDList[i] == null || REDDY.FSIDList[i].get_dirty_flag() == false)
                    {
                        continue;
                    }

                    do_fsid_sync_internal(i);
                }

                if (m_shutdown && shutdownloop)
                {
                    m_shutdown_done_gc = true;
                    break;
                }
                tctr.stop_counter();
                next_wait = (m_shutdown)? 0 : (((5000 - tctr.get_millisecs_avg()) < 0) ? 0 : (5000 - tctr.get_millisecs_avg()));
            }

            //we are exiting now
            DEFS.DEBUG("IFSDMux", "Leaving gc/sync thread (Mux)...");
        }

        /*
         * There is no need for locks for sync (shared lock) and unmount (exclusive lock).
         */ 
        public void unmount(int fsid)
        {

            REDDY.FSIDList[fsid].rootdir.sync();
            ((CInode)REDDY.FSIDList[fsid].rootdir).unmount(true);

            lock (REDDY.FSIDList[fsid])
            {
                RedFS_Inode inowip = REDDY.FSIDList[fsid].get_inode_file_wip("GC");
                REDDY.ptrRedFS.sync(inowip);
                REDDY.ptrRedFS.flush_cache(inowip, true); 
                REDDY.FSIDList[fsid].sync_internal();
                REDDY.ptrRedFS.redfs_commit_fsid(REDDY.FSIDList[fsid]);
            }
        }

        /*
        * a. Check that the destination directory exists, and get its inode number
        * b. Allocate a new inode in the destination fsid.
        * c. clone the source file.
        * d. Insert into the destination.
        * e. If failed, do error handling.
        */
        public bool CloneFileTLock(int srcfsid, String sourcefile, int destfsid, String destinationfile)
        {
            string destdirpath = ExtractParentPath(destinationfile);
            if (destdirpath == null) {
                DEFS.DEBUG("CLONEF", "couldnot parse parent path");
                return false;
            }

            Inode_Info destdiri = inode_exists_internal(destfsid, destdirpath, "clone", false, false);
            if (destdiri == null || destdiri.fa == FileAttributes.Normal) {
                DEFS.DEBUG("CLONEF", "destdir is absent or is a file");
                return false;
            }

            int pino = destdiri.ino;
            int newino = find_free_ino_bit(destfsid);

            CFile newfile = REDDY.FSIDList[srcfsid].rootdir.clone_file_tlock(sourcefile, destfsid, newino, pino, destinationfile);
            if (newfile == null) {
                DEFS.DEBUG("CLONEF", "failed to clone file " + sourcefile);
                return false;
            }

            newfile.rename_file(ADFN(destinationfile));
            if (REDDY.FSIDList[destfsid].rootdir.insert_clonefile_tlock(destdirpath, newfile) == false)
            {
                //delete the wip.
                newfile.remove_ondisk_data2();
                return false;
            }
            REDDY.FSIDList[destfsid].set_dirty(true);
            return true;
        }

        public bool CloneDirectoryTlock(int srcfsid, String sourcedir, int destfsid, String destinationdir)
        {
            string destdirpath = ExtractParentPath(destinationdir);
            if (destdirpath == null)
            {
                return false;
            }

            
            Inode_Info destdiri = inode_exists_internal(destfsid, destdirpath, "dclone1", false,false);
            if (destdiri == null)
            {
                return false;
            }
            Inode_Info destdiri2 = inode_exists_internal(destfsid, destinationdir, "dclone2", false, false);
            if (destdiri2 != null) 
            {
                return false;
            }

            Inode_Info srcdirA = inode_exists_internal(srcfsid, sourcedir, "dclone3", true, false);
            if (srcdirA == null) {
                return false;
            }

            int count = srcdirA.nodecount;
            int pino = destdiri.ino;
            List<int> inolist = NEXT_N_INODE_NUMBERS(destfsid, count);

            CDirectory newdir = REDDY.FSIDList[srcfsid].rootdir.clone_directory_tlock(sourcedir, destfsid, inolist, pino, destdirpath);
            if (newdir == null)
            {
                return false;
            }

            newdir.rename_directory(ADFN(destinationdir));
            if (REDDY.FSIDList[destfsid].rootdir.insert_clonedirectory_tlock(destdirpath, newdir) == false)
            {
                //delete the wip.
                newdir.remove_orphan_directory();
                return false;
            }
            return true;
        }
    }
}
