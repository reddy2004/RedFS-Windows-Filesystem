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

namespace redfs_v2
{
    public class REFCntManager
    {
        private bool m_shutdown = false;
        private Map256M m_dbnbitmap;
        private WRLoader m_wrloader;

        public REFCntManager()
        {
            m_dbnbitmap = new Map256M("allocationmap");
            m_wrloader = new WRLoader();
            m_wrloader.init();
        }

        public bool is_block_free(int dbn)
        {
            return m_dbnbitmap.is_block_free(dbn);
        }

        public int get_total_inuse_blocks()
        {
            return m_dbnbitmap.USED_BLK_COUNT;
        }

        public bool shut_down()
        {
            DEFS.DEBUG("SHUTDOWN", "Calling REFCntManager() shut down");
            m_shutdown = true;
            m_dbnbitmap.shut_down();
            m_wrloader.shut_down();
            while (m_wrloader.m_initialized == true)
            {
                System.Threading.Thread.Sleep(100);
            }
            DEFS.DEBUG("SHUTDOWN", "Finishing REFCntManager() shut down");
            return true;
        }

        public void set_fsidbit(int fsid)
        {
            m_dbnbitmap.fsid_setbit(fsid);
        }

        public bool check_if_fsidbit_set(int fsid)
        {
            return m_dbnbitmap.fsid_checkbit(fsid);
        }

        /*
         * Below three functions are exposed for public.
         * MP Safe function, can take long if there 
         * are too many updates to be made.
         */
        public void touch_refcount(Red_Buffer wb, bool isinodefilel0)
        {
            DEFS.ASSERT(wb != null, "touch refcount needs the wb");

            if (wb.get_touchrefcnt_needed() == false)// || wb.get_ondisk_dbn() == 0)
            {
                return;
            }
            else
            {
                //DEFS.DEBUG("-REF-", "CTH refcount for dbn = " + wb.get_ondisk_dbn() + " inofile = " + isinodefilel0);
                wb.set_touchrefcnt_needed(false);
            }

            if (wb.get_level() == 0 && isinodefilel0)
            {
                m_wrloader.mod_refcount(0, wb.get_ondisk_dbn(), REFCNT_OP.TOUCH_REFCOUNT, wb, true);
            }
            else
            {
                DEFS.ASSERT(wb.get_level() > 0, "touch_refcount is only for indirects only, except for ino-L0!");
                m_wrloader.mod_refcount(0, wb.get_ondisk_dbn(), REFCNT_OP.TOUCH_REFCOUNT, wb, false);
            }
        }

        public void increment_refcount_onalloc(int fsid, int dbn)
        {
            m_wrloader.mod_refcount(fsid, dbn, REFCNT_OP.INCREMENT_REFCOUNT_ALLOC, null, false);
        }

        public void increment_refcount(int fsid, Red_Buffer wb, bool isinodefilel0)
        {
            m_wrloader.mod_refcount(fsid, wb.get_ondisk_dbn(), REFCNT_OP.INCREMENT_REFCOUNT, wb, isinodefilel0);
        }

        /*
         * MP Safe
         */
        public void decrement_refcount_ondealloc(int fsid, int dbn)
        {
            /*
             * Must correspond to an l0 buffer, otherwise we need the indirect to propogate
             * the refcount downwards. For indirects, use the other one. L0 also can use the
             * other one - no probs.
             * We need this, we dont want to load the actual L0 wb's when deleting!!
             */
            m_wrloader.mod_refcount(fsid, dbn, REFCNT_OP.DECREMENT_REFCOUNT_ONDEALLOC, null, false);
        }

        public void decrement_refcount(int fsid, Red_Buffer wb, bool isinodefilel0)
        {
            if (wb.get_level() == 0 && isinodefilel0)
            {
                m_wrloader.mod_refcount(fsid, wb.get_ondisk_dbn(), REFCNT_OP.DECREMENT_REFCOUNT, wb, isinodefilel0);
            }
            else
            {
                m_wrloader.mod_refcount(fsid, wb.get_ondisk_dbn(), REFCNT_OP.DECREMENT_REFCOUNT, null, false);
            }
        }

        public void get_refcnt_info(int dbn, ref int refcnt, ref int childcnt)
        {
            m_wrloader.get_refcount(dbn, ref refcnt, ref childcnt);
        }

        public void sync_blocking()
        {
            m_dbnbitmap.sync();
            m_wrloader.sync_blocking();
        }

        public void sync()
        {
            m_dbnbitmap.sync();
            m_wrloader.sync2();
        }

        public void snapshotscanner(int idx, bool takesnap)
        {
            m_wrloader.snapshot_scanner_dowork(idx, takesnap);
            Thread.Sleep(10);
        }

        /*
         * Can try to allocate a dbn with preferrene, this is suitable for
         * inplace writes of L0 data.
         */
        public int allocate_dbn(BLK_TYPE btype, int preferreddbn)
        {
            /*
             * First see if it is in the  list.
             * Now search the list.
             */
/*
            lock (GLOBALQ.m_deletelog)
            {
                int count = (GLOBALQ.m_deletelog.Count > 16) ? 16 : GLOBALQ.m_deletelog.Count;

                for (int i = 0; i < count; i++)
                {
                    int dbn = (int)(GLOBALQ.m_deletelog.ElementAt(0));
                    GLOBALQ.m_deletelog.RemoveAt(0);

                    if (dbn == preferreddbn)
                    {
                        return preferreddbn;
                    }
                    else
                    {
                        m_dbnbitmap.free_bit(dbn);
                    }
                }
            }
           
            if (m_dbnbitmap.try_alloc_bit(preferreddbn) == true)
            {
                return preferreddbn;
            }
*/
            
            /*
             * At this point, we have done the delete queue, and picked up the cost.
             * And now we have to allocate some random one and return.
             */
            return m_dbnbitmap.allocate_bit();
        }

        /*
         * this will periodically check if any dbns have become free and free
         * it in the bitmap appropriately.
         */
        private void tServiceThread()
        {
            while (m_shutdown == false)
            {
                for (int i = 0; i < 10; i++)
                {
                    if (m_shutdown)
                    {
                        m_dbnbitmap.sync();
                        return;
                    }
                    System.Threading.Thread.Sleep(1000);
                }

                lock (GLOBALQ.m_deletelog2)
                {
                    int count = GLOBALQ.m_deletelog2.Count;
                    try
                    {
                        for (int i = 0; i < count; i++)
                        {
                            int dbn = (int)(GLOBALQ.m_deletelog2.ElementAt(0));
                            GLOBALQ.m_deletelog2.RemoveAt(0);
                            m_dbnbitmap.free_bit(dbn);
                        }
                        DEFS.DEBUGCLR("FREE", "Deallocated " + count + " dbns in this iteration");
                    }
                    catch (Exception e)
                    { 
                        DEFS.DEBUGYELLOW("EXEPTION", "Caught in dellog : cnt = " + count + " and size = " + 
                            GLOBALQ.m_deletelog2.Count + " e.msg = " + e.Message);
                    }
                }
                m_dbnbitmap.sync();
            }
        }

        public void init()
        {
            Thread tc = new Thread(new ThreadStart(tServiceThread));
            tc.Start();
        }
    }
}
