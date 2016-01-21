using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using System.Threading;
using System.IO;

namespace redfs_v2
{
    /*
     * Two of the supported ops for reading/writing.
     */ 
    public enum REDFS_OP    
    {
        REDFS_READ,
        REDFS_WRITE
    }

    /*
     * This api (class) can be used to read/write files using a wip.
     * The wip can be inodefile/regularfile/dirfile. 
     * Operations supported are delete/truncate/append to any type of file
     * Dedupe operation is also supported with a source wip and donor dbn.
     */
    
    public class REDFSDrive
    {
        private RedFSPersistantStorage mvdisk;
        private REFCntManager refcntmgr;
        private ZBufferCache mFreeBufCache;

        private TimingCounter timer_io_time = new TimingCounter();
        
        // Data for the wip delete scanner.
        
        private bool m_shutdown_dscanner;
        private bool m_dscanner_done = false;
        private BlockingCollection<RedFS_Inode> m_wipdelete_queue = new BlockingCollection<RedFS_Inode>(524288);

        // External userlayer can pass a wip to be deleted, but the responsibility
        // of zero'ing it out in the inode file, and clearing the inomap bit is
        // left to the upper layer.

        public bool is_block_free(int dbn)
        {
            return refcntmgr.is_block_free(dbn);
        }

        public void get_refcnt_info(int dbn, ref int refcnt, ref int childcnt)
        {
            refcntmgr.get_refcnt_info(dbn, ref refcnt, ref childcnt);
        }

        public int get_total_inuse_blocks()
        {
            return refcntmgr.get_total_inuse_blocks();
        }

        public void get_total_iops(ref long reads, ref long writes)
        {
            reads = mvdisk.total_disk_reads;
            writes = mvdisk.total_disk_writes;
        }

        // Generally external interface to delete a wip. the caller must ensure
        // that wip->size is set to zero with no pointers and this must be written
        // into the inofile.
         
        public void redfs_delete_wip(int fsid, RedFS_Inode wip, bool clearincorebufs)
        {
            DEFS.DEBUG("DELTHR", "<<<<<<<< Request wip = " + wip.get_ino() + 
                " to be deleted, type = " +  wip.get_wiptype() + " clearibufs " + clearincorebufs + " >>>>>>>.");
            RedFS_Inode wip4delq = new RedFS_Inode(wip.get_wiptype(), wip.get_ino(), 0);

            for (int i = 0; i < 128; i++)
            {
                wip4delq.data[i] = wip.data[i];
            }
            if (clearincorebufs == false)
            {
                DEFS.ASSERT(wip.L0list.Count == 0, "ext del shouldve flushed cached, L0");
                DEFS.ASSERT(wip.L1list.Count == 0, "ext del shouldve flushed cached, L1");
                DEFS.ASSERT(wip.L2list.Count == 0, "ext del shouldve flushed cached, L2");
            }
            else
            {
                wip.sort_buflists();
                //wip.L0list.Clear();
                mFreeBufCache.deallocateList(wip.L0list);

                for (int i = 0; i < wip.L1list.Count; i++)
                {
                    RedBufL1 wbl1 = (RedBufL1)wip.L1list.ElementAt(i);
                    if (wbl1.is_dirty)
                    {
                        mvdisk.write(wip, wbl1);
                    }
                }

                for (int i = 0; i < wip.L2list.Count; i++)
                {
                    RedBufL2 wbl2 = (RedBufL2)wip.L2list.ElementAt(i);
                    if (wbl2.is_dirty)
                    {
                        mvdisk.write(wip, wbl2);
                    }
                }
                wip.L1list.Clear();
                wip.L2list.Clear();   
            }

            refcntmgr.sync();
            wip4delq.setfilefsid_on_dirty(fsid); //this must be set or else the counters go wrong.
            m_wipdelete_queue.Add(wip4delq);

            for (int i = 0; i < 16; i++) { wip.set_child_dbn(i, DBN.INVALID); }
            wip.is_dirty = false;
            wip.set_filesize(0);
        }

        // If i want to delete a L0, then first i must propogate all the refcounts from the indirect first.
        // Then i will have to pass the indirect + its dbn first for the refcounts to be propogated downward.
        // After that i can reduce the refcount of the L1
        
        private void delete_wip_internal2(RedFS_Inode wip)
        {
            DEFS.ASSERT(wip.L0list.Count == 0, "delete_wip_internal2 shouldve flushed cached, L0");
            DEFS.ASSERT(wip.L1list.Count == 0, "delete_wip_internal2 shouldve flushed cached, L1");
            DEFS.ASSERT(wip.L2list.Count == 0, "delete_wip_internal2 shouldve flushed cached, L2");

            if (wip.get_inode_level() == 0)
            {
                for (int i = 0; i < OPS.NUML0(wip.get_filesize()); i++) 
                {
                    int dbnl0 = wip.get_child_dbn(i);
                    refcntmgr.decrement_refcount_ondealloc(wip.get_filefsid(), dbnl0);
                }
            }
            else if (wip.get_inode_level() == 1)
            {
                int counter = OPS.NUML0(wip.get_filesize());
                for (int i = 0; i < OPS.NUML1(wip.get_filesize()); i++) 
                {
                    RedBufL1 wbl1 = (RedBufL1)redfs_load_buf(wip, 1, OPS.PIDXToStartFBN(1, i), false);
                    int idx = 0;
                    refcntmgr.touch_refcount(wbl1, false);
                    while (counter > 0 && idx < 1024)
                    {
                        int dbnl0 = wbl1.get_child_dbn(idx);
                        refcntmgr.decrement_refcount_ondealloc(wip.get_filefsid(), dbnl0);
                        counter--;
                        idx++;
                    }
                    refcntmgr.decrement_refcount_ondealloc(wip.get_filefsid(), wbl1.m_dbn);
                }
            }
            else if (wip.get_inode_level() == 2)
            {
                int counter = OPS.NUML0(wip.get_filesize());
                int numl1s_remaining = OPS.NUML1(wip.get_filesize());

                for (int i2 = 0; i2 < OPS.NUML2(wip.get_filesize()); i2++)
                {
                    RedBufL2 wbl2 = (RedBufL2)redfs_load_buf(wip, 2, OPS.PIDXToStartFBN(2, i2), false);
                    refcntmgr.touch_refcount(wbl2, false);

                    int curr_l1cnt = (numl1s_remaining > 1024) ? 1024 : numl1s_remaining;

                    for (int i1 = 0; i1 < curr_l1cnt; i1++) 
                    {
                        RedBufL1 wbl1 = (RedBufL1)redfs_load_buf(wip, 1, OPS.PIDXToStartFBN(1, i1), false);
                        int idx = 0;

                        refcntmgr.touch_refcount(wbl1, false);
                        while (counter > 0 && idx < 1024)
                        {
                            int dbnl0 = wbl1.get_child_dbn(idx);
                            refcntmgr.decrement_refcount_ondealloc(wip.get_filefsid(), dbnl0);
                            counter--;
                            idx++;
                        }
                        refcntmgr.decrement_refcount_ondealloc(wip.get_filefsid(), wbl1.m_dbn);
                    }

                    refcntmgr.decrement_refcount_ondealloc(wip.get_filefsid(), wbl2.m_dbn);
                }
            }
        }

        private void WThr_Delete_Scanner()
        {
            DEFS.DEBUG("DELTHR", "Starting delete scanner THREAD");

            // todo, load information from checkpoint and store back in queue if required.
             
            while (m_shutdown_dscanner == false)
            {
                RedFS_Inode wip = (RedFS_Inode)m_wipdelete_queue.Take();
                if (wip.get_ino() == -1)
                {
                    DEFS.DEBUG("DELTHR", "Recived signal to shutdown");
                    break;
                }
                else
                {
                    DEFS.DEBUG("DELTHR", "Attempting to delete ino = " + wip.get_ino() + " type = " + wip.get_wiptype());
                    delete_wip_internal2(wip);
                }
            }

           
            // TODO : Write the undone work into a checkpoint file, so that restart can handle that.
       
            DEFS.DEBUG("DELTHR", "Stopping delete scanner THREAD");
            m_dscanner_done = true;
        }

        public void dedupe_swap_changelog()
        {
            mvdisk.swap_clog();
        }

        public REDFSDrive()
        {
            // Create the buffer cache
            
            mFreeBufCache = new ZBufferCache();
            mFreeBufCache.init();

            DEFS.DEBUG("TRACE", "Before creating disk0");
            
            // Open the 'disk' first where all the file/volume data is present.

            mvdisk = new RedFSPersistantStorage("disk2", "clog");

            // Open the refcount manager, and initialize its internal threads.
            refcntmgr = new REFCntManager();
            refcntmgr.init();

            // Create a zero-drive/Lundrive/Backupdrive if not already present. 
            redfs_create_zeroed_fsid();
            redfs_create_lun_fsid();
            redfs_create_backup_fsid();

            // Start the delete thread to listen/start unfinished work.
           
            Thread tc = new Thread(new ThreadStart(WThr_Delete_Scanner));
            tc.Start();
        }

        public bool shut_down()
        {
            DEFS.DEBUG("SHUTDOWN", "Calling redfs() shut down");

            mvdisk.shut_down();

            while (m_dscanner_done == false)
            {
                m_shutdown_dscanner = true;
                m_wipdelete_queue.Add(new RedFS_Inode(WIP_TYPE.REGULAR_FILE, -1, 0));
                DEFS.DEBUG("SHUTDOWN", "Waiting for dscanner..");
                Thread.Sleep(1000);
            }
            refcntmgr.shut_down();
            mFreeBufCache.shutdown();
            DEFS.DEBUG("SHUTDOWN", "Finishing redfs() shut down");
            return true;
        }

       
        // The caller must set the parent appropriately.
        private Red_Buffer redfs_allocate_buffer(int fsid, BLK_TYPE type, long startfbn, bool forread)
        {
            Red_Buffer wb = null;

            switch (type)
            {
                case BLK_TYPE.REGULAR_FILE_L0:
                    wb = mFreeBufCache.allocate(startfbn);
                    break;
                case BLK_TYPE.REGULAR_FILE_L1:
                    wb = new RedBufL1(startfbn);
                    break;
                case BLK_TYPE.REGULAR_FILE_L2:
                    wb = new RedBufL2(startfbn);
                    break;
            }

            DEFS.ASSERT(wb != null, "Incorrect buffer type passed for allocate() in REDFS");

            
            int pdbn = 0;
            if (forread == true)
            {
                wb.set_dirty(false);
                wb.set_ondisk_exist_flag(true);
            }
            else
            {
                wb.set_dirty(true);
                wb.set_ondisk_exist_flag(true);
                int dbn = refcntmgr.allocate_dbn(type, 0);
                refcntmgr.increment_refcount_onalloc(fsid, dbn);
                wb.set_dbn_reassignment_flag(false); //this is just created!.
                wb.set_touchrefcnt_needed(false); //nobody is derived from this.
                REDFS_BUFFER_ENCAPSULATED wbe = new REDFS_BUFFER_ENCAPSULATED(wb);
                wbe.set_dbn(dbn);
                pdbn = dbn;
            }
            
            //DEFS.DEBUG("REDFS", "calling redfs_allocate_buffer(" + type + "," + startfbn + 
            //    ", forread " + forread + ") dbn = " + pdbn);
            return wb;
        }

        private void redfs_levelincr_regularfile_wip(RedFS_Inode wip)
        {
            //DEFS.DEBUG("REDFS", "redfs_levelincr_regularfile_wip ( " + wip.m_ino + ") currlevel = " + wip.get_inode_level());

            if (wip.get_inode_level() == 0)
            {
                DEFS.ASSERT(OPS.NUML0(wip.get_filesize()) == 16, "Unfull wip in level increment codepath ");

                RedBufL1 wbl1 = (RedBufL1)redfs_allocate_buffer(wip.get_filefsid(), BLK_TYPE.REGULAR_FILE_L1, 0, false);
                for (int i = 0; i < 16; i++)
                {
                    //DEFS.DEBUG("REDFS", "L0->L1, idx(" + i + ") dbn = " + wip.get_child_dbn(i));
                    wbl1.set_child_dbn(i, wip.get_child_dbn(i));
                    wip.set_child_dbn(i, DBN.INVALID);
                }
                wip.set_child_dbn(0, wbl1.m_dbn);
                wip.L1list.Add(wbl1);
            }
            else if (wip.get_inode_level() == 1)
            {
                DEFS.ASSERT(OPS.NUML1(wip.get_filesize()) == 16, "Unfull wip in level increment codepath2");

                RedBufL2 wbl2 = (RedBufL2)redfs_allocate_buffer(wip.get_filefsid(), BLK_TYPE.REGULAR_FILE_L2, 0, false);
                for (int i = 0; i < 16; i++)
                {
                    //DEFS.DEBUG("REDFS", "L1->L2, idx(" + i + ") dbn = " + wip.get_child_dbn(i));
                    wbl2.set_child_dbn(i, wip.get_child_dbn(i));
                    wip.set_child_dbn(i, DBN.INVALID);
                }
                wip.set_child_dbn(0, wbl2.m_dbn);
                wip.L2list.Add(wbl2);
            }
            else
            {
                DEFS.ASSERT(false, "Cannot level increment a L2 level file");
            }
            wip.is_dirty = true;
        }

        // Currently, this is for a directory file only, when a directory file is being
        // overwritten, then the wip needs to be reinstantiated, that is why this function is
        // needed.
        // 
        private void redfs_create_sparse_file3(RedFS_Inode wip, long size)
        {
            DEFS.ASSERT(wip.get_filesize() == 0, "File size must be 0 when calling create sparse file");

            int numl2 = OPS.NUML2(size);
            int numl1 = OPS.NUML1(size);
            int numl0 = OPS.NUML0(size);

            if (OPS.FSIZETOILEVEL(size) == 0) 
            {
                for (int i = 0; i < numl0; i++) 
                {
                    wip.set_child_dbn(i, 0);
                }
                wip.set_filesize(size);
                wip.is_dirty = true;
                return;
            }
            else if (OPS.FSIZETOILEVEL(size) == 1) 
            {
                int numl0todo = numl0;

                for (int i = 0; i < numl1; i++)
                {
                    RedBufL1 wbl1 = (RedBufL1)redfs_allocate_buffer(wip.get_filefsid(), BLK_TYPE.REGULAR_FILE_L1, OPS.PIDXToStartFBN(1, i), false);
                    wip.set_child_dbn(i, wbl1.m_dbn);

                    int counter = 0;
                    while (numl0todo >= 0) 
                    {
                        wbl1.set_child_dbn(counter, 0);   
                        numl0todo--;
                        counter++;
                        if (counter == 1024) break;
                    }
                }

            }
        }
        
 
        // dummy is valid only for regrow, where only indirects are allocated,
        // but L0 bufs are not populated. L1's will have 0's
        public void redfs_resize_wip(RedFS_Inode wip, long newsize, bool dummy)
        {
            if (wip.get_filesize() <= newsize)
            {
                bool dummy6 = ((newsize - wip.get_filesize()) > 256 * 1024) ? true : false;
                bool quickgrow = (wip.get_filesize() == 0) ? true : false;
                if (dummy6 && quickgrow)
                {
                    redfs_grow_wip_internal_superfast2(wip, newsize);
                }
                else
                {
                    redfs_grow_wip_internal(wip, newsize, dummy6);
                }
            }
            else
            {
                redfs_shrink_wip_internal(wip.get_filefsid(), wip, newsize);
            }
        }

        //
        // Shrink wip internal logic, will involve freeing bufs, and resizing wip.
        // Can be used to delete wip (truncate to zero size). Windows API will callback
        // setfilesize once write is done.
        // 
        private void redfs_shrink_wip_internal(int callingfsid, RedFS_Inode wip, long newsize)
        {
            if (newsize == 0)
            {
                DEFS.DEBUG("REDFS", "As good as delete since newsize is zero");
                redfs_delete_wip(callingfsid, wip, true);
                wip.set_filesize(0);
                return;
            }

            int numl0new = OPS.NUML0(newsize);
            int[] dbns = new int[numl0new];

            RedFS_Inode newwip = new RedFS_Inode(wip.get_wiptype(), wip.get_ino(), wip.get_parent_ino());
            newwip.setfilefsid_on_dirty(wip.get_filefsid());
            newwip.set_filesize(newsize);

            redfs_get_file_dbns(wip, dbns, 0, numl0new);
            redfs_create_wip_from_dbnlist(newwip, dbns, false);
            newwip.set_filesize(newsize);
            sync(newwip);

            redfs_delete_wip(callingfsid, wip, true);

            for (int i = 0; i < 128; i++) 
            {
                wip.data[i] = newwip.data[i];
            }
            wip.is_dirty = true;
            DEFS.ASSERT(wip.get_filesize() == newsize, "could not shrink wip 2");
        }

        private void redfs_grow_wip_internal_superfast2(RedFS_Inode wip, long newsize)
        {
            DEFS.DEBUG("REDFS", "Superfast grow : " + newsize + ", " + wip.get_string_rep2());
            DEFS.ASSERT(newsize <= ((long)1024 * 1024 * 1024 * 64), "Max size for inode exceeded");
            DEFS.ASSERT(OPS.FSIZETOILEVEL(newsize) >= 1, "Cannot use fast grow for small files");

            int count = (OPS.FSIZETOILEVEL(newsize) == 1) ? OPS.NUML1(newsize) : OPS.NUML2(newsize);
            for (int i = 0; i < count; i++)
            {
                wip.set_child_dbn(i, 0);
            }
            wip.set_filesize(newsize);
        }

        private void redfs_grow_wip_internal(RedFS_Inode wip, long newsize, bool dummy)
        {
            DEFS.ASSERT(newsize <= ((long)1024 * 1024 * 1024 * 64), "Max size for inode exceeded");

            //DEFS.DEBUG("REDFS", "redfs_grow_wip_internal(" + wip.m_ino + "," + newsize + "," + dummy +
            //         ") " + " level = " + wip.get_inode_level() + " currsize=" + wip.get_filesize());
            //DEFS.DEBUG("REDFS", "Incore count = " + wip.L0list.Count + "," + wip.L1list.Count + "," + wip.L2list.Count);

            while (true)
            {
                int currL0sincore = wip.L0list.Count;
                long nextstepsize = OPS.NEXT4KBOUNDARY(wip.get_filesize(), newsize);
                
                // We are done since, newsize is reached, so we can return.
                 
                if (wip.get_filesize() == newsize)
                {
                    //DEFS.DEBUG("REDFS", "Inode resize is complete, Filesize = " + 
                    //        wip.get_filesize() + " slack : " + OPS.SLACK(wip.get_filesize()));
                    return;
                }

                
                // Load the indirects at the end of the filetree
                 
                int xsfbn2 = OPS.OffsetToStartFBN(2, wip.get_filesize() - 1);
                int xsfbn1 = OPS.OffsetToStartFBN(1, wip.get_filesize() - 1);

                if (wip.get_inode_level() == 2)
                {
                    redfs_load_buf(wip, 2, xsfbn2, false);
                    redfs_load_buf(wip, 1, xsfbn1, false);
                }
                else if (wip.get_inode_level() == 1)
                {
                    redfs_load_buf(wip, 1, xsfbn1, false);
                }

                 
                // Cover to the first 4k boundary, where there is no need to add any new L0.
                
                if (OPS.NUML0(wip.get_filesize()) == OPS.NUML0(nextstepsize))
                {
                    wip.set_filesize(nextstepsize);
                    continue;
                }

                int new_L0fbn = OPS.OffsetToFBN(nextstepsize - 1);

                if (wip.get_inode_level() == 0)
                {
                    bool growlevel = (OPS.FSIZETOILEVEL(nextstepsize) == 1) ? true : false;
                    RedBufL0 wbl0 = null;
                    int wbl0dbn = 0;

                    if (dummy == false)
                    {
                        wbl0 = (RedBufL0)redfs_allocate_buffer(wip.get_filefsid(), BLK_TYPE.REGULAR_FILE_L0, new_L0fbn, false);
                        wbl0dbn = wbl0.m_dbn;
                        wip.L0list.Add(wbl0);
                    }

                    if (!growlevel)
                    {
                        DEFS.ASSERT(new_L0fbn < 16, "Wrong computation for fbn 1, " + new_L0fbn + " fsiz :"+ wip.get_filesize());
                        wip.set_child_dbn(new_L0fbn, wbl0dbn);
                    }
                    else if (growlevel)
                    {
                        redfs_levelincr_regularfile_wip(wip);
                        RedBufL1 wbl1 = (RedBufL1)OPS.get_buf3("WGI", wip, 1, new_L0fbn, false);
                        DEFS.ASSERT(wbl1 != null, "This should never fail, since the level was grown just now");
                        wbl1.set_child_dbn(16, wbl0dbn);
                    }

                    wip.set_filesize(nextstepsize);
                    wip.is_dirty = true;
                }
                else if (wip.get_inode_level() == 1)
                {
                    bool growlevel = (OPS.FSIZETOILEVEL(nextstepsize) == 2) ? true : false;

                    RedBufL0 wbl0 = null;

                    if (dummy == false)
                    {
                        wbl0 = (RedBufL0)redfs_allocate_buffer(wip.get_filefsid(), BLK_TYPE.REGULAR_FILE_L0, new_L0fbn, false);
                    }

                    if (!growlevel)
                    {
                        int lastfbn = OPS.OffsetToFBN(wip.get_filesize() - 1);
                        DEFS.ASSERT(new_L0fbn == (lastfbn + 1), "nextfbn must be 1 greater than the previous fbn " + new_L0fbn + " > " + lastfbn +
                                    " nextstepsize " + nextstepsize);

                        int sfbn1 = OPS.OffsetToStartFBN(1, nextstepsize - 1);

                        RedBufL1 wbl1 = null;
                        if (new_L0fbn % 1024 == 0)
                        {
                            wbl1 = (RedBufL1)redfs_allocate_buffer(wip.get_filefsid(), BLK_TYPE.REGULAR_FILE_L1, sfbn1, false);
                            int idx = wbl1.myidx_in_myparent();
                            wip.set_child_dbn(idx, wbl1.m_dbn);
                            wip.L1list.Add(wbl1);
                        }
                        else
                        {
                            wbl1 = (RedBufL1)redfs_load_buf(wip, 1, sfbn1, false);
                        }

                        if (dummy == false)
                        {
                            int xidx = wbl0.myidx_in_myparent();
                            wbl1.set_child_dbn(xidx, wbl0.m_dbn);
                            wip.L0list.Add(wbl0);
                        }
                        else
                        {
                            wbl1.set_child_dbn(OPS.myidx_in_myparent(0, new_L0fbn), 0);
                        }
                        
                        wip.is_dirty = true;
                    }
                    else if (growlevel)
                    {
                        redfs_levelincr_regularfile_wip(wip);

                        RedBufL2 wbl2 = (RedBufL2)OPS.get_buf3("WGI", wip, 2, 0, false);
                        DEFS.ASSERT(wbl2 != null, "This can never be null since level was just grown 2");

                        RedBufL1 wbl1 = (RedBufL1)redfs_allocate_buffer(wip.get_filefsid(), BLK_TYPE.REGULAR_FILE_L1, 16384, false);
                        wbl2.set_child_dbn(16, wbl1.m_dbn);
                        wip.L1list.Add(wbl1);
                        DEFS.ASSERT(new_L0fbn == 16384, "Incorrect fbn in evaluation");

                        if (dummy == false)
                        {
                            wbl1.set_child_dbn(0, wbl0.m_dbn);
                            wip.L0list.Add(wbl0);
                        }
                        else
                        {
                            wbl1.set_child_dbn(0, 0);
                        }
                    }
                    wip.set_filesize(nextstepsize);
                }
                else if (wip.get_inode_level() == 2)
                {
                    RedBufL2 wbl2 = null;
                    bool needL2 = (OPS.NUML2(wip.get_filesize()) < OPS.NUML2(nextstepsize)) ? true : false;
                    int sfbn2 = OPS.OffsetToStartFBN(2, nextstepsize - 1);

                    if (needL2)
                    {
                        wbl2 = (RedBufL2)redfs_allocate_buffer(wip.get_filefsid(), BLK_TYPE.REGULAR_FILE_L2, sfbn2, false);
                        wip.L2list.Add(wbl2);
                        int idx = wbl2.myidx_in_myparent();
                        wip.set_child_dbn(idx, wbl2.m_dbn);
                    }
                    else
                    {
                        wbl2 = (RedBufL2)redfs_load_buf(wip, 2, sfbn2, false);
                    }

                    bool needL1 = (OPS.NUML1(wip.get_filesize()) < OPS.NUML1(nextstepsize)) ? true : false;
                    RedBufL1 wbl1 = null;
                    int sfbn1 = OPS.OffsetToStartFBN(1, nextstepsize - 1);

                    if (needL1)
                    {
                        wbl1 = (RedBufL1)redfs_allocate_buffer(wip.get_filefsid(), BLK_TYPE.REGULAR_FILE_L1,
                            sfbn1, false);
                        int idx = wbl1.myidx_in_myparent();
                        wbl2.set_child_dbn(idx, wbl1.m_dbn);
                        wip.L1list.Add(wbl1);
                    }
                    else
                    {
                        wbl1 = (RedBufL1)redfs_load_buf(wip, 1, sfbn1, false);
                    }

                    if (dummy == false)
                    {
                        RedBufL0 wbl0 = (RedBufL0)redfs_allocate_buffer(wip.get_filefsid(), BLK_TYPE.REGULAR_FILE_L0, new_L0fbn, false);
                        int last_slot = wbl0.myidx_in_myparent();
                        wbl1.set_child_dbn(last_slot, wbl0.m_dbn);
                        wip.L0list.Add(wbl0);
                    }
                    else
                    {
                        wbl1.set_child_dbn(OPS.myidx_in_myparent(0, new_L0fbn), 0);
                    }
                    wip.set_filesize(nextstepsize);
                }

                DEFS.ASSERT(wip.L0list.Count <= (currL0sincore + 1), "L0's added more than one in loop " + 
                        wip.L0list.Count + ", old = " + currL0sincore);
            }
        }

        //
        // First check incore, if not load and return.
        // Adjust refcounts accordingly. 
        //
        private Red_Buffer redfs_load_buf(RedFS_Inode wip, int level, int someL0fbn, bool forwrite)
        {
            Red_Buffer ret = (Red_Buffer)OPS.get_buf3("WLB", wip, level, someL0fbn, true);
            if (ret != null) 
            {
                return ret;
            }
            //DEFS.DEBUG("REDFS", "calling redfs_load_buf(" + wip.m_ino + "," + level + "," + someL0fbn + ")");

            if (level == 2)
            {
                if (wip.get_inode_level() == 2)
                {
                    int start_fbnl2 = OPS.SomeFBNToStartFBN(2, someL0fbn);

                    int l2_idx_wip = someL0fbn / (1024 * 1024);
                    int dbnl2 = wip.get_child_dbn(l2_idx_wip);
                    DEFS.ASSERT(dbnl2 != DBN.INVALID, "Invalid dbn found in a valid portion 1, fsize = " + wip.get_filesize());

                    RedBufL2 wbl2 = (RedBufL2)redfs_allocate_buffer(wip.get_filefsid(), BLK_TYPE.REGULAR_FILE_L2, start_fbnl2, true);
                    wbl2.m_dbn = dbnl2;
                    mvdisk.read(wbl2);
                    wip.L2list.Add(wbl2);
                    DEFS.ASSERT(wbl2.needtouchbuf, "This cannot be cleared out! 1");
                    DEFS.ASSERT(wbl2.needdbnreassignment, "we also need to reassign this dbn in case of overwrite 3");

                    refcntmgr.touch_refcount(wbl2, false);
                    return wbl2;
                }
                else
                {
                    DEFS.ASSERT(false, "Cannot load an l2 buf from a level " + wip.get_inode_level() + " file");
                }
            }
            else if (level == 1)
            {
                if (wip.get_inode_level() == 2)
                {
                    int start_fbnl1 = OPS.SomeFBNToStartFBN(1, someL0fbn);
                    RedBufL2 wbl2 = (RedBufL2)redfs_load_buf(wip, 2, someL0fbn, forwrite);

                    RedBufL1 wbl1 = (RedBufL1)redfs_allocate_buffer(wip.get_filefsid(), BLK_TYPE.REGULAR_FILE_L1, start_fbnl1, true);

                    int idx = wbl1.myidx_in_myparent();
                    int dbnl1 = wbl2.get_child_dbn(idx);
                    DEFS.ASSERT(dbnl1 != DBN.INVALID, "Invalid dbn found in a valid portion 2, fsize = " + wip.get_filesize());
                    DEFS.ASSERT(wbl1.needtouchbuf, "This cannot be cleared out! 2");
                    DEFS.ASSERT(wbl1.needdbnreassignment, "we also need to reassign this dbn in case of overwrite 2");

                    wbl1.m_dbn = dbnl1;
                    mvdisk.read(wbl1);
                    wip.L1list.Add(wbl1);
                    refcntmgr.touch_refcount(wbl1, false);
                    return wbl1;
                }
                else if (wip.get_inode_level() == 1)
                {
                    int start_fbnl1 = OPS.SomeFBNToStartFBN(1, someL0fbn);

                    int idx = someL0fbn / (1024);
                    int dbnl1 = wip.get_child_dbn(idx);
                    DEFS.ASSERT(dbnl1 != DBN.INVALID, "Invalid dbn found in a valid portion 3, fsize = " + wip.get_filesize());
                    RedBufL1 wbl1 = (RedBufL1)redfs_allocate_buffer(wip.get_filefsid(), BLK_TYPE.REGULAR_FILE_L1, start_fbnl1, true);
                    wbl1.m_dbn = dbnl1;
                    mvdisk.read(wbl1);
                    wip.L1list.Add(wbl1);
                    DEFS.ASSERT(wbl1.needdbnreassignment, "we also need to reassign this dbn in case of overwrite 1");
                    DEFS.ASSERT(wbl1.needtouchbuf, "This cannot be cleared out! 3");
                    refcntmgr.touch_refcount(wbl1, false);
                    return wbl1;
                }
                else
                {
                    DEFS.ASSERT(false, "Cannot load a level 1 buf from a level 0 inode");
                }
            }
            else if (level == 0)
            {
                if (wip.get_inode_level() == 0)
                {
                    DEFS.ASSERT(someL0fbn < 16, "Requesting for large fbn in level-0 file");
                    int idx = someL0fbn;
                    int dbn0 = wip.get_child_dbn(idx);
                    DEFS.ASSERT(dbn0 != DBN.INVALID, "Invalid dbn found in a valid portion 4, fsize = " + wip.get_filesize());

                    RedBufL0 wbl0 = (RedBufL0)redfs_allocate_buffer(wip.get_filefsid(), BLK_TYPE.REGULAR_FILE_L0, someL0fbn, true);
                    wbl0.m_dbn = dbn0;

                    if (forwrite == false)
                    {
                        mvdisk.read(wbl0);
                    }

                    //
                    // WTF, i had put this before read, and had fuck trouble until i finally rootcaused the issue.
                    // Same with fast write case, i have to read and write from loadbuf for inode L0.
                    // 
                    if (forwrite == false && wip.get_wiptype() == WIP_TYPE.PUBLIC_INODE_FILE)
                    {
                        DEFS.ASSERT(wbl0.needtouchbuf, "This cannot be cleared out! 6");
                        refcntmgr.touch_refcount(wbl0, true);
                    }
                    wip.L0list.Add(wbl0);

                    return wbl0;
                }
                else
                {
                    RedBufL1 wbl1 = (RedBufL1)redfs_load_buf(wip, 1, someL0fbn, forwrite);

                    int dbn0 = wbl1.get_child_dbn(someL0fbn%1024);
                    if (dbn0 == DBN.INVALID)
                    {
                        redfs_show_vvbns2(wip, true);
                    }
                    DEFS.ASSERT(dbn0 != DBN.INVALID, "Invalid dbn found in a valid portion 5, fsize = " + 
                        wip.get_filesize() + " wbl1.sfbn = " + wbl1.m_start_fbn + " somel0fbn = " + someL0fbn);

                    RedBufL0 wbl0 = (RedBufL0)redfs_allocate_buffer(wip.get_filefsid(), BLK_TYPE.REGULAR_FILE_L0, someL0fbn, true);
                    wbl0.m_dbn = dbn0;

                    if (forwrite == false)
                    {
                        mvdisk.read(wbl0);
                    }

                    if (forwrite == false && wip.get_wiptype() == WIP_TYPE.PUBLIC_INODE_FILE)
                    {
                        DEFS.ASSERT(wbl0.needtouchbuf, "This cannot be cleared out! 4");
                        refcntmgr.touch_refcount(wbl0, true);
                    }
                    wip.L0list.Add(wbl0);
                    return wbl0;
                }
            }
            DEFS.ASSERT(false, "redfs_load_buf failed to find anything");
            return null;
        }

        
        // Must not do sync, but just remove non-dirty buffers.     
        public void flush_cache(RedFS_Inode wip, bool inshutdown)
        {
            wip._lasthitbuf = null;

            for (int i = 0; i < wip.L0list.Count; i++)
            {
                RedBufL0 wbl0 = (RedBufL0)wip.L0list.ElementAt(0);
                if (wbl0.is_dirty == false)
                {
                    wip.L0list.RemoveAt(0);
                    mFreeBufCache.deallocate4(wbl0);
                    i--;
                }           
            }

            for (int i = 0; i < wip.L1list.Count; i++)
            {
                RedBufL1 wbl1 = (RedBufL1)wip.L1list.ElementAt(0);
                if (wbl1.is_dirty == false)
                {
                    wip.L1list.RemoveAt(0);
                    i--;
                }
            }

            for (int i = 0; i < wip.L2list.Count; i++)
            {
                RedBufL2 wbl2 = (RedBufL2)wip.L2list.ElementAt(0);
                if (wbl2.is_dirty == false)
                {
                    wip.L2list.RemoveAt(0);
                    i--;
                }
            }

            if (!(!inshutdown || wip.get_incore_cnt() == 0))
            {
                  //redfs_show_vvbns2(wip, true);
                  //OPS.dumplistcontents(wip.L0list);
            }

            DEFS.ASSERT(!inshutdown || wip.get_incore_cnt() == 0, "Cannot have dirty buffers incore " +
                " when doing a flush cache during shutdown (" + wip.L0list.Count + "," + wip.L1list.Count + 
                "," + wip.L2list.Count  + ") : " + wip.get_string_rep2());
        }

        public void sync(RedFS_Inode wip)
        { 
            //
            // First clean all the L0's, when cleaning, note that the entries in the L1 are matching!
            // Repeat the same for all the levels starting from down onwards.
            //
            wip.sort_buflists();
            wip._lasthitbuf = null;
            //DEFS.DEBUG("-SYNC-", "wip = " + wip.m_ino + " cnt = " + wip.L0list.Count + "," + wip.L1list.Count + "," + wip.L2list.Count);
            //if (wip.get_wiptype() == WIP_TYPE.PUBLIC_INODE_FILE)
            //{
                //OPS.dumplistcontents(wip.L0list);
                //OPS.dumplistcontents(wip.L1list);
                //OPS.dumplistcontents(wip.L2list);
            //}

            for (int i = 0; i < wip.L0list.Count; i++)
            {
                RedBufL0 wbl0 = (RedBufL0)wip.L0list.ElementAt(i); //too freaky bug, shouldnt be 0 but i.

                int myidxinpt = wbl0.myidx_in_myparent();
                if (wip.get_inode_level() == 0)
                {
                    DEFS.ASSERT(wip.get_child_dbn(myidxinpt) == wbl0.m_dbn, "mismatch during sync, " +
                        " wbl1(" + myidxinpt + ")=" + wip.get_child_dbn(myidxinpt) + " and wbl0.m_dbn = " +
                        wbl0.m_dbn + " in wip = " + wip.get_ino());
                }
                else
                {
                    RedBufL1 wbl1 = (RedBufL1)OPS.get_buf3("SYNC", wip, 1, (int)wbl0.m_start_fbn, false);
                    DEFS.ASSERT(wbl1.get_child_dbn(myidxinpt) == wbl0.m_dbn, "mismatch during sync, " +
                        " wbl1(" + myidxinpt + ")=" + wbl1.get_child_dbn(myidxinpt) + " and wbl0.m_dbn = " +
                        wbl0.m_dbn + " in wip = " + wip.get_ino());
                }
                 
                if (wbl0.is_dirty)
                {
                    //if (wip.get_wiptype() == WIP_TYPE.PUBLIC_INODE_FILE)
                   // {
                     //   DEFS.DEBUG("NOTE", "Cleaning lo block of inofile " + wbl0.m_dbn);
                   // }
                    mvdisk.write(wip, wbl0);
                }
                
                if (wip.get_wiptype() == WIP_TYPE.DIRECTORY_FILE || wbl0.isTimetoClear())
                {
                    wip.L0list.RemoveAt(i);
                    mFreeBufCache.deallocate4(wbl0);
                    i--;
                }
            }

            if (wip.get_wiptype() == WIP_TYPE.PUBLIC_INODE_FILE)
            {
                DEFS.DEBUG("XSDF", "Counter = " + wip.L0list.Count);
                OPS.dumplistcontents(wip.L0list);
            }

            for (int i = 0; i < wip.L1list.Count; i++)
            {
                RedBufL1 wbl1 = (RedBufL1)wip.L1list.ElementAt(i);

               
                int myidxinpt = wbl1.myidx_in_myparent();
                if (wip.get_inode_level() == 1)
                {
                    DEFS.ASSERT(wip.get_child_dbn(myidxinpt) == wbl1.m_dbn, "mismatch during sync, " +
                        " wbl1(" + myidxinpt + ")=" + wip.get_child_dbn(myidxinpt) + " and wbl1.m_dbn = " +
                        wbl1.m_dbn + " in wip = " + wip.get_ino());
                }
                else if (wip.get_inode_level() == 2)
                {
                    RedBufL2 wbl2 = (RedBufL2)OPS.get_buf3("SYNC", wip, 2, (int)wbl1.m_start_fbn, false);
                    DEFS.ASSERT(wbl2.get_child_dbn(myidxinpt) == wbl1.m_dbn, "mismatch during sync, " +
                        " wbl2(" + myidxinpt + ")=" + wbl2.get_child_dbn(myidxinpt) + " and wbl1.m_dbn = " +
                        wbl1.m_dbn + " in wip = " + wip.get_ino());
                }
                else
                {
                    DEFS.ASSERT(false, "how the hell do we have l1 bufs in L0 wip?");
                }
                
                if (wbl1.is_dirty)
                {
                    mvdisk.write(wip, wbl1);
                }
                if (wip.get_wiptype() == WIP_TYPE.DIRECTORY_FILE ||
                            (wbl1.isTimetoClear() && !OPS.HasChildIncoreOld(wip, 1, wbl1.m_start_fbn)))
                {
                    wip.L1list.RemoveAt(i);
                    i--;
                }
                wip.is_dirty = false;
            }

            for (int i = 0; i < wip.L2list.Count; i++)
            {
                RedBufL2 wbl2 = (RedBufL2)wip.L2list.ElementAt(i);

                int myidxinpt = wbl2.myidx_in_myparent();
                if (wip.get_inode_level() == 2)
                {
                    DEFS.ASSERT(wip.get_child_dbn(myidxinpt) == wbl2.m_dbn, "mismatch during sync, " +
                        " wbl1(" + myidxinpt + ")=" + wip.get_child_dbn(myidxinpt) + " and wbl1.m_dbn = " +
                        wbl2.m_dbn + " in wip = " + wip.get_ino());
                }
                else
                {
                    DEFS.ASSERT(false, "how the hell do we have l1 bufs in L0 wip?");
                }

                if (wbl2.is_dirty)
                {
                    mvdisk.write(wip, wbl2);
                }
                if (wip.get_wiptype() == WIP_TYPE.DIRECTORY_FILE ||
                    (wbl2.isTimetoClear() && !OPS.HasChildIncoreOld(wip, 2, wbl2.m_start_fbn)))
                {
                    wip.L2list.RemoveAt(i);
                    i--;
                }
            }
            /*
             * Better sync for every inode sync, so that when shutting down, inowip is also
             * accounted for.
             */ 
            refcntmgr.sync();
            wip.is_dirty = false;
        }

        private void redfs_reassign_new_dbn(RedFS_Inode wip, Red_Buffer wb)
        {
            bool isinofileL0 = (wb.get_level() == 0) && (wip.get_wiptype() == WIP_TYPE.PUBLIC_INODE_FILE);

            DEFS.ASSERT(wb.get_dbn_reassignment_flag() == true, "Wrong call");

            //refcntmgr.decrement_refcount(wb, isinofileL0);
            if (wb.get_ondisk_dbn() != 0)
            {
                refcntmgr.decrement_refcount_ondealloc(wip.get_filefsid(), wb.get_ondisk_dbn());
            }
            int newdbn = refcntmgr.allocate_dbn(wb.get_blk_type(), 0);
            //DEFS.DEBUG("REALLOC", "(Reassign) : ### " + wb.get_ondisk_dbn() + " -> " + 
            //        newdbn + " ### lvl = " + wb.get_level() + " sfbn = " + wb.get_start_fbn());

            REDFS_BUFFER_ENCAPSULATED wbe = new REDFS_BUFFER_ENCAPSULATED(wb);
            wbe.set_dbn(newdbn);
            wb.set_dbn_reassignment_flag(false);
            refcntmgr.increment_refcount_onalloc(wip.get_filefsid(), newdbn);
        }

        /*
         * wbl0 can be null in case of fast read/write case.
         */
        private void redfs_allocate_new_dbntree(RedFS_Inode wip, RedBufL0 wbl0, int givenfbn)
        {
            bool reassigndone = false;
            int start_fbn = (wbl0 == null) ? givenfbn : (int)wbl0.m_start_fbn;

            if (wip.get_inode_level() == 0)
            {
                if (wbl0 != null && wbl0.needdbnreassignment)
                {
                    redfs_reassign_new_dbn(wip, wbl0);
                    int pidx = wbl0.myidx_in_myparent();
                    wip.set_child_dbn(pidx, wbl0.m_dbn);
                    wip.is_dirty = true;
                    reassigndone = true;
                    wbl0.needdbnreassignment = false;
                }
            }
            else if (wip.get_inode_level() == 1)
            {
                RedBufL1 wbl1 = (RedBufL1)OPS.get_buf3("RE-DBN", wip, 1, start_fbn, false);
                if (wbl1.needdbnreassignment)
                {
                    redfs_reassign_new_dbn(wip, wbl1);
                    int pidx = wbl1.myidx_in_myparent();
                    wip.set_child_dbn(pidx, wbl1.m_dbn);
                    wip.is_dirty = true;
                    reassigndone = true;
                    wbl1.needdbnreassignment = false;
                }
                if (wbl0 != null && wbl0.needdbnreassignment)
                {
                    redfs_reassign_new_dbn(wip, wbl0);
                    int pidx = wbl0.myidx_in_myparent();
                    wbl1.set_child_dbn(pidx, wbl0.m_dbn);
                    wbl1.is_dirty = true;
                    reassigndone = true;
                    wbl0.needdbnreassignment = false;
                }
            }
            else if (wip.get_inode_level() == 2)
            {
                RedBufL1 wbl1 = (RedBufL1)OPS.get_buf3("RE-DBN", wip, 1, (int)start_fbn, false);
                RedBufL2 wbl2 = (RedBufL2)OPS.get_buf3("RE-DBN", wip, 2, (int)start_fbn, false);
                if (wbl2.needdbnreassignment)
                {
                    redfs_reassign_new_dbn(wip, wbl2);
                    int pidx = wbl2.myidx_in_myparent();
                    wip.set_child_dbn(pidx, wbl2.m_dbn);
                    wip.is_dirty = true;
                    reassigndone = true;
                    wbl2.needdbnreassignment = false;
                }
                if (wbl1.needdbnreassignment)
                {
                    redfs_reassign_new_dbn(wip, wbl1);
                    int pidx = wbl1.myidx_in_myparent();
                    wbl2.set_child_dbn(pidx, wbl1.m_dbn);
                    wbl2.is_dirty = true;
                    reassigndone = true;
                    wbl1.needdbnreassignment = false;
                }
                if (wbl0 != null && wbl0.needdbnreassignment)
                {
                    redfs_reassign_new_dbn(wip, wbl0);
                    int pidx = wbl0.myidx_in_myparent();
                    wbl1.set_child_dbn(pidx, wbl0.m_dbn);
                    wbl1.is_dirty = true;
                    reassigndone = true;
                    wbl0.needdbnreassignment = false;
                }
            }
            else
            {
                DEFS.ASSERT(false, "wrong level");
            }
            if (reassigndone == true)
            {
                //DEFS.DEBUG("DBN-R", "redfs_allocate_new_dbntree, wip = " + wip.m_ino + " sfbn = " + wbl0.m_start_fbn);
                //redfs_show_vvbns(wip, false);
            }
        }

        private void do_fast_read(RedFS_Inode wip, int fbn, byte[] buffer, int offset)
        {
            RedBufL1 wbl1 = (RedBufL1)redfs_load_buf(wip, 1, fbn, true);
            int idx = OPS.myidx_in_myparent(0, fbn);
            int dbn0 = wbl1.get_child_dbn(idx);
            mvdisk.read(wip, dbn0, buffer, offset);        
        }

        //does not work correctly yet, but very fast > 60 Mbps
        private void do_fast_write(RedFS_Inode wip, int fbn, byte[] buffer, int offset)
        {
            RedBufL1 wbl1 = (RedBufL1)redfs_load_buf(wip, 1, fbn, true);
            
            int idx = OPS.myidx_in_myparent(0, fbn);
            int dbn0 = wbl1.get_child_dbn(idx);

            if (dbn0 != 0 || dbn0 != DBN.INVALID)
            {
                refcntmgr.decrement_refcount_ondealloc(wip.get_filefsid(), dbn0);
            }

            redfs_allocate_new_dbntree(wip, null, fbn);
            dbn0 = refcntmgr.allocate_dbn(BLK_TYPE.REGULAR_FILE_L0, 0);
            refcntmgr.increment_refcount_onalloc(wip.get_filefsid(), dbn0);

            wbl1.set_child_dbn(idx, dbn0);
            mvdisk.write(wip, fbn, dbn0, buffer, offset);
        }

        private int do_io_internal(REDFS_OP type, RedFS_Inode wip, long fileoffset, byte[] buffer, int boffset, int blength)
        {
            //DEFS.DEBUG("WOP", type + "(ino, fileoffset, length) = (" + wip.m_ino + "," + fileoffset + "," + blength + ")");
            
            DEFS.ASSERT(blength <= buffer.Length && ((buffer.Length - boffset) >= blength),
                                    "Overflow detected in do_io_internal type = " + type);

            if (wip.get_filesize() < (fileoffset + blength))
            {
                DEFS.ASSERT(wip.get_ino() == 0 || type == REDFS_OP.REDFS_WRITE, "Cannot grow wip for " +
                        "read operations, except for inofile. wip.ino = " + wip.get_ino() + " and type = " + type);
                redfs_resize_wip(wip, (fileoffset + blength), false);
            }

            int buffer_start = boffset;
            int buffer_end = boffset + blength;

            while (buffer_start < buffer_end)
            {
                int wboffset = (int)(fileoffset % 4096);
                int copylength = ((4096 - wboffset) < (buffer_end - buffer_start)) ?
                                        (4096 - wboffset) : (buffer_end - buffer_start);

                // Do dummy read if the write is a full write 
                int fbn = OPS.OffsetToFBN(fileoffset);

                bool fullwrite = (copylength == 4096 && type == REDFS_OP.REDFS_WRITE && 
                        wip.get_wiptype() != WIP_TYPE.PUBLIC_INODE_FILE) ? true : false;
                bool fullread = (copylength == 4096 && type == REDFS_OP.REDFS_READ && 
                        wip.get_wiptype() != WIP_TYPE.PUBLIC_INODE_FILE) ? true : false;

                if (fullwrite && wip.get_inode_level() > 0 && 
                            (wip.iohistory == 0xFFFFFFFFFFFFFFFF) &&
                            (wip.get_wiptype() != WIP_TYPE.PUBLIC_INODE_FILE) && 
                            (OPS.get_buf3("TEST", wip, 0, fbn, true) == null))
                {
                    do_fast_write(wip, fbn, buffer, buffer_start);
                    buffer_start += copylength;
                    wboffset += copylength;
                    fileoffset += copylength;
                    continue;
                }
                else if (fullread && wip.get_inode_level() > 0 && 
                            (wip.iohistory == 0) &&
                            (wip.get_wiptype() != WIP_TYPE.PUBLIC_INODE_FILE) && 
                            (OPS.get_buf3("TEST", wip, 0, fbn, true) == null))
                {
                    do_fast_read(wip, fbn, buffer, buffer_start);
                    buffer_start += copylength;
                    wboffset += copylength;
                    fileoffset += copylength;
                    continue;
                }

                timer_io_time.start_counter();
                RedBufL0 wbl0 = (RedBufL0)redfs_load_buf(wip, 0, fbn, fullwrite);
                timer_io_time.stop_counter();
                /*
                for (int i = 0; i < copylength; i++)
                {
                    if (type == REDFS_OP.REDFS_WRITE)
                    {
                        wbl0.data[wboffset++] = buffer[buffer_start++];
                    }
                    else
                    {
                        buffer[buffer_start++] = wbl0.data[wboffset++];
                    }
                }
                */
                if (type == REDFS_OP.REDFS_WRITE)
                {
                    Array.Copy(buffer, buffer_start, wbl0.data, wboffset, copylength);
                    buffer_start += copylength;
                    wboffset += copylength;
                    wbl0.is_dirty = true;
                    wbl0.mTimeToLive = 1;
                    redfs_allocate_new_dbntree(wip, wbl0, -1);
                    wip.iohistory = (wip.iohistory << 1) | 0x0000000000000001;
                }
                else
                {
                    Array.Copy(wbl0.data, wboffset, buffer, buffer_start, copylength);
                    buffer_start += copylength;
                    wboffset += copylength;
                    wbl0.mTimeToLive += (wbl0.mTimeToLive < 4)? 1 : 0;
                    wip.iohistory = (wip.iohistory << 1);
                }
                DEFS.ASSERT(wboffset <= 4096 && buffer_start <= (boffset + blength), "Incorrect computation in redfs_io " + type);
                fileoffset += copylength;
            }


            if (type == REDFS_OP.REDFS_WRITE && wip.get_incore_cnt() > 16384)
            {
                sync(wip);
                //flush_cache2(wip);
            }
            if (wip.get_wiptype() == WIP_TYPE.PUBLIC_INODE_FILE)
            {
               // DEFS.DEBUG("WOPINOFILE", type + "(ino, fileoffset, length) = (" + wip.m_ino + "," + fileoffset + "," + blength + ")");
                //This sync also gave me lots of fucking problems. so be careful to commit inowip regularly
                //sync(wip);
            }

            if (timer_io_time.iteration % 512 == 0)
            {
                DEFS.DEBUGCLR("^TIME^", "iotime msec = " + timer_io_time.get_microsecs_avg() + "," + 
                    timer_io_time.get_microsecs_lastop() + ", size = " + blength);
            }
            return blength;
        }

        
        // Below four are externally exposed functions.
        
        public int redfs_write(RedFS_Inode wip, long fileoffset, byte[] buffer, int boffset, int blength)
        {
            return do_io_internal(REDFS_OP.REDFS_WRITE, wip, fileoffset, buffer, boffset, blength);
        }

        public int redfs_read(RedFS_Inode wip, long fileoffset, byte[] buffer, int boffset, int blength)
        {
            return do_io_internal(REDFS_OP.REDFS_READ, wip, fileoffset, buffer, boffset, blength);
        }

        //
        // Below to are externally exposed for dirfile handling only.
        // In case of read, we *must* touch the buffers so that refcounts
        // are propogated downwards by the time we issue deletes.
        // 
        // TODO, propogate refcounts, clear incore buffers.
        //
        public byte[] redfs_read_dirfile(RedFS_Inode wip)
        {
            DEFS.ASSERT(wip.get_wiptype() == WIP_TYPE.DIRECTORY_FILE, "Wrong wiptype for readdirfile : " + wip.get_wiptype());
            DEFS.DEBUG("REDFS", "redfs read_dirfile, wip = " + wip.get_ino() + " size = " + wip.get_filesize());

            byte[] buffer = new byte[wip.get_filesize()];
            do_io_internal(REDFS_OP.REDFS_READ, wip, 0, buffer, 0, (int)wip.get_filesize());

            sync(wip);
            flush_cache(wip, true);
            return buffer;
        }

        
        // Now delete the whole previous file, and recreate a new one here.       
        public void redfs_write_dirfile(int callingfsid, RedFS_Inode wip, byte[] data)
        {
            DEFS.ASSERT(wip.get_wiptype() == WIP_TYPE.DIRECTORY_FILE, "Wrong wiptype for readdirfile : " + wip.get_wiptype());
            DEFS.DEBUG("REDFS", "Redfs write_dirfile 1, wip = " + wip.get_ino() + " size = " + wip.get_filesize());

            redfs_delete_wip(callingfsid, wip, false);
            do_io_internal(REDFS_OP.REDFS_WRITE, wip, 0, data, 0, data.Length);
            DEFS.DEBUG("REDFS", "redfs write_dirfile 2, wip = " + wip.get_ino() + " size = " + wip.get_filesize());
            DEFS.DEBUG("REDFS", wip.get_string_rep2());
            sync(wip);
            flush_cache(wip, true);
        }

        private void redfs_create_backup_fsid()
        {
            RedFS_FSID wbfsid = null;

            if (refcntmgr.check_if_fsidbit_set(2) == false)
            {
                wbfsid = new RedFS_FSID(2, 0, "BACKUP Drive", "NILL", "This drive will contain all your Incremental backups, these backup" +
                    " files are actually your regular files on you disk, that you backup from time to time. ");
            }
            else
            {
                wbfsid = redfs_load_fsid(2, true);
            }

            refcntmgr.set_fsidbit(2);
            mvdisk.write_fsid(wbfsid);
            wbfsid.set_dirty(false);
            DEFS.DEBUG("FSID", "Created a Backup DRIVE");   
        }

        private void redfs_create_lun_fsid()
        {
            RedFS_FSID wbfsid = null;

            if (refcntmgr.check_if_fsidbit_set(1) == false)
            {
                wbfsid = new RedFS_FSID(1, 0, "LUN Drive", "NILL", "This drive will contain all your LUNS, these lun" + 
                    " files are actually raw virtual drives that can be formatted as NTFS/FAT32 and mounted with " + 
                    "the REDFS_Initiator");
            }
            else
            {
                wbfsid = redfs_load_fsid(1, true);
            }

            refcntmgr.set_fsidbit(1);
            mvdisk.write_fsid(wbfsid);
            wbfsid.set_dirty(false);
            DEFS.DEBUG("FSID", "Created a LUN DRIVE");         
        }
        // 
        // redfs_create_zeroed_fsid() is called everytime we come up. If there is already
        //  a zero-drive, we just bail out.
        //
        private void redfs_create_zeroed_fsid()
        {
            RedFS_FSID wbfsid = null;

            if (refcntmgr.check_if_fsidbit_set(0) == false)
            {
                wbfsid = new RedFS_FSID(0, 0, "Zero Drive", "NILL", "You can make clones of this drive and then write " +
                    "data to it. Used this drive to create brand new ZERO filled drives for your data. You may also clone " +
                    "any other drives dervied from the children of this drive.");
            }
            else
            {
                wbfsid = redfs_load_fsid(0, true);
            }

            refcntmgr.set_fsidbit(0);
            mvdisk.write_fsid(wbfsid);
            wbfsid.set_dirty(false);  
            DEFS.DEBUG("FSID", "Created a ZERO DRIVE"); 
        }

        public RedFS_FSID redfs_load_fsid(int fsid, bool rwflag)
        {
            if (refcntmgr.check_if_fsidbit_set(fsid) == false)
            {
                return null;
            }
            else
            {
                RedFS_FSID tmp = mvdisk.read_fsid(fsid);
                if (tmp.ismarked_for_deletion())
                    return null;
                else
                    return mvdisk.read_fsid(fsid);
            }
        }

        public RedFS_FSID redfs_dup_fsid(RedFS_FSID bfs)
        {
            if (bfs.get_dirty_flag())
            {
                DEFS.DEBUG("FSID", "Cannot DUP a fsid which is dirty");
                return null;
            }

            int target = -1;
            for (int i = 0; i < 2048; i++) 
            {
                if (refcntmgr.check_if_fsidbit_set(i) == false) 
                {
                    target = i;
                    break;
                }
            }
            refcntmgr.set_fsidbit(target);
            RedFS_FSID newone = new RedFS_FSID(target, bfs.m_dbn, bfs.get_drive_name(), bfs.data);
            mvdisk.write_fsid(newone);

            // Update refcounts here 
            RedFS_Inode inowip = bfs.get_inode_file_wip("FSID_DUP");
            lock (inowip)
            {
                if (inowip.get_inode_level() == 0)
                { 
                    for (int i=0;i<OPS.NUML0(inowip.get_filesize());i++) 
                    {
                        RedBufL0 wbl0 = (RedBufL0)redfs_load_buf(inowip, 0, i, false);
                        refcntmgr.increment_refcount(bfs.get_fsid(), wbl0, true);
                    }
                }
                else if (inowip.get_inode_level() == 1)
                {
                    for (int i = 0; i < OPS.NUML1(inowip.get_filesize()); i++)
                    {
                        RedBufL1 wbl1 = (RedBufL1)redfs_load_buf(inowip, 1, OPS.PIDXToStartFBN(1, i), false);
                        refcntmgr.increment_refcount(bfs.get_fsid(), wbl1, false);
                    }                
                }
                else if (inowip.get_inode_level() == 2)
                {
                    for (int i = 0; i < OPS.NUML2(inowip.get_filesize()); i++)
                    {
                        RedBufL2 wbl2 = (RedBufL2)redfs_load_buf(inowip, 2, OPS.PIDXToStartFBN(2, i), false);
                        refcntmgr.increment_refcount(bfs.get_fsid(), wbl2, false);
                    }
                }
            }

            DEFS.DEBUG("FSID", "Created a new fsid on "+ target);
            return newone;
        }

        public void redfs_commit_fsid(RedFS_FSID fsidblk)
        {
            fsidblk.sync_internal();
            mvdisk.write_fsid(fsidblk);
            fsidblk.set_dirty(false);
        }

        public void redfs_delete_fsid(RedFS_FSID fsidblk)
        {
            fsidblk.mark_for_deletion();
            mvdisk.write_fsid(fsidblk);
            fsidblk.set_dirty(false);
        }

        //
        // Setting the reserve flag will increment refcount. otherwise it must be for
        // resize type calls where the new inode is generally discarded.
        // 
        public void redfs_create_wip_from_dbnlist(RedFS_Inode wip, int[] dbnlist, bool doreserve)
        {
            DEFS.ASSERT(doreserve == false, "Not implimented yet");
            if (wip.get_inode_level() == 0)
            {
                int c = 0;
                for (int i = 0; i < dbnlist.Length; i++) 
                {
                    wip.set_child_dbn(i, dbnlist[c++]);
                }
            }
            else
            {
                DEFS.ASSERT(dbnlist.Length < (1024 * 16), "size > 64Mb is not yet implimented!");
                int numl1 = OPS.NUML1((long)dbnlist.Length*4096);
                int counter = 0;

                for (int i = 0; i < numl1; i++) 
                {
                    RedBufL1 wbl1 = (RedBufL1)redfs_allocate_buffer(wip.get_filefsid(), BLK_TYPE.REGULAR_FILE_L1, i * 1024, true);
                    wip.set_child_dbn(i, wbl1.m_dbn);
                    int itr = 0;

                    while (counter < dbnlist.Length && itr < 1024)
                    {
                        wbl1.set_child_dbn(itr, dbnlist[counter]);
                        refcntmgr.increment_refcount_onalloc(wip.get_filefsid(), dbnlist[counter]);
                        counter++;
                        itr++;
                    }
                }
            }
        }

        public int redfs_get_file_dbns(RedFS_Inode wip, int[] dbns, int startfbn, int count)
        {
            DEFS.ASSERT(dbns.Length == count, "Please pass correct sizes");
            if (wip.get_inode_level() == 0)
            {
                DEFS.ASSERT(startfbn < 16 && (startfbn + count) < 16, "Out of range request in redfs_get_file_dbns");
                int c = 0;
                for (int i = startfbn; i < count; i++)
                {
                    dbns[c++] = wip.get_child_dbn(i);
                }
                DEFS.ASSERT(c == count, "Mismatch has occurred 1 :" + c + "," + count);
                return c;
            }
            else
            {
                
                // The idea is to bring everything incore.
                 
                int l0cnt = OPS.NUML0(wip.get_filesize());
                DEFS.ASSERT((startfbn + count) <= l0cnt, "Out of range request in redfs_get_file_dbns 2");

                int numl1 = OPS.NUML1(wip.get_filesize());
                int c = 0;
                for (int i = 0; i < numl1; i++)
                {
                    int startfbn_l1 = OPS.PIDXToStartFBN(1, i);
                    RedBufL1 wbl1 = (RedBufL1)redfs_load_buf(wip, 1, startfbn_l1, false);
                    for (int i0 = 0; i0 < 1024; i0++)
                    {
                        int cfbn = startfbn_l1 + i0;
                        if (cfbn >= startfbn && cfbn < (startfbn + count))
                        {
                            dbns[c++] = wbl1.get_child_dbn(i0);
                        }
                    }
                }
                DEFS.ASSERT(c == count, "Mismatch has occurred 2 :" + c + "," + count);
                return c;
            }
        }

        public void redfs_dup_file(RedFS_Inode wip, RedFS_Inode dupwip)
        {
            //shouldnt have any data incore, caller must have cleaned the old file and sync'd the inode file.
            DEFS.ASSERT(wip.get_incore_cnt() == 0, "Must not have incore buffers");
            DEFS.ASSERT(wip.get_wiptype() == WIP_TYPE.REGULAR_FILE, "Cannot dup a non regular file");
            DEFS.ASSERT(wip.is_dirty == false, "Cannot be dirty, this must have been flushed to disk!");
            DEFS.ASSERT(dupwip.get_filesize() == 0, "Dup wip cannot be inited already");

            //do increment refcount for >= L1 files, and for L0 files, just direct increment.
            if (wip.get_inode_level() == 0)
            {
                int numL0 = OPS.NUML0(wip.get_filesize());
                for (int i = 0; i < numL0; i++) 
                {
                    int dbn0 = wip.get_child_dbn(i);
                    refcntmgr.increment_refcount_onalloc(wip.get_filefsid(), dbn0);
                    dupwip.set_child_dbn(i, dbn0);
                }
            }
            else if (wip.get_inode_level() == 1)
            {
                for (int i = 0; i < OPS.NUML1(wip.get_filesize()); i++)
                {
                    RedBufL1 wbl1 = (RedBufL1)redfs_load_buf(wip, 1, OPS.PIDXToStartFBN(1, i), false);
                    refcntmgr.touch_refcount(wbl1, false);
                    refcntmgr.increment_refcount(wip.get_filefsid(), wbl1, false);

                    int dbn1 = wip.get_child_dbn(i);
                    dupwip.set_child_dbn(i, dbn1);
                }
            }
            else
            {
                int numl1s_remaining = OPS.NUML1(wip.get_filesize());

                for (int i2 = 0; i2 < OPS.NUML2(wip.get_filesize()); i2++)
                {
                    RedBufL2 wbl2 = (RedBufL2)redfs_load_buf(wip, 2, OPS.PIDXToStartFBN(2, i2), false);
                    refcntmgr.touch_refcount(wbl2, false);
                    refcntmgr.increment_refcount(wip.get_filefsid(), wbl2, false);

                    int dbn2 = wip.get_child_dbn(i2);
                    dupwip.set_child_dbn(i2, dbn2);                    
                }
            }
            dupwip.is_dirty = true;
            dupwip.set_filesize(wip.get_filesize());
        }

        // Dump the vbns on to the console, works fine. 
        public void redfs_show_vvbns2(RedFS_Inode wip, bool incoreonly)
        {
            DEFS.DEBUG("VBNS", "Printing redfsshowvbns, fsize " + wip.get_filesize());
            wip.sort_buflists();

            if (incoreonly)
            {
                for (int i = 0; i < wip.L2list.Count; i++)
                {
                    RedBufL2 wbl2 = (RedBufL2)wip.L2list.ElementAt(i);
                    DEFS.DEBUG("VBNS", "** " + wbl2.m_dbn + " s[" + wbl2.m_start_fbn + "]");
                }

                for (int i = 0; i < wip.L1list.Count; i++)
                {
                    Red_Buffer wb = wip.L1list.ElementAt(i);
                    DEFS.DEBUG("VBNS", "*       " + wb.get_ondisk_dbn() + " s[" + wb.get_start_fbn() + "]");
                }

                for (int i = 0; i < wip.L0list.Count; i++)
                {
                    Red_Buffer wb = wip.L0list.ElementAt(i);
                    DEFS.DEBUG("VBNS", "               " + wip.L0list.ElementAt(i).get_ondisk_dbn() + " s[" + wb.get_start_fbn() + "]");
                }
            }
            else
            {
                int numl2 = OPS.NUML2(wip.get_filesize());
                int numl1 = OPS.NUML1(wip.get_filesize());
                int numl0 = OPS.NUML0(wip.get_filesize());

                string wipstr = (wip.get_wiptype() == WIP_TYPE.PUBLIC_INODE_FILE) ? "INOWIP" : (" " + wip.get_ino());

                DEFS.DEBUG("REDFS", "redfs show vbns (wip = " + wipstr + " ) disk = " + numl2 + "," + numl1 + "," + numl0);

                for (int i = 0; i < numl2; i++)
                {
                    Red_Buffer wbl2 = redfs_load_buf(wip, 2, OPS.PIDXToStartFBN(2, i), false);
                    int refc =0, chdc = 0;
                    refcntmgr.get_refcnt_info(wbl2.get_ondisk_dbn(), ref refc, ref chdc);
                    DEFS.DEBUG("VBNS", "** " + wbl2.get_ondisk_dbn()  + " (2- " + i + ") " + refc + "," + chdc);
                }

                for (int i = 0; i < numl1; i++)
                {
                    Red_Buffer wbl1 = redfs_load_buf(wip, 1, OPS.PIDXToStartFBN(1, i), false);
                    int refc = 0, chdc = 0;
                    refcntmgr.get_refcnt_info(wbl1.get_ondisk_dbn(), ref refc, ref chdc);
                    DEFS.DEBUG("VBNS", "*       " + wbl1.get_ondisk_dbn() + " (1- " + i + ") " + refc + "," + chdc);
                }

                if (wip.get_inode_level() == 0)
                {
                    for (int i = 0; i < numl0; i++) 
                    {
                        int refc = 0, chdc = 0;
                        refcntmgr.get_refcnt_info(wip.get_child_dbn(i), ref refc, ref chdc);
                        DEFS.DEBUG("VBNS", "               " + wip.get_child_dbn(i) + " (0- " + i + ") " + refc + "," + chdc);
                    }
                }
                else
                {
                    for (int i = 0; i < numl1; i++)
                    {
                        RedBufL1 wbl1 = (RedBufL1)redfs_load_buf(wip, 1, OPS.PIDXToStartFBN(1, i), false);
                        for (int i0 = 0; i0 < 1024; i0++)
                        {
                            if (wbl1.get_child_dbn(i0) < 0) break;
                            int refc = 0, chdc = 0;
                            refcntmgr.get_refcnt_info(wbl1.get_child_dbn(i0), ref refc, ref chdc);
                            DEFS.DEBUG("VBNS", "               " + wbl1.get_child_dbn(i0) + " (0- " + i0 + ") " + refc + "," + chdc);
                        }
                    }
                }
            }
        }

        /*
         * This can block until everything is undone, May take a long time,
         * and is usually throttled to let other updates go through quickly.
         */ 
        public bool dedupe_disksnapshot_undo(string snapfile)
        {
            GLOBALQ.disk_snapshot_mode_enabled = false;
            GLOBALQ.disk_snapshot_optype = REFCNT_OP.UNDEFINED;
            GLOBALQ.snapshotfile.Flush();
            GLOBALQ.snapshotfile.Close();
            GLOBALQ.snapshotfile = null;
            return false;
        }

        private bool redfs_take_disk_snapshot(string snapfile)
        {
            DEFS.ASSERT(GLOBALQ.snapshotfile == null && GLOBALQ.disk_snapshot_mode_enabled == false,
                    "Incorrect start point for redfs_take_disk_snapshot");

            GLOBALQ.snapshotfile = new FileStream(snapfile, FileMode.OpenOrCreate);
            GLOBALQ.disk_snapshot_optype = REFCNT_OP.TAKE_DISK_SNAPSHOT;
            GLOBALQ.disk_snapshot_mode_enabled = true;

            //now start sending messages in loop.
            for (int r = 0; r < 4194304; r++)
            {
                refcntmgr.snapshotscanner(r, true);
            }
            refcntmgr.sync_blocking();

            GLOBALQ.disk_snapshot_mode_enabled = false;
            for (int i = 0; i < GLOBALQ.disk_snapshot_map.Length; i++) 
            {
                DEFS.ASSERT(GLOBALQ.disk_snapshot_map[i] == true, "Some check failed verify " + i);
                GLOBALQ.disk_snapshot_map[i] = false;
            }
            GLOBALQ.disk_snapshot_optype = REFCNT_OP.UNDEFINED;
            GLOBALQ.snapshotfile.Flush();
            GLOBALQ.snapshotfile.Close();
            GLOBALQ.snapshotfile = null;
            return true;
        }

        private bool redfs_undo_disk_snapshot(string snapfile)
        {
            DEFS.ASSERT(GLOBALQ.snapshotfile == null && GLOBALQ.disk_snapshot_mode_enabled == false,
                    "Incorrect start point for redfs_take_disk_snapshot");

            GLOBALQ.snapshotfile = new FileStream(snapfile, FileMode.Open);
            GLOBALQ.disk_snapshot_optype = REFCNT_OP.UNDO_DISK_SNAPSHOT;
            GLOBALQ.disk_snapshot_mode_enabled = true;

            
            //now start sending messages in loop.
            for (int r = 0; r < 4194304; r++) 
            {
                refcntmgr.snapshotscanner(r, false);
            }

            GLOBALQ.disk_snapshot_mode_enabled = false;
            for (int i = 0; i < GLOBALQ.disk_snapshot_map.Length; i++)
            {
                DEFS.ASSERT(GLOBALQ.disk_snapshot_map[i] == true, "Some check failed 2343");
                GLOBALQ.disk_snapshot_map[i] = false;
            }
            GLOBALQ.disk_snapshot_optype = REFCNT_OP.UNDEFINED;
            GLOBALQ.snapshotfile.Flush();
            GLOBALQ.snapshotfile.Close();
            GLOBALQ.snapshotfile = null;
             
            return true;
        }

        //
        // Dedupe code from here onwards, no user io is possible in this case, wip must be passed for the
        // recipient and dbn must be specified for the donor, a bcmp is done before dedupe. Dedupe order
        // is very delicate and both caller and we must handle all scenarios correctly. sourcedbn must also
        // match.
        //
        public DEDUP_RETURN_CODE redfs_do_blk_sharing(RedFS_Inode rwip, int recipientfbn, int sourcedbn, int donordbn)
        {
            //DEFS.DEBUG("DEDUPE", "ino, fbn, dbn = (" + rwip.get_ino() + "," + recipientfbn + "," + sourcedbn + ") -> " + donordbn);

            //this is temporary, this must also be handled correctly.
            if (OPS.get_buf3("dedupe", rwip, 0, recipientfbn, true) != null)
            {
                return DEDUP_RETURN_CODE.DEDUP_BUFINCORE;
            }

            if (rwip.get_inode_level() == 0)
            {
                int fsourcedbn = rwip.get_child_dbn(recipientfbn);
                if (fsourcedbn != sourcedbn) 
                {
                    return DEDUP_RETURN_CODE.DEDUP_SOURCE_DBN_HAS_CHANGED;
                }

                int refcnt = 0, childcnt = 0;
                refcntmgr.get_refcnt_info(fsourcedbn, ref refcnt, ref childcnt);
                DEFS.ASSERT(childcnt == 0, "Cannot be >0 for a L0 dbn in a 0-leve wip");
                DEFS.ASSERT(refcnt > 0, "Disklayer snapshot shouldve ensured this is present.");

                //We assume that the upper layer has done bcompare for us.
                //redfs_show_vvbns2(rwip, false);
                refcntmgr.decrement_refcount_ondealloc(rwip.get_filefsid(), fsourcedbn);
                rwip.set_child_dbn(recipientfbn, donordbn);
                refcntmgr.increment_refcount_onalloc(rwip.get_filefsid(), donordbn);

                //DEFS.DEBUGYELLOW("DEDUPE", "Sucess2 :: ino, fbn = (" + rwip.get_ino() + "," + recipientfbn + ") -> " + donordbn + ", ref = " + refcnt);
                //redfs_show_vvbns2(rwip, false);
                rwip.is_dirty = true;
                return DEDUP_RETURN_CODE.DEDUP_DONE_INOFBN;
            }
            else if (rwip.get_inode_level() >= 1) 
            {
                RedBufL1 wbl1 = (RedBufL1)redfs_load_buf(rwip, 1, recipientfbn, false);
                int idx = OPS.myidx_in_myparent(0, recipientfbn);
                int fsourcedbn = wbl1.get_child_dbn(idx);

                if (fsourcedbn != sourcedbn || fsourcedbn == donordbn)
                {
                    return DEDUP_RETURN_CODE.DEDUP_SOURCE_DBN_HAS_CHANGED;
                }
/*
 * imp code - commented just for testing
                int refcnt1 = 0, childcnt1 = 0;
                int wbl1dbnbefore = wbl1.m_dbn;
                refcntmgr.get_refcnt_info(wbl1dbnbefore, ref refcnt1, ref childcnt1);
                DEFS.ASSERT(childcnt1 == 0 && refcnt1 >= 1, "Cannot be >0 for a L1 dbn in a 1-level " +
                        "wip (prop shouldve been done) " + childcnt1 + "," + refcnt1);

                int refcnt0 = 0, childcnt0 = 0;
                refcntmgr.get_refcnt_info(fsourcedbn, ref refcnt0, ref childcnt0);
                DEFS.ASSERT(childcnt0 == 0 && refcnt0 >= 1, "Cannot be >0 for a L0 dbn in a 0-level buffer " +
                    childcnt0 + "," + refcnt0 + " fsourcedbn=" + fsourcedbn + " wbl1.m_dbn = " + wbl1.m_dbn);
*/
                //
                //Now observe the fact the the refcount of the L1 can never be greater than that of L0, or else
                //free path will result in some corruption.
                //It is perfectly fine if both are equal!.
                //
                //redfs_show_vvbns2(rwip, false);
                //DEFS.ASSERT(refcnt1 <= refcnt0, "Some corruption in refcounts has happened (" + wbl1.m_dbn + "," + refcnt1 + "," + 
                //    childcnt1 + ") , (" + fsourcedbn + "," + refcnt0 + "," + childcnt0 + ")");

                // Currently without any bcompare
                wbl1.set_child_dbn(idx, donordbn);

                //
                //there are lot of issues with directly updating ondisk data, we need incore buf sharing to get
                //this working correctly. If wbl1 of child in some derived clone is loaded, then that copy automatically
                //becomes stale and we end up creating a memory leak here.
                //
                //mvdisk.write(rwip, wbl1); //commit to disk, and clean so that the wbl1 does not get realloced!!

                //for (int i = 0; i < refcnt0; i++)
                //{
                    refcntmgr.increment_refcount_onalloc(rwip.get_filefsid(), donordbn); //k times
                    refcntmgr.decrement_refcount_ondealloc(rwip.get_filefsid(), fsourcedbn); //ktimes
                //}
                //redfs_show_vvbns2(rwip, false);
                rwip.is_dirty = true; 
                //DEFS.DEBUG("DEDUP", "Suceeded in " + fsourcedbn + "->" + donordbn + " loops = " + 1/*refcnt0*/);
                DEFS.ASSERT(wbl1.get_child_dbn(idx) == donordbn, "This should be fixed now");
                return DEDUP_RETURN_CODE.DEDUP_DONE_INOFBN;
            }

            DEFS.ASSERT(false, "Something bad happened in dedupe_do_blk_sharing");
            return DEDUP_RETURN_CODE.DEDUP_UNKNOWN;
        }
    } //redfsdrive
}
