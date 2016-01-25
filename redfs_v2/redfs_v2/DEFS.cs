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
using System.IO;
using Dokan;

namespace redfs_v2
{
    public class Inode_Info
    {
        public string name;
        public bool isfile;
        public long size;
        public DateTime CreationTime;
        public DateTime LastAccessTime;
        public DateTime LastWriteTime;
        public FileAttributes fa;
        public int ino;
        public int nodecount;
        public int backupoffset;
    }

    public interface CInode
    {
        bool checkname(string name);
        FileAttributes gettype();
        string get_string_rep();
        string get_full_path();
        string get_iname();
        int get_inode_number();
        int get_parent_inode_number();

        void unmount(bool inshutdown);
        bool is_unmounted();
        bool is_time_for_clearing();
        DateTime get_atime();
        DateTime get_ctime();
        DateTime get_mtime();
        void set_acm_times(DateTime a, DateTime c, DateTime m);
        long get_size();
        int get_fsid_of_inode();
    }

    public enum FILE_IO_CODE
    { 
        OKAY,
        ERR_NULL_WIP,
        ERR_FILE_DELETED,
        ERR_REDFS_WRITE_FAILED
    }

    public enum FILE_STATE
    { 
        FILE_DEFAULT,        /* default flag, CFile->m_state must have some value right */
        FILE_IN_DOKAN_IO,   /* Dokan is using, and is in some dokan context. Until, close is called, could also mean dedupe etc */
        FILE_UNMOUNTED,     /* Unmounted, user may have to reopne if needed */
        FILE_DELETED,        /* File is deleted, so who ever comes here must react accordingly */
        FILE_ORPHANED       /*The object you are refereing to is not incore anymore, you have to reopen and do this op again.*/
    };

    public enum DIR_STATE
    {
        NOT_LOADED,             /* Nothing is incore */
        DIRFILE_LOADING,        /* Internal flag, ideally in ST, nobody should see this flag */
        DIRFILE_DIRTY,          /* Dir is dirty, DIRFILE_DIRTY superset of DIR_CONTENTS_INCORE */
        DIR_CONTENTS_INCORE,    /* Entries are incore, but this dir itself is not dirty */
        DIR_TERMINATED,         /* Terminated - dont do anything from now on */
        DIR_IS_SKELETON,         /* Only some entries are present, they cannot be dirty, just ReadOnly */
        DIR_DELETED             /* Directory was just deleted, so dont try to reload or read it. wait for parent to syncup */
    };

    public class DEFS
    {
        public static StreamWriter log;

        public static void INIT_LOGGING()
        {
            string t = get_time_formatted();

            DEFS.DEBUG("IFSD","Setting INIT_LOGGING()" + t); 
            if (log == null)
            {
                string fname = CONFIG.GetBasePath() + "log_" + t + ".txt";
                Console.WriteLine("Setting INIT_LOGGING()" + fname);
                log = new StreamWriter(fname);
                DEFS.DEBUG("IFSD","Setting INIT_LOGGING()" + t); 
            }
  
            DEFS.DEBUG("IFSD", "Done INIT_LOGGING()");
        }

        public static void STOP_LOGGING()
        {
            if (log != null)
            {
                log.Flush();
                log.Close();
                log = null;
            }
        }

        public static void ASSERT(bool value, String dbg)
        {
            if (!value)
            {
                Console.BackgroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("ASSERT :: " + dbg);
                DEBUG("ASSERT", dbg);
                Console.BackgroundColor = ConsoleColor.Black;
                try
                {
                    DokanNet.DokanUnmount('X');
                }
                catch (Exception ed) 
                {
                    Console.WriteLine("Exception " + ed.Message);
                }
                STOP_LOGGING();
                Console.WriteLine(System.Environment.StackTrace);
                System.Threading.Thread.Sleep(100000);
                System.Environment.Exit(0);
            }
        }

        public static void DEBUGYELLOW(string id, string msg)
        {
            Console.BackgroundColor = ConsoleColor.Yellow;
            string dbgstr = "DGB:\t" + id + "\t" + msg;
            Console.WriteLine(dbgstr);
            if (log != null) log.WriteLine(dbgstr);
            Console.BackgroundColor = ConsoleColor.Black;
        }

        public static void DEBUGCLR(string id, string msg)
        {
            Console.BackgroundColor = ConsoleColor.Blue;
            string dbgstr = "DGB:\t" + id + "\t" + msg;
            Console.WriteLine(dbgstr);
            if (log != null) log.WriteLine(dbgstr);
            Console.BackgroundColor = ConsoleColor.Black;
        }

        public static void DEBUG(string id, string msg)
        {
            //string dbgstr = "DGB:\t" + get_time_formatted() + "\t" + id + "\t" + msg;
            string dbgstr = "DGB:\t" + id + "\t" + msg;
            Console.WriteLine(dbgstr);
            if (log != null) log.WriteLine(dbgstr);
        }

        public static string get_time_formatted()
        {
            DateTime dt = DateTime.Now;
            int hr = dt.Hour;
            int min = dt.Minute;
            int sec = dt.Second;
            int milli = dt.Millisecond;

            string ret = "";

            if (hr <= 9) ret += "0" + hr.ToString();
            else ret += hr.ToString();

            if (min <= 9) ret += "0" + min.ToString();
            else ret += min.ToString();

            if (sec <= 9) ret += "0" + sec.ToString();
            else ret += sec.ToString();

            if (milli <= 9) ret += "00" + milli.ToString();
            else if (milli <= 99) ret += "0" + milli.ToString();
            else ret += milli.ToString();

            return ret;
        }

        public static string getDataInStringRep(long size) 
        {
            double value = 0;
            string type = "";

            if (size < 1024) { value = (double)(size); type = " B"; }
            else if (size < 1024 * 1024) { value = (double)size / 1024; type = " KB"; }
            else if (size < (1024 * 1024 * 1024)) {value = (double)size / (1024 * 1024); type = " MB";}
            else if (size < ((long)1024 * 1024 * 1024 * 1024)) { value = (double)size / (1024 * 1024 * 1024); type = " GB"; }
            else { value = (double)size / ((long)1024 * 1024 * 1024 * 1024); type = " TB"; }

            return String.Format("{0:0.00}", value) + type; 
        }
    }
}
