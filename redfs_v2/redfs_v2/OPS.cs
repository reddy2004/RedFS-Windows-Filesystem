using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace redfs_v2
{
    /*
     * Created on program startup, ptrRedFS will always be incore and is the interface
     * to the filesystem. pFSID is created for DokanentryPt to interact with ptrRedFS.
     * pDOKAN is there for Dokan only,
     * Later we may add many ptrRedFS and other clients like pDOKAN to interact with
     * each other.
     */
    public class REDDY
    {
        public static REDFSDrive ptrRedFS;
        public static DokanEntryPt pDOKAN;
        public static IFSD_Mux ptrIFSDMux;

        public static int mountedidx = -1;
        /*
         * The global list of all the fsid's that are in the filesystem.
         * Remember that we could write to any fsid we want.
         */
        public static RedFS_FSID[] FSIDList = new RedFS_FSID[1024];
    }

    public class TimingCounter
    {
        public long iteration = 1;
        long starttime = 0;
        long endtime = 0;
        long total_time = 0;
        long curroptime = 0;

        public TimingCounter()
        { 
        
        }

        public void start_counter()
        {
            iteration++;
            starttime = DateTime.Now.ToUniversalTime().Ticks;
        }

        public void stop_counter()
        {
            endtime = DateTime.Now.ToUniversalTime().Ticks;
            curroptime = (endtime - starttime);
            total_time += curroptime;       
        }

        public int get_millisecs_avg()
        {
            int mseconds = (int)((total_time / iteration) / 10000);
            return mseconds;
        }

        public int get_millisecs_lastop()
        {
            int mseconds = (int)((curroptime) / 10000);
            return mseconds;        
        }

        public int get_microsecs_avg()
        {
            int mseconds = (int)((total_time / iteration) / 10);
            return mseconds;
        }

        public int get_microsecs_lastop()
        {
            int mseconds = (int)((curroptime) / 10);
            return mseconds;
        }
    }

    public class OPS
    {
        public static string ChecksumPageWRLoader(byte[] buf)
        { 
            MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] hash = md5.ComputeHash(buf);
            return HashToString(hash);
        }

        public static string HashToString(byte[] hash)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("X2"));
            }
            return sb.ToString();
        }

        public static void set_int(byte[] data, int byteoffset, int value)
        {
            byte[] val = BitConverter.GetBytes(value);
            data[byteoffset] = val[0];
            data[byteoffset + 1] = val[1];
            data[byteoffset + 2] = val[2];
            data[byteoffset + 3] = val[3];
        }
        
        public static int get_int(byte[] data, int byteoffset)
        {
            return BitConverter.ToInt32(data, byteoffset);
        }

        public static bool DedupeBCompare(RedBufL0 wbl0a, RedBufL0 wbl0b)
        {
            return true;
        }

        /*
         * See if we can do the write directly without inserting L0's and get over with it
         * quickly. returns the number of dbns required if possible, or -1.
         */
        public static int myidx_in_myparent(int level, int somefbn)
        {
            int m_start_fbn = SomeFBNToStartFBN(level, somefbn);

            switch (level)
            { 
                case 0:
                    return (int)(m_start_fbn % 1024);
                case 1:
                    return (int)((m_start_fbn % (1024 * 1024)) / 1024); 
                case 2:
                    return (int)(m_start_fbn / (1024 * 1024));
            }
            return 0;
        }

        public static int is_aligned_full_write(long fileoffset, int buflen)
        {
            return ((fileoffset % 4096 == 0) && (buflen % 4096 == 0) && 
                        buflen != 0) ? (buflen / 4096) : -1;
        }

        public static int inonum_to_inowipfbn(int ino) { return ino/32;}
        public static int get_first_free_bit(byte b) 
        {
            int bint = b;
            for (int i = 0; i < 8; i++) 
            {
                if (((bint >> i) & 0x0001) == 0) return i;
            }
            return -1;
        }
        public static byte set_free_bit(byte b, int k)
        {
            int bint = (1 << k);
            int bnew = (byte)(bint & 0x00FF);
            bnew |= b;
            DEFS.ASSERT(b != (byte)bnew, "Some bit must have been set");
            return (byte)bnew;
        }

        public static void set_dbn(byte[] data, int idx, int dbn)
        {
            byte[] val = BitConverter.GetBytes(dbn);
            int offset = idx * 4;
            data[offset] = val[0];
            data[offset + 1] = val[1];
            data[offset + 2] = val[2];
            data[offset + 3] = val[3];
        }

        public static int get_dbn(byte[] data, int idx)
        {
            int offset = idx * 4;
            return BitConverter.ToInt32(data, offset); ;
        }

        public static bool Checkout_Wip2(RedFS_Inode inowip, RedFS_Inode mywip, int m_ino)
        {
            WIP_TYPE oldtype = mywip.get_wiptype();

            for (int i = 0; i < 16; i++) 
            { 
                DEFS.ASSERT(mywip.get_child_dbn(i) == DBN.INVALID, "Wip cannot be valid during checkout, " + 
                        i + " value = " + mywip.get_child_dbn(i));
            }
            long fileoffset = m_ino * 128;
            
            lock (inowip)
            {
                REDDY.ptrRedFS.redfs_read(inowip, fileoffset, mywip.data, 0, 128);
                if (oldtype != WIP_TYPE.UNDEFINED)
                    mywip.set_wiptype(oldtype);
            }
            DEFS.DEBUG("CO_WIP", mywip.get_string_rep2());
            return mywip.verify_inode_number();
        }

        public static bool CheckinZerodWipData(RedFS_Inode inowip, int m_ino)
        {
            long fileoffset = m_ino * 128;
            byte[] data = new byte[128];
            lock (inowip)
            {
                REDDY.ptrRedFS.redfs_write(inowip, fileoffset, data, 0, 128);
                inowip.is_dirty = true;
            }
            return true;        
        }

        public static bool Checkin_Wip(RedFS_Inode inowip, RedFS_Inode mywip, int m_ino)
        {
            DEFS.ASSERT(m_ino == mywip.get_ino(), "Inode numbers dont match, can lead to corruption " + m_ino + "," + mywip.get_ino());
            long fileoffset = m_ino * 128;
            lock (inowip)
            {
                REDDY.ptrRedFS.redfs_write(inowip, fileoffset, mywip.data, 0, 128);
                DEFS.DEBUG("OPS", "CheckIn wip " + mywip.get_ino() + " size = " + mywip.get_filesize());
                inowip.is_dirty = true;
            }
            DEFS.DEBUG("CI_WIP", mywip.get_string_rep2());
            return true;
        }

        public static int COMPAREBUFS(byte[] b1, byte[] b2)
        {
            DEFS.ASSERT(b1.Length == b2.Length, "Cannot compare");
            int mismatch = 0;
            for (int i = 0; i < b1.Length; i++) 
            {
                if (b1[i] != b2[i]) mismatch++;
            }
            return mismatch;
        }

        public static void BZERO(byte[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++) { buffer[i] = 0; }
        }

        public static int NUML0(long size)
        {
            return (int)((size % 4096 == 0) ? (size / 4096) : (size / 4096 + 1));
        }
        public static int NUML1(long size)
        {
            if (size <= ((long)1024 * 64)) { return 0; }
            int numl0 = NUML0(size);
            return (int)((numl0 % 1024 == 0) ? (numl0 / 1024) : (numl0 / 1024 + 1));
        }
        public static int NUML2(long size)
        {
            if (size <= ((long)1024*1024*64)) {return 0;}
            int numl1 = NUML1(size);
            return (int)((numl1 % 1024 == 0) ? (numl1 / 1024) : (numl1 / 1024 + 1));
        }

        public static int FSIZETONUMBLOCKS(long size)
        { 
            return (NUML2(size) + NUML1(size) + NUML0(size));
        }
        public static int FSIZETOILEVEL(long size)
        {
            if (size <= 16 * 4096) return 0;
            else if (size <= (1024 * 4096) * 16) return 1;
            else return 2;
        }

        public static long NEXT4KBOUNDARY(long currsize, long newsize)
        {
            return ((currsize % 4096) == 0) ? ((newsize < (currsize + 4096)) ?
                    newsize : (currsize + 4096)) : ((long)NUML0(currsize) * 4096);
        }

        public static long NEXTL1BOUNDARY(long currsize, long newsize)
        {
            int numL1s = NUML1(currsize);
            return (numL1s * (4096 * 1024));
        }

        public static long ComputeNextStartFbn(RedFS_Inode wip)
        {
            int cnt = NUML0(wip.get_filesize());
            return ((long)cnt * 4096);
        }
        /*
        public static bool HasChildIncore(RedFS_Inode wip, int level, long sfbn)
        {
            if (level == 1)
            {
                RedBufL1 wbl1 = (RedBufL1)wip.FindOrInsertOrRemoveBuf(FIR_OPTYPE.FIND, 1, sfbn, null, null);
                if (wbl1.get_numchildptrs_incore() != 0) return true;
                else return false;
            }
            else if (level == 2)
            {
                RedBufL2 wbl2 = (RedBufL2)wip.FindOrInsertOrRemoveBuf(FIR_OPTYPE.FIND, 2, sfbn, null, null);
                if (wbl2.get_numchildptrs_incore() != 0) return true;
                else return false;
            }
            else
            {
                DEFS.ASSERT(false, "Dont pass wrong arguments");
                return false;
            }
        }
         */

        public static bool HasChildIncoreOld(RedFS_Inode wip, int level, long sfbn)
        {
            DEFS.ASSERT(level > 0, "Incorrect level to HasChildIncore()");
            if (level == 1)
            {
                int count0 = wip.L0list.Count;
                int span1 = 1024;

                for (int i = 0; i < count0; i++) 
                {
                    RedBufL0 wbl0 = (RedBufL0)wip.L0list.ElementAt(i);
                    if (wbl0.m_start_fbn >= sfbn && wbl0.m_start_fbn < (sfbn + span1))
                    {
                        return true;
                    }
                }
                return false;
            }
            else
            {
                int count1 = wip.L1list.Count;
                int span2 = 1024 * 1024;

                for (int i = 0; i < count1; i++)
                {
                    RedBufL1 wbl1 = (RedBufL1)wip.L1list.ElementAt(i);
                    if (wbl1.m_start_fbn >= sfbn && wbl1.m_start_fbn < (sfbn + span2))
                    {
                        return true;
                    }
                }
                return false;           
            }
        }
        
        public static int PIDXToStartFBN(int level, int idx)
        {
            if (level == 0) 
            {
                return idx;
            }
            else if (level == 1) 
            {
                return idx * 1024;
            }
            else if (level == 2)
            {
                return idx * (1024 * 1024);
            }
            else
            {
                DEFS.ASSERT(false, "Incorrect level passed to PIDXToStartFBN:" + level + " " + idx);
                return -1;
            }
        }

        public static int OffsetToFBN(long offset) 
        {
            //return ((offset % 4096) == 0)?((int)(offset/4096 + 1)):((int)(offset/4096));
            return (int) (offset / 4096);
        }

        public static int SomeFBNToStartFBN(int level, int somefbn)
        {
            if (level == 0)
            {
                return somefbn;
            }
            else if (level == 1)
            {
                return (int)((somefbn / 1024) * 1024);
            }
            else if (level == 2)
            {
                return (int)((somefbn / (1024 * 1024)) * (1024 * 1024));
            }
            else
            {
                DEFS.ASSERT(false, "Incorrect level to resolve in SomeFBNToStartFBN");
                return -1;
            }
        }

        public static int OffsetToStartFBN(int level, long offset) 
        {
            int fbn = OffsetToFBN(offset);
            return SomeFBNToStartFBN(level, fbn);
        }

        public static int SLACK(long filesize)
        {
            return (int)((long)NUML0(filesize) * 4096 - filesize);
        }
        /*
        public static Red_Buffer get_buf2(string who, RedFS_Inode wip, int level, int some_fbn, bool isquery)
        {
            DEFS.DEBUG("getbuf", "-> " + who + "," + wip.m_ino + "," + level + "," + some_fbn + "," + isquery);
            Red_Buffer retbuf = wip.FindOrInsertOrRemoveBuf(FIR_OPTYPE.FIND, level, some_fbn, null, null); 
            if (!isquery) 
            {
                DEFS.ASSERT(retbuf != null, "newer get_buf2 has failed");
            }
            return retbuf;
        }
         */
        /*
         * This can never return null, the caller *must* know that this buffer
         * is incore before calling. Must be called with a lock held on wip. But in case
         * is query is set true, then the caller is not sure if the buf is incore, in that
         * case we can return null safely.
         */

        public static Red_Buffer get_buf3(string who, RedFS_Inode wip, int level, int some_fbn, bool isquery)
        {
            List<Red_Buffer> list = null;

            switch (level)
            {
                case 0:
                    list = wip.L0list;
                    break;
                case 1:
                    list = wip.L1list;
                    break;
                case 2:
                    list = wip.L2list;
                    break;
            }
            DEFS.ASSERT(list != null, "List cannot be null in get_buf()");

            int start_fbn = SomeFBNToStartFBN(level, some_fbn);

            //some optimization, 10-12 mbps more.
            if (level == 1)
            {
                if (wip._lasthitbuf != null && wip._lasthitbuf.get_start_fbn() == start_fbn)
                    return wip._lasthitbuf;
            }

            for (int idx = 0; idx < (list.Count); idx++)
            //for (int idx = (list.Count - 1); idx >= 0; idx--)
            {
                int idx2 = (level == 0) ? (list.Count - idx - 1) : idx;
                Red_Buffer wb = (Red_Buffer)list.ElementAt(idx2);
                if (wb.get_start_fbn() == start_fbn)
                {
                    //if (wb.get_level() > 0 && list.Count > 2) //like splay tree.
                    //{
                    //    list.RemoveAt(idx2);
                    //    list.Insert(0, wb);
                    //}
                    if (level == 1) wip._lasthitbuf = wb; //good opti - gives 10-12mbps more.
                    return wb;
                }
            }

            DEFS.ASSERT(isquery, "who = " + who + ", get_buf() failed " + wip.get_ino() + "," + level + "," + some_fbn);
            return null;
        }

        public static void dumplistcontents(List<Red_Buffer> list)
        {
            DEFS.DEBUGCLR("DIMP", "Dumping list contents");
            for (int idx = 0; idx < list.Count; idx++)
            {
                Red_Buffer wb = (Red_Buffer)list.ElementAt(idx);
                DEFS.DEBUG("DUMPLIST", "-> (" + wb.get_level() + ")" + wb.get_ondisk_dbn() + " l=" + 
                        wb.get_start_fbn() + " isdirty = " + ((RedBufL0)wb).is_dirty);
            }        
        }

        public static void dump_inoL0_wips(byte[] buffer)
        {
            byte[] buf = new byte[128];
            RedFS_Inode wip = new RedFS_Inode(WIP_TYPE.REGULAR_FILE, 0, 0);

            for (int i = 0; i < 32; i++)
            {
                for (int t = 0; t < 128; t++) buf[t] = buffer[i * 128 + t];
                wip.parse_bytes(buf);
                if (wip.get_ino() != 0)
                {
                    DEFS.DEBUG("->", wip.get_string_rep2());
                }
            }            
        }
    }
}
