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
    public class CFSvalueoffsets
    {
        public static int fsid_ofs = 0;                //int
        public static int fsid_bofs = 4;               //int
        public static int fsid_flags = 8;              //long

        public static int fsid_namel = 16;             //int
        public static int fsid_bnamel = 20;            //int
        public static int fsid_commentl = 24;          //int

        public static int fsid_created = 28;           //long
        public static int fsid_mounted = 36;           //long
        public static int fsid_autodel = 44;           //long
        public static int fsid_lastrefresh = 52;       //long
        
        public static int fsid_logical_data = 60;      //long
        public static int fsid_unique_data = 68;       //long
        public static int fsid_start_inodenum = 76;     //int

        public static int fsid_inofile_data = 128;       //wip->data
        public static int fsid_inomap_data = 256;       //wip->data

        //required for plotting the graph
        public static int fsid_modified = 384;          //long
        public static int backing_fsid_modified = 392;  //long

        public static int fsid_name_str = 1536;        //char string unicode
        public static int fsid_bname_str = 1792;       //char string unicode
        public static int fsid_comments = 2048;        //char string unicode
    }

    public class RedFS_FSID
    {
        public CDirectory rootdir;

        public byte[] data = new byte[4096];
        private bool is_dirty2;
        public int m_dbn;

        public int get_fsid() { return m_dbn; }
        public int get_parent_fsid() { return get_int(CFSvalueoffsets.fsid_bofs); }

        private RedFS_Inode _ninowip;

        public bool get_dirty_flag() { return is_dirty2; }
        public void set_dirty(bool flag)
        {
            is_dirty2 = flag;
            if (flag) 
            {
                set_long(CFSvalueoffsets.fsid_modified, 0);
            }
        }

        public RedFS_Inode get_inode_file_wip(string requester) 
        {
            if (_ninowip == null)
            {
                _ninowip = new RedFS_Inode(WIP_TYPE.PUBLIC_INODE_FILE, 0, -1);
                for (int i = 0; i < 128; i++)
                {
                    _ninowip.data[i] = data[CFSvalueoffsets.fsid_inofile_data + i];
                }
                _ninowip.set_wiptype(WIP_TYPE.PUBLIC_INODE_FILE);
                _ninowip.setfilefsid_on_dirty(m_dbn);
            }

            DEFS.DEBUG("FSID", "Giving a inowip to " + requester);
            return _ninowip; 
        }

        public RedFS_Inode get_inodemap_wip() 
        {
            RedFS_Inode inowip = new RedFS_Inode(WIP_TYPE.PUBLIC_INODE_MAP, 0, -1);
            byte[] buf = new byte[128];
            for (int i = 0; i < 128; i++)
            {
                buf[i] = data[CFSvalueoffsets.fsid_inomap_data + i];
            }
            inowip.parse_bytes(buf);
            return inowip; 
        }

        public bool sync_internal() 
        {
            if (_ninowip != null)
            {
                for (int i = 0; i < 128; i++)
                {
                    data[CFSvalueoffsets.fsid_inofile_data + i] = _ninowip.data[i];
                }
                set_dirty(true);
            }
            return true; 
        }

        public bool set_inodemap_wip(RedFS_Inode wip) 
        {
            byte[] buf = new byte[128];
            wip.get_bytes(buf);

            for (int i = 0; i < 128; i++)
            {
                data[CFSvalueoffsets.fsid_inomap_data + i] = buf[i];
            }
            set_dirty(true);
            return true;
        }

        private void init_internal2()
        {
            RedFS_Inode w2 = get_inodemap_wip();
            for (int i = 0; i < 16; i++)
            {
                w2.set_child_dbn(i, DBN.INVALID);
            }
            w2.set_filesize(0);
            set_inodemap_wip(w2);
            set_logical_data(0);
            set_dirty(true);
        }

        public RedFS_FSID(int id, int bid, string name, string bname, string comments)
        {
            m_dbn = id;
            set_dirty(true);
            set_int(CFSvalueoffsets.fsid_ofs, id);
            set_int(CFSvalueoffsets.fsid_bofs, bid);
            set_string(CFSvalueoffsets.fsid_namel, CFSvalueoffsets.fsid_name_str, name);
            set_string(CFSvalueoffsets.fsid_bnamel, CFSvalueoffsets.fsid_bname_str, bname);
            set_string(CFSvalueoffsets.fsid_commentl, CFSvalueoffsets.fsid_comments, comments);
            set_long(CFSvalueoffsets.fsid_created, DateTime.Now.ToUniversalTime().Ticks);
            set_long(CFSvalueoffsets.fsid_lastrefresh, DateTime.Now.ToUniversalTime().Ticks);

            init_internal2();
        }

        /*
         * For duping.
         */
        public RedFS_FSID(int id, int bid, string bname, byte[] buffer)
        {
            m_dbn = id;
            for (int i = 0; i < 4096; i++)
                data[i] = buffer[i];

            set_int(CFSvalueoffsets.fsid_ofs, id);
            set_int(CFSvalueoffsets.fsid_bofs, bid);

            set_string(CFSvalueoffsets.fsid_bnamel, CFSvalueoffsets.fsid_bname_str, bname);

            set_long(CFSvalueoffsets.fsid_created, DateTime.Now.ToUniversalTime().Ticks);
            set_long(CFSvalueoffsets.fsid_lastrefresh, DateTime.Now.ToUniversalTime().Ticks);
        }

        public RedFS_FSID(int id, byte[] buffer)
        {
            m_dbn = id;
            for (int i = 0; i < 4096; i++)
                data[i] = buffer[i];
        }

        public void set_drive_name(string newname, string backingname) 
        {
            set_string(CFSvalueoffsets.fsid_namel, CFSvalueoffsets.fsid_name_str, newname);
        }

        public void update_comment(string comments) 
        {
            set_string(CFSvalueoffsets.fsid_commentl, CFSvalueoffsets.fsid_comments, comments);
        }


        private void set_int(int byteoffset, int value)
        {
            byte[] val = BitConverter.GetBytes(value);
            data[byteoffset] = val[0];
            data[byteoffset + 1] = val[1];
            data[byteoffset + 2] = val[2];
            data[byteoffset + 3] = val[3];
            is_dirty2 = true;
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
            is_dirty2 = true;
        }
        private long get_long(int byteoffset)
        {
            return BitConverter.ToInt64(data, byteoffset); ;
        }

        private string get_string(int lloc, int sloc)
        {
            int length = get_int(lloc);
            byte[] str = new byte[length];
            for (int i = 0; i < str.Length; i++)
            {
                str[i] = data[sloc + i];
            }
            return Encoding.Unicode.GetString(str);
        }

        private void set_string(int lloc, int sloc, string str)
        {
            byte[] b = Encoding.Unicode.GetBytes(str);
            set_int(lloc, b.Length);
            for (int i = 0; i < b.Length; i++)
            {
                data[sloc + i] = b[i];
            }
            set_dirty(true);
        }

        public string get_drive_name()
        {
            lock (data)
            {
                return get_string(CFSvalueoffsets.fsid_namel, CFSvalueoffsets.fsid_name_str);
            }
        }

        public void print_contents()
        {
            Console.WriteLine("FSID : " + get_int(CFSvalueoffsets.fsid_ofs) + " isdirty = " + is_dirty2);
            Console.WriteLine("Backing FSID : " + get_int(CFSvalueoffsets.fsid_bofs));
            Console.WriteLine("Name (" + get_int(CFSvalueoffsets.fsid_namel) + ") : " +
                                                get_string(CFSvalueoffsets.fsid_namel, CFSvalueoffsets.fsid_name_str));
            Console.WriteLine("Backing Name (" + get_int(CFSvalueoffsets.fsid_bnamel) + ") : " +
                                                get_string(CFSvalueoffsets.fsid_bnamel, CFSvalueoffsets.fsid_bname_str));
            Console.WriteLine("Comments (" + get_int(CFSvalueoffsets.fsid_commentl) + ") : " +
                                    get_string(CFSvalueoffsets.fsid_commentl, CFSvalueoffsets.fsid_comments));
            Console.WriteLine("Created : " + get_long(CFSvalueoffsets.fsid_created));
            Console.WriteLine("Refreshed on : " + get_long(CFSvalueoffsets.fsid_lastrefresh));
        }

        public string[] get_data_for_datagridview()
        {
            lock (data)
            {
                Random r = new Random();
                long ctime = get_long(CFSvalueoffsets.fsid_created);
                long rtime = get_long(CFSvalueoffsets.fsid_lastrefresh);
                long adeltime = get_long(CFSvalueoffsets.fsid_lastrefresh);

                string[] row = new string[10];
                row[0] = m_dbn.ToString();
                row[1] = get_string(CFSvalueoffsets.fsid_namel, CFSvalueoffsets.fsid_name_str);
                row[2] = get_string(CFSvalueoffsets.fsid_bnamel, CFSvalueoffsets.fsid_bname_str);
                row[3] = DateTime.FromBinary(ctime).ToShortTimeString() + " " + DateTime.FromBinary(ctime).ToShortDateString();
                row[4] = " - "; // DateTime.FromBinary(adeltime).ToShortTimeString() + " " + DateTime.FromBinary(adeltime).ToShortDateString();
                row[5] = " - "; // DateTime.FromBinary(rtime).ToShortTimeString() + " " + DateTime.FromBinary(rtime).ToShortDateString();
                row[6] = DEFS.getDataInStringRep(get_logical_data());
                row[7] = " - ";
                row[8] = " - ";
                return row;
            }
        }

        public void mark_for_deletion()
        {
            long flag = get_long(CFSvalueoffsets.fsid_flags);
            flag |= 0x0000000000000001;
            set_long(CFSvalueoffsets.fsid_flags, flag);
        }

        public bool ismarked_for_deletion()
        { 
            long flag = get_long(CFSvalueoffsets.fsid_flags);
            if ((flag & 0x0000000000000001) != 0)
                return true;
            else
                return false;
        }

        public int get_start_inonumber()
        {
            return get_int(CFSvalueoffsets.fsid_start_inodenum);
        }

        public void set_start_inonumber(int sinon)
        {
            set_int(CFSvalueoffsets.fsid_start_inodenum, sinon);
            set_dirty(true);
        }

        public void diff_upadate_logical_data(long value)
        {
            long final = get_logical_data() + value;
            final = (final < 0) ? 0 : final;
            set_logical_data(final);
        }

        public void set_logical_data(long d)
        {
            set_long(CFSvalueoffsets.fsid_logical_data, d);
            set_dirty(true);
        }

        public long get_logical_data()
        {
            return get_long(CFSvalueoffsets.fsid_logical_data);
        }
    }
}
