using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;

namespace redfs_v2
{
    public class wrcomparator : IComparer
    {
        int IComparer.Compare(object obj1, object obj2)
        {
            WRBuf w1 = (WRBuf)obj1;
            WRBuf w2 = (WRBuf)obj2;

            if (w1.get_buf_age() > w2.get_buf_age())
                return -1;
            else if (w1.get_buf_age() > w2.get_buf_age())
                return 1;
            return 0;
        }
    }

    public enum REFCNT_OP
    {
        UNDEFINED,
        INCREMENT_REFCOUNT_ALLOC,
        INCREMENT_REFCOUNT,
        DECREMENT_REFCOUNT_ONDEALLOC,
        DECREMENT_REFCOUNT,
        TOUCH_REFCOUNT,
        GET_REFANDCHD_INFO,
        DO_SYNC,
        TAKE_DISK_SNAPSHOT,
        UNDO_DISK_SNAPSHOT,
        SHUT_DOWN
    }

    public class REFDEF
    {
        public static int dbn_to_rbn(int dbn) { return dbn / 512; }
        public static int dbn_to_ulist_idx(int dbn) { return (dbn % 512); }
        public static int dbn_to_smbit(int dbn) { return (dbn / (1024 * 1024)); }
        //public static Int16 parse_flag(byte[] data, int idx) { return 0; }
        //public static void set_flag(byte[] data, int idx, Int16 flag) { }
        public static string get_string_rep(UpdateReqI cu)
        {
            return "dbn, blk, op, value, tfbn = " + cu.dbn + "," + cu.blktype + 
                "," + cu.optype + "," + cu.value + "," + cu.tfbn;
        }

        public static bool snapshot_getbit(int offset, byte[] data)
        {

            return false;
        }
        public static void snapshot_setbit(int offset, byte[] data, bool value)
        { 
        
        
        }
    }

    /*
     * 8 byte format:-
     * 4 bytes -> refcount of the block.
     * 2 bytes -> refcoudn that should be propogated downward
     * 1 byte -> 1-dedupe overwritten flag.
     *           1-
     * 1 bytes  -> TBD
     */
    public class WRBuf
    {
        private long m_creation_time;
        private int start_dbn;

        public byte[] data = new byte[4096];
        public bool is_dirty;
        public int m_rbn;

        public WRBuf(int r)
        {
            m_rbn = r;
            start_dbn = r * 512;
            m_creation_time = DateTime.Now.ToUniversalTime().Ticks;
        }
        public void reinit(int r)
        {
            m_rbn = r;
            start_dbn = r * 512;
            m_creation_time = DateTime.Now.ToUniversalTime().Ticks;        
        }

        public int get_buf_age()
        {
            long elapsed = (DateTime.Now.ToUniversalTime().Ticks - m_creation_time);
            return (int)(elapsed / 10000000);
        }

        public void touch_buf()
        {
            m_creation_time = DateTime.Now.ToUniversalTime().Ticks;
        }

        public void set_refcount(int dbn, int value)
        {
            lock (data)
            {
                touch_buf();
                int offset = dbn % 512 * 8 + 0;
                DEFS.ASSERT(dbn >= start_dbn && dbn < start_dbn + 512,
                    "Wrong dbn range in VBuf " + dbn + "," + start_dbn +
                    "," + m_rbn);

                byte[] val = BitConverter.GetBytes(value);
                data[offset] = val[0];
                data[offset + 1] = val[1];
                data[offset + 2] = val[2];
                data[offset + 3] = val[3];
                is_dirty = true;
            }
        }

        public void set_childcount(int dbn, int value)
        {
            lock (data)
            {
                touch_buf();
                int offset = dbn % 512 * 8 + 4;
                DEFS.ASSERT(dbn >= start_dbn && dbn < start_dbn + 512,
                    "Wrong dbn range in VBuf 3" + dbn + "," +
                    start_dbn + "," + m_rbn);

                byte[] val = BitConverter.GetBytes((Int16)value);
                data[offset] = val[0];
                data[offset + 1] = val[1];
                is_dirty = true;
            }
        }

        public int get_refcount(int dbn)
        {
            lock (data)
            {
                touch_buf();

                DEFS.ASSERT(dbn >= start_dbn && dbn < start_dbn + 512,
                    "Wrong dbn range in VBuf2 " + dbn + "," + start_dbn +
                    "," + m_rbn);

                int offset = dbn % 512 * 8 + 0;
                return BitConverter.ToInt32(data, offset);
            }
        }
        public int get_childcount(int dbn)
        {
            lock (data)
            {
                touch_buf();
                DEFS.ASSERT(dbn >= start_dbn && dbn < start_dbn + 512,
                    "Wrong dbn range in VBuf3 " + dbn + "," + start_dbn +
                    "," + m_rbn);

                int offset = dbn % 512 * 8 + 4;
                return BitConverter.ToInt16(data, offset);
            }
        }

        public bool get_dedupe_overwritten_flag(int dbn)
        {
            lock (data)
            {
                int offset = dbn % 512 * 8 + 6;
                byte bvale = data[offset];
                if ((bvale & 0x01) != 0) return true;
                return false;
            }
        }

        public void set_dedupe_overwritten_flag(int dbn, bool flag)
        {
            lock (data)
            {
                int offset = dbn % 512 * 8 + 6;
                data[offset] = (flag)? ((byte)(data[offset] | 0x01)): (byte)(data[offset] & 0xFE);
                is_dirty = true;
            }
        }

        /*
        public BLK_TYPE get_blk_type2(int dbn)
        {
            lock (data)
            {
                touch_buf();
                DEFS.ASSERT(dbn >= start_dbn && dbn < start_dbn + 512,
                    "Wrong dbn range in VBuf3 " + dbn + "," + start_dbn +
                    "," + m_rbn);

                int offset = dbn % 512 * 8 + 6;
                int t = (int)data[offset];
                return t + BLK_TYPE.FSID_BLOCK;
            }
        }

        public void set_blk_type2(int dbn, BLK_TYPE type)
        {
            lock (data)
            {
                touch_buf();
                DEFS.ASSERT(dbn >= start_dbn && dbn < start_dbn + 512,
                    "Wrong dbn range in VBuf3 " + dbn + "," + start_dbn +
                    "," + m_rbn);

                int offset = dbn % 512 * 8 + 6;
                data[offset] = (byte)(type);
                is_dirty = true;
            }
        }
         */
    }

    public class UpdateReqI
    {
        public REFCNT_OP optype;
        public BLK_TYPE blktype;
        public int fsid;
        public int dbn;
        public Int16 value; /* -1/+1 always */
        public int tfbn; /* the fbn of the transaction file */
        public bool processed;
        public bool deleted_sucessfully;
    }

    public class WRContainer
    {
        /* Allocated on demand */
        public WRBuf incoretbuf;
    }

    public class GLOBALQ
    {
        public static BlockingCollection<UpdateReqI> m_reqi_queue = new BlockingCollection<UpdateReqI>(4194304);

        public static List<int> m_deletelog2 = new List<int>();
        public static WRContainer[] WRObj = new WRContainer[4194304];
        public bool[] clear_dedup_flag = new bool[4194304];

        /*
         * Below entries are for snapshot before starting dedupe so as to
         * protect dbns from being overwritten.
         */ 
        public static bool disk_snapshot_mode_enabled = false;
        public static bool[] disk_snapshot_map = new bool[4194304];
        public static REFCNT_OP disk_snapshot_optype;
        public static FileStream   snapshotfile = null;
    }
}
