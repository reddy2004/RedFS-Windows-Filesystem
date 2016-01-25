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
    /*
     * Two bits
     */
    public enum WIP_TYPE
    {
        UNDEFINED,
        PUBLIC_INODE_FILE,
        PUBLIC_INODE_MAP,
        DIRECTORY_FILE,
        REGULAR_FILE
    };

    public class WIDOffsets
    {
        public static int wip_dbndata = 0;              //int[16] array
        public static int wip_inoloc = 64;                 //int
        public static int wip_parent = 68;                 //int - not used for the time being.
        public static int wip_size = 72;                //long

        public static int wip_created_from_fsid = 80;         //int
        public static int wip_modified_in_fsid = 84;         //int
        public static int wip_flags = 88;               //int
        public static int wip_cookie = 92;              //int
        public static int wip_ibflag = 96;              //int
    }

    public class wbufcomparator : IComparer<Red_Buffer>
    {
        public int Compare(Red_Buffer c1, Red_Buffer c2)
        {
            if (c1.get_start_fbn() < c2.get_start_fbn()) return -1;
            else if (c1.get_start_fbn() > c2.get_start_fbn()) return 1;
            else return 0;
        }
    }

    /*
     * Structure of 128 bytes.
     * 4*16 = 64        direct/indirect pointers.
     * 4 bytes          inode number (must corrospond to offset in file, otherwise considered 'free' slot)
     * 4 bytes          wigen (positive value if inode is used)
     * 8 bytes          file size
    ---- 80 bytes ---
     * 4 bytes          created fsid key/ touched fsid key.
     * 4 bytes          flags (for dedupe, file type etc) 
     * 4 bytes          cookie.
     * 4 bytes          ibflag.
    ---- 96 bytes ----
     * 32 bytes         reserved for future use.
    ---- 128 bytes---
     */

    public class RedFS_Inode
    {
        public Red_Buffer _lasthitbuf;

        private WIP_TYPE wiptype;

        public byte[] data = new byte[128];

        /*
         * This list will contain all the childen, both L0's and indirects.
         * Resize cases must be carefully handled. Cleaner will always sort
         * and proceed from L0 to L1 to L2
         */
        public List<Red_Buffer> L0list = new List<Red_Buffer>();
        public List<Red_Buffer> L1list = new List<Red_Buffer>();
        public List<Red_Buffer> L2list = new List<Red_Buffer>();

        public bool is_dirty;
        public ulong iohistory = 0;
        public int m_ino;

        public RedFS_Inode(WIP_TYPE t, int ino, int pino) 
        {
            m_ino = ino;
            for (int i = 0; i < 16; i++) {
                set_child_dbn(i, DBN.INVALID);
            }
            wiptype = t;
            set_wiptype(wiptype);
            set_int(WIDOffsets.wip_parent, pino);
            set_int(WIDOffsets.wip_inoloc, ino);
        }

        public void set_ino(int pino, int ino)
        {
            set_int(WIDOffsets.wip_inoloc, ino);
            set_int(WIDOffsets.wip_parent, pino);
            //m_ino = ino;
        }

        public int get_ino() 
        { 
            return get_int(WIDOffsets.wip_inoloc); 
        }

        public void sort_buflists()
        {
            L0list.Sort(new wbufcomparator());
            L1list.Sort(new wbufcomparator());
            L2list.Sort(new wbufcomparator());
        }

        public int get_incore_cnt()
        {
            return (L0list.Count + L1list.Count + L2list.Count);
        }

        public int get_filefsid_created()
        {
            return get_int(WIDOffsets.wip_created_from_fsid);
        }
        public int get_filefsid()
        {
            return get_int(WIDOffsets.wip_modified_in_fsid);
        }

        public void setfilefsid_on_dirty(int fsid)
        {
            int cf = get_int(WIDOffsets.wip_created_from_fsid);
            if (cf == 0) { //new file
                set_int(WIDOffsets.wip_created_from_fsid, fsid);
            }
            int modfsid = get_int(WIDOffsets.wip_modified_in_fsid);
            if (modfsid != fsid)
            {
                set_int(WIDOffsets.wip_modified_in_fsid, fsid);
                DEFS.DEBUG("FSID", "Set fsid for inode: " + get_ino() + " from fsid " + modfsid + " to fsid");
            }
        }

        public int get_inode_level() 
        { 
            return OPS.FSIZETOILEVEL(get_filesize()); 
        }

        public int get_child_dbn(int idx) 
        {
            int offset = idx * 4;
            return get_int(offset); 
        }
        public void set_child_dbn(int idx, int dbn) 
        {
            set_int(idx * 4, dbn);
        }

        public long get_filesize() 
        {
            return get_long(WIDOffsets.wip_size);
        }
        public void set_filesize(long s) 
        {
            set_long(WIDOffsets.wip_size, s);
        }

        public int get_cookie()
        {
            return get_int(WIDOffsets.wip_cookie);
        }
        public void set_cookie(int c) 
        {
            set_int(WIDOffsets.wip_cookie, c);
        }


        public int get_ibflag()
        {
            return get_int(WIDOffsets.wip_ibflag);
        }
        public void set_ibflag(int c)
        {
            set_int(WIDOffsets.wip_ibflag, c);
        }

        public void set_wiptype(WIP_TYPE type)
        {
            int flag = get_int(WIDOffsets.wip_flags);
            int value = -1;
            switch (type)
            {
                case WIP_TYPE.PUBLIC_INODE_FILE:
                    value = 0;
                    break;
                case WIP_TYPE.PUBLIC_INODE_MAP:
                    value = 1;
                    break;
                case WIP_TYPE.DIRECTORY_FILE:
                    value = 2;
                    break;
                case WIP_TYPE.REGULAR_FILE:
                    value = 3;
                    break;
                case WIP_TYPE.UNDEFINED:
                    value = 4;
                    break;
            }
            //DEFS.ASSERT(value != -1 && value <= 3, "Entry for wip type in the flag is incorrect : type=" + type + " value = " + value);
            flag &= 0x0FFFFFF0;
            flag |= value;
            set_int(WIDOffsets.wip_flags, flag);
        }

        public WIP_TYPE get_wiptype()
        {
            int flag = get_int(WIDOffsets.wip_flags);
            int value = flag & 0x00000007;
            WIP_TYPE type = WIP_TYPE.UNDEFINED;
            switch (value)
            { 
                case 0:
                    type = WIP_TYPE.PUBLIC_INODE_FILE;
                    break;
                case 1:
                    type = WIP_TYPE.PUBLIC_INODE_MAP;
                    break;
                case 2:
                    type = WIP_TYPE.DIRECTORY_FILE;
                    break;
                case 3:
                    type = WIP_TYPE.REGULAR_FILE;
                    break;
                case 4:
                    type = WIP_TYPE.UNDEFINED;
                    break;
            }
            //DEFS.ASSERT(type != WIP_TYPE.UNDEFINED, "Entry for wip type in the flag is incorrect");
            return type;
        }

        public void parse_bytes(byte[] buf) 
        {
            DEFS.ASSERT(buf.Length == 128 && m_ino == 0, "parse_bytes will not work correctly for non-standard input");
            //DEFS.DEBUG("FSID", "Copying filedata to wip - direct parse, ino = " + m_ino);
            for (int i = 0; i < 128; i++)
            {
                data[i] = buf[i];
            }
        }

        public bool verify_inode_number()
        {
            bool retval = (get_int(WIDOffsets.wip_inoloc) == m_ino) ? true : false;
            if (get_int(WIDOffsets.wip_inoloc) != 0 && !retval) 
            {
                DEFS.DEBUGYELLOW("ERROR", "Some error is detected!! retval = " + retval);
            }
            //DEFS.DEBUG("C", get_string_rep2());
            //DEFS.DEBUG("C", "in verify inode " + get_int(WIDOffsets.wip_inoloc) + "," + m_ino);
            return retval;
        }

        public void set_parent_ino(int pino)
        {
            set_int(WIDOffsets.wip_parent, pino);
        }

        public int get_parent_ino()
        {
            return get_int(WIDOffsets.wip_parent);
        }

        public void get_bytes(byte[] buf) 
        {
            DEFS.ASSERT(buf.Length == 128 && get_ino() == 0, "get_bytes will not work correctly for non-standard input");
            DEFS.DEBUG("FSID", "Copying wip to file data - direct get, ino = " + get_ino());
            for (int i = 0; i < 128; i++)
            {
                buf[i] = data[i];
            }           
        }

        public string get_string_rep2()
        {
            string ret = "DETAILS:" + get_ino() + "," + get_parent_ino() +  " : " + get_wiptype() + ",sz=" + get_filesize() + ":dbns=";
            for (int i=0;i<16;i++) 
            { 
                ret += " " + get_child_dbn(i);
            }
            return ret;
        }
        /*
         * Following four functions for setting and getting int,long
         * values from the wip->data.
         */
        private void set_int(int byteoffset, int value)
        {
            byte[] val = BitConverter.GetBytes(value);
            data[byteoffset] = val[0];
            data[byteoffset + 1] = val[1];
            data[byteoffset + 2] = val[2];
            data[byteoffset + 3] = val[3];
            is_dirty = true;
        }
        private int get_int(int byteoffset)
        {
            return BitConverter.ToInt32(data, byteoffset); ;
        }
        private void set_long(int byteoffset, long value)
        {
            byte[] val = BitConverter.GetBytes(value);
            data[byteoffset] = val[0];
            data[byteoffset + 1] = val[1];
            data[byteoffset + 2] = val[2];
            data[byteoffset + 3] = val[3];
            data[byteoffset + 4] = val[4];
            data[byteoffset + 5] = val[5];
            data[byteoffset + 6] = val[6];
            data[byteoffset + 7] = val[7];
            is_dirty = true;
        }
        private long get_long(int byteoffset)
        {
            return BitConverter.ToInt64(data, byteoffset); ;
        }
    }
}
