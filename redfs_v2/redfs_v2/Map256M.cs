using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace redfs_v2
{
    public class MapBuffer
    {
        private long m_creation_time;
        public bool is_dirty;
        public byte[] data = new byte[256 * 1024]; /* 256 kb */

        public MapBuffer() { m_creation_time = DateTime.Now.ToUniversalTime().Ticks; }
        public int get_buf_age()
        {
            long elapsed = (DateTime.Now.ToUniversalTime().Ticks - m_creation_time);
            return (int)(elapsed / 10000000);
        }
        public void touch_buf()
        {
            m_creation_time = DateTime.Now.ToUniversalTime().Ticks;
        }
    }

    public class Map256M
    {
        private FileStream mfile, xfile;
        private MapBuffer[] mbufs = new MapBuffer[1024];

        private int dbn_to_mbufidx(int dbn) { return dbn / 2097152; }
        private int dbn_to_bitidx(int dbn) { return dbn % 8; }
        private int dbn_to_mbufoffset(int dbn) { return (dbn % 2097152) / 8; }
        private void load_mbx(int idx)
        {
            if (mbufs[idx] == null)
            {
                mbufs[idx] = new MapBuffer();
                DEFS.DEBUG("ACTMAP", "Loaded mbx = " + ((long)idx * (256 * 1024)));
                mfile.Seek((long)idx * (256 * 1024), SeekOrigin.Begin);
                mfile.Read(mbufs[idx].data, 0, (256 * 1024));
            }
            mbufs[idx].touch_buf();
        }

        private bool initialized = false;
        public int USED_BLK_COUNT = 0;

        private int start_dbn_single = 1024;
        //private int start_dbn_bulk;

        public Map256M(string name)
        {
            mfile = new FileStream(CONFIG.GetBasePath() + name,
                    FileMode.OpenOrCreate, FileAccess.ReadWrite);
            xfile = new FileStream(CONFIG.GetBasePath() + name + ".x",
                FileMode.OpenOrCreate, FileAccess.ReadWrite);
            initialized = true;

            byte[] buf = new byte[4];
            xfile.Read(buf, 0, 4);
            USED_BLK_COUNT = OPS.get_dbn(buf, 0);
            DEFS.DEBUGYELLOW("REF", "Found used block count = " + USED_BLK_COUNT);
        }

        private void dealloc_bit_internal(int dbn)
        {
            int idx = dbn_to_mbufidx(dbn);
            load_mbx(idx);

            int offset = dbn_to_mbufoffset(dbn);
            int bitshift = dbn_to_bitidx(dbn);

            mbufs[idx].data[offset] &= (byte)(~(1 << bitshift));
            mbufs[idx].is_dirty = true;
            mbufs[idx].touch_buf();
            start_dbn_single = dbn;

            USED_BLK_COUNT--;
        }
        private void set_bit_internal(int dbn)
        {
            int idx = dbn_to_mbufidx(dbn);
            load_mbx(idx);

            int offset = dbn_to_mbufoffset(dbn);
            int bitshift = dbn_to_bitidx(dbn);

            mbufs[idx].data[offset] |= (byte)(1 << bitshift);
            mbufs[idx].touch_buf();
            mbufs[idx].is_dirty = true;

            USED_BLK_COUNT++;
        }

        private bool get_bit_internal(int dbn)
        {
            int idx = dbn_to_mbufidx(dbn);
            load_mbx(idx);

            int offset = dbn_to_mbufoffset(dbn);
            int bitshift = dbn_to_bitidx(dbn);

            int ret = (mbufs[idx].data[offset] >> bitshift) & 0x01;
            mbufs[idx].touch_buf();
            return (ret == 0x01) ? true : false;
        }

        private bool alloc_bit_internal(int dbn)
        {
            if (get_bit_internal(dbn))
            {
                return false;
            }
            else
            {
                set_bit_internal(dbn);
                return true;
            }
        }

        /*
         * External interface, uses locks
         */
        public void fsid_setbit(int dbn)
        {
            
            if (!initialized) return;

            lock (mfile)
            {
                set_bit_internal(dbn);
            }                
        }

        public bool fsid_checkbit(int dbn)
        {
            if (!initialized) return false;

            lock (mfile)
            {
                return get_bit_internal(dbn);
            }        
        }

        public bool is_block_free(int dbn)
        {
            if (!initialized) return false;
            lock (mfile)
            {
                return !get_bit_internal(dbn);
            }           
        }

        public void free_bit(int dbn)
        {
            if (!initialized) return;

            lock (mfile)
            {
                dealloc_bit_internal(dbn);
            }
        }

        public bool try_alloc_bit(int dbn)
        {
            if (!initialized) return false;

            lock (mfile)
            {
                return alloc_bit_internal(dbn);
            }
        }

        //int test_quick_alloc = 1024;

        public int allocate_bit()
        {
            //If this path is fast, we can touch upto 70Mbps!.
            //if (test_quick_alloc != 0)
            //    return test_quick_alloc++;

            if (!initialized) return -1;
            
            lock (mfile)
            {
                int sidx = dbn_to_mbufidx(start_dbn_single);

                for (int idx = sidx; idx < 1024; idx++)
                {
                    DEFS.ASSERT(start_dbn_single >= (2097152 * sidx), "Incorrect starting point in allocate_bit");

                    int sdbn = start_dbn_single;
                    load_mbx(idx);

                    for (int dbn = sdbn; dbn < ((idx + 1) * 2097152); dbn++)
                    {
                        if (alloc_bit_internal(dbn)) return dbn;
                        start_dbn_single = dbn + 1;
                    }
                }
            }
            DEFS.ASSERT(false, "Count not allocate even a single bit!!");
            return -1;
        }

        public void sync()
        {
            if (!initialized) return;

            lock (mfile)
            {
                for (int i = 0; i < 1024; i++)
                {
                    if (mbufs[i] != null)
                    {
                        if (mbufs[i].is_dirty)
                        {
                            mfile.Seek((long)i * (256 * 1024), SeekOrigin.Begin);
                            mfile.Write(mbufs[i].data, 0, (256 * 1024));
                            mbufs[i].is_dirty = false;
                        }

                        if (mbufs[i].get_buf_age() > 120000)
                        {
                            mbufs[i] = null;
                        }
                    }
                }

                xfile.SetLength(0);
                xfile.Seek(0, SeekOrigin.Begin);
                byte[] buf = new byte[4];
                OPS.set_dbn(buf, 0, USED_BLK_COUNT);
                xfile.Write(buf, 0, 4);
                xfile.Flush();
                DEFS.DEBUG("REF", "SAVED used block cnt = " + USED_BLK_COUNT);
            } //lock 
        }

        public void shut_down()
        {
            DEFS.DEBUG("SHUTDOWN", "Calling Map256M() shut down");
            sync();
            mfile.Flush();
            mfile.Close();
            xfile.Flush();
            xfile.Close();
            initialized = false;
            mfile = null;
            xfile = null;
            DEFS.DEBUG("SHUTDOWN", "Finishing Map256M() shut down");
        }
    }
}
