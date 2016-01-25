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

namespace redfs_v2
{
    public class DBN
    {
        public static int INVALID = -1;
    };

    public enum BLK_TYPE
    {
        FSID_BLOCK,
        PUBLIC_INODE_FILE_L0,
        REGULAR_FILE_L2,
        REGULAR_FILE_L1,
        REGULAR_FILE_L0,
        IGNORE
    };

    public interface Red_Buffer
    {
        int get_level();
        BLK_TYPE get_blk_type();
        void data_to_buf(byte[] data);
        byte[] buf_to_data(); /* for reading into, and out*/
        int get_ondisk_dbn();

        long get_start_fbn();
        void set_start_fbn(long fbn);
        void set_dirty(bool flag);

        /*
         * This can be used in refcount adjustment logic.
         */
        bool does_exist_ondisk();
        void set_ondisk_exist_flag(bool value);

        bool get_dbn_reassignment_flag();
        void set_dbn_reassignment_flag(bool v);

        bool get_touchrefcnt_needed();
        void set_touchrefcnt_needed(bool v);
    }

    
    public class REDFS_BUFFER_ENCAPSULATED
    {
        Red_Buffer mwb;
        public REDFS_BUFFER_ENCAPSULATED(Red_Buffer wb)
        {
            mwb = wb;
        }

        public int get_child_dbn(int idx)
        {
            switch (mwb.get_blk_type())
            {
                case BLK_TYPE.REGULAR_FILE_L1:
                    return ((RedBufL1)mwb).get_child_dbn(idx);
                case BLK_TYPE.REGULAR_FILE_L2:
                    return ((RedBufL2)mwb).get_child_dbn(idx);
            }
            return DBN.INVALID;
        }

        public int get_dbn()
        {
            switch (mwb.get_blk_type())
            {
                case BLK_TYPE.REGULAR_FILE_L1:
                    return ((RedBufL1)mwb).m_dbn;
                case BLK_TYPE.REGULAR_FILE_L2:
                    return ((RedBufL2)mwb).m_dbn;
            }
            return DBN.INVALID;
        }

        public void set_dbn(int dbn)
        {
            switch (mwb.get_blk_type())
            {
                case BLK_TYPE.REGULAR_FILE_L0:
                    ((RedBufL0)mwb).m_dbn = dbn;
                    break;
                case BLK_TYPE.REGULAR_FILE_L1:
                    ((RedBufL1)mwb).m_dbn = dbn;
                    break;
                case BLK_TYPE.REGULAR_FILE_L2:
                    ((RedBufL2)mwb).m_dbn = dbn;
                    break;
            }
        }
    }

    public class RedBufL0 : Red_Buffer
    {
        public byte[] data = new byte[4096];
        public bool is_dirty;
        public int m_dbn;
        public long m_start_fbn;
        private bool m_exists_ondisk;

        private long creation_time;
        public int mTimeToLive = 6;
        public bool needdbnreassignment;
        public bool needtouchbuf = true;

        public RedBufL0(long sf) 
        {   
            m_start_fbn = sf; 
            creation_time = DateTime.Now.ToUniversalTime().Ticks;
            needdbnreassignment = true;
            needtouchbuf = true;
        }

        int Red_Buffer.get_level() { return 0; }
        BLK_TYPE Red_Buffer.get_blk_type() { return BLK_TYPE.REGULAR_FILE_L0; }
        void Red_Buffer.data_to_buf(byte[] data) { }
        byte[] Red_Buffer.buf_to_data() { return data; }
        int Red_Buffer.get_ondisk_dbn() { return m_dbn; }
        long Red_Buffer.get_start_fbn() { return m_start_fbn; }
        void Red_Buffer.set_start_fbn(long fbn) { m_start_fbn = fbn; }
        void Red_Buffer.set_dirty(bool flag) { is_dirty = flag; }
        bool Red_Buffer.does_exist_ondisk() { return m_exists_ondisk; }
        void Red_Buffer.set_ondisk_exist_flag(bool value) { m_exists_ondisk = value; }
        bool Red_Buffer.get_dbn_reassignment_flag() { return needdbnreassignment; }
        void Red_Buffer.set_dbn_reassignment_flag(bool v) { needdbnreassignment = v; }
        bool Red_Buffer.get_touchrefcnt_needed() { return needtouchbuf; }
        void Red_Buffer.set_touchrefcnt_needed(bool v) { needtouchbuf = v; }

        public void touch() { creation_time = DateTime.Now.ToUniversalTime().Ticks; }
        public bool isTimetoClear() 
        {
            long curr = DateTime.Now.ToUniversalTime().Ticks;
            int seconds = (int)((curr - creation_time) / 10000000);

            if (seconds > mTimeToLive) return true;
            else return false;        
        }
        public int myidx_in_myparent() { return (int)(m_start_fbn % 1024); }

        public void reinitbuf(long sf)
        {
            m_start_fbn = sf;
            creation_time = DateTime.Now.ToUniversalTime().Ticks;
            needdbnreassignment = true;
            needtouchbuf = true;        
            is_dirty = false;
            m_dbn = 0;
            m_exists_ondisk = false;
            mTimeToLive = 1;
            Array.Clear(data, 0, 4096);
        }
    }

    public class RedBufL1 : Red_Buffer
    {
        public byte[] data = new byte[4096];
        public bool is_dirty;
        public int m_dbn;
        public long m_start_fbn;
        private bool m_exists_ondisk;

        private long creation_time;
        private int mTimeToLive = 6;
        public bool needdbnreassignment;
        public bool needtouchbuf = true;

        public RedBufL1(long sf) 
        { 
            m_start_fbn = sf;
            for (int i = 0; i < 1024; i++)
            {
                set_child_dbn(i, DBN.INVALID);
            }
            creation_time = DateTime.Now.ToUniversalTime().Ticks;
            needdbnreassignment = true;
            needtouchbuf = true;
        }
        int Red_Buffer.get_level() { return 1; }
        BLK_TYPE Red_Buffer.get_blk_type() { return BLK_TYPE.REGULAR_FILE_L1; }
        void Red_Buffer.data_to_buf(byte[] d) 
        {
            //fuck, didnt code this till 24th feb 2013!!.
            for (int i = 0; i < 4096; i++)
            {
                data[i] = d[i];
            }
        }
        byte[] Red_Buffer.buf_to_data() { return data; }
        int Red_Buffer.get_ondisk_dbn() { return m_dbn; }
        long Red_Buffer.get_start_fbn() { return m_start_fbn; }
        void Red_Buffer.set_start_fbn(long fbn) { m_start_fbn = fbn; }
        void Red_Buffer.set_dirty(bool flag) { is_dirty = flag; }
        bool Red_Buffer.does_exist_ondisk() { return m_exists_ondisk; }
        void Red_Buffer.set_ondisk_exist_flag(bool value) { m_exists_ondisk = value; }
        bool Red_Buffer.get_dbn_reassignment_flag() { return needdbnreassignment; }
        void Red_Buffer.set_dbn_reassignment_flag(bool v) { needdbnreassignment = v; }
        bool Red_Buffer.get_touchrefcnt_needed() { return needtouchbuf; }
        void Red_Buffer.set_touchrefcnt_needed(bool v) { needtouchbuf = v; }

        public void touch() { creation_time = DateTime.Now.ToUniversalTime().Ticks; }
        public bool isTimetoClear()
        {
            long curr = DateTime.Now.ToUniversalTime().Ticks;
            int seconds = (int)((curr - creation_time) / 10000000);

            if (seconds > mTimeToLive) return true;
            else return false;
        }

        public int myidx_in_myparent() { return (int)((m_start_fbn % (1024 * 1024)) / 1024); }

        public int get_child_dbn(int idx)
        {
            lock (data)
            {
                return OPS.get_dbn(data, idx);
            }
        }

        public void set_child_dbn(int idx, int dbn)
        {
            lock (data)
            {
                is_dirty = true;
                OPS.set_dbn(data, idx, dbn);
            }
        }
    }

    /*
     * Regular file L2 indirect buffer.
     */
    public class RedBufL2 : Red_Buffer
    {
        public byte[] data = new byte[4096];
        public bool is_dirty;
        public int m_dbn;
        public long m_start_fbn;
        private bool m_exists_ondisk;

        private long creation_time;
        private int mTimeToLive = 6;
        public bool needdbnreassignment;
        public bool needtouchbuf = true;

        public Red_Buffer[] m_chdptr = new Red_Buffer[1024];

        public RedBufL2(long sf) 
        { 
            m_start_fbn = sf;
            for (int i = 0; i < 1024; i++)
            {
                set_child_dbn(i, DBN.INVALID);
            }
            creation_time = DateTime.Now.ToUniversalTime().Ticks;
            needdbnreassignment = true;
        }
        int Red_Buffer.get_level() { return 2; }
        BLK_TYPE Red_Buffer.get_blk_type() { return BLK_TYPE.REGULAR_FILE_L2; }
        void Red_Buffer.data_to_buf(byte[] d) 
        { 
            //fuck, didnt code this till 24th feb 2013!!.
            for (int i = 0; i < 4096; i++) 
            {
                data[i] = d[i];
            }
        }
        byte[] Red_Buffer.buf_to_data() { return data; }
        int Red_Buffer.get_ondisk_dbn() { return m_dbn; }
        long Red_Buffer.get_start_fbn() { return m_start_fbn; }
        void Red_Buffer.set_start_fbn(long fbn) { m_start_fbn = fbn; }
        void Red_Buffer.set_dirty(bool flag) { is_dirty = flag; }
        bool Red_Buffer.does_exist_ondisk() { return m_exists_ondisk; }
        void Red_Buffer.set_ondisk_exist_flag(bool value) { m_exists_ondisk = value; }
        bool Red_Buffer.get_dbn_reassignment_flag() { return needdbnreassignment; }
        void Red_Buffer.set_dbn_reassignment_flag(bool v) { needdbnreassignment = v; }
        bool Red_Buffer.get_touchrefcnt_needed() { return needtouchbuf; }
        void Red_Buffer.set_touchrefcnt_needed(bool v) { needtouchbuf = v; }

        public void touch() { creation_time = DateTime.Now.ToUniversalTime().Ticks; }
        public bool isTimetoClear()
        {
            long curr = DateTime.Now.ToUniversalTime().Ticks;
            int seconds = (int)((curr - creation_time) / 10000000);

            if (seconds > mTimeToLive) return true;
            else return false;
        }

        public int myidx_in_myparent() { return (int)(m_start_fbn/(1024*1024));}

        public int get_child_dbn(int idx)
        {
            lock (data)
            {
                return OPS.get_dbn(data, idx);
            }
        }
        public void set_child_dbn(int idx, int dbn)
        {
            lock (data)
            {
                is_dirty = true;
                OPS.set_dbn(data, idx, dbn);
            }
        }

        public int get_numchildptrs_incore()
        {
            int count = 0;
            for (int i = 0; i < 1024; i++) 
            {
                if (m_chdptr[i] != null)
                {
                    RedBufL1 wbl1 = (RedBufL1)m_chdptr[i];
                    DEFS.ASSERT(wbl1.m_dbn == get_child_dbn(i), "Mismatch in get_incore L2 () 2@ " + i);
                    count++;
                }
            }
            return count;
        }
    }
}
