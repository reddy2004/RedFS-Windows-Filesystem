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
using System.Security.Cryptography;

namespace redfs_v2
{
    /*
     * For now, just a single file with locks, only the cleaner
     * will write to it, readers will read with lock held.
     */
    public class RedFSPersistantStorage
    {
        private FileStream dfile;
        private FileStream clogfile;

        private bool initialized;

        public long total_disk_reads;
        public long total_disk_writes;

        private Item fptemp = new fingerprintCLOG(0);
        private int fpcache_cnt = 0;
        private byte[] fpcache_buf = new byte[36 * 1024];
        private MD5 md5 = System.Security.Cryptography.MD5.Create();

        public void swap_clog()
        {
            flush_clog();
            lock (fpcache_buf)
            {
                clogfile.Flush();
                clogfile.Close();

                File.Move(CONFIG.GetBasePath() + "clog", CONFIG.GetBasePath() + "clog1");
                clogfile = new FileStream(CONFIG.GetBasePath() + "clog",
                        FileMode.OpenOrCreate, FileAccess.ReadWrite);
            }
        }

        public void flush_clog()
        {
            lock (fpcache_buf)
            {
                if (fpcache_cnt > 0)
                {
                    clogfile.Write(fpcache_buf, 0, fpcache_cnt*36);
                    fpcache_cnt = 0;
                }
            }
        }

        private void CheckSumBuf(RedFS_Inode wip, int fbn, int dbn, byte[] buffer, int offset)
        {
            lock (fpcache_buf)
            {
                if (fpcache_cnt == 1024)
                {
                    fpcache_cnt = 0;
                    clogfile.Write(fpcache_buf, 0, fpcache_buf.Length);
                    clogfile.Flush();
                }

                if (wip.get_wiptype() == WIP_TYPE.REGULAR_FILE)
                {
                    fingerprintCLOG fpt = (fingerprintCLOG)fptemp;

                    fpt.fsid = wip.get_filefsid();
                    fpt.inode = wip.get_ino();
                    fpt.fbn = fbn;
                    fpt.dbn = dbn;
                    fpt.cnt = (int)clogfile.Position;

                    byte[] hash = md5.ComputeHash(buffer, offset, 4096);
                    for (int i = 0; i < 16; i++)
                    {
                        fpt.fp[i] = hash[i];
                    }

                    fptemp.get_bytes(fpcache_buf, fpcache_cnt * fptemp.get_size());
                    fpcache_cnt++;
                }
            }
        }
        private void CheckSumBuf(RedFS_Inode wip, Red_Buffer wb)
        {
            lock (fpcache_buf)
            {
                if (fpcache_cnt == 1024)
                {
                    fpcache_cnt = 0;
                    clogfile.Write(fpcache_buf, 0, fpcache_buf.Length);
                    clogfile.Flush();
                }

                if (wb.get_level() == 0 && wip.get_wiptype() == WIP_TYPE.REGULAR_FILE)
                {
                    fingerprintCLOG fpt = (fingerprintCLOG)fptemp;

                    fpt.fsid = wip.get_filefsid();
                    fpt.inode = wip.get_ino();
                    fpt.fbn = (int)wb.get_start_fbn();
                    fpt.dbn = wb.get_ondisk_dbn();
                    fpt.cnt = (int)clogfile.Position;

                    byte[] hash = md5.ComputeHash(wb.buf_to_data());
                    for (int i = 0; i < 16; i++)
                    {
                        fpt.fp[i] = hash[i];
                    }

                    fptemp.get_bytes(fpcache_buf, fpcache_cnt * fptemp.get_size());
                    fpcache_cnt++;
                }
            }
        }

        public RedFSPersistantStorage(string name, string clog)
        {
            dfile = new FileStream(CONFIG.GetBasePath() + name,
                    FileMode.OpenOrCreate, FileAccess.ReadWrite);

            clogfile = new FileStream(CONFIG.GetBasePath() + clog,
                    FileMode.OpenOrCreate, FileAccess.ReadWrite);

            initialized = true;
        }

        public RedFS_FSID read_fsid(int fsid)
        {
            if (!initialized) return null;
            lock (dfile)
            {
                byte[] buffer = new byte[4096];
                dfile.Seek((long)fsid * 4096, SeekOrigin.Begin);
                dfile.Read(buffer, 0, 4096);
                RedFS_FSID fs = new RedFS_FSID(fsid, buffer);
                return fs;
            }
        }

        public bool write_fsid(RedFS_FSID wbfsid)
        {
            if (!initialized) return false;
            lock (dfile)
            {
                dfile.Seek((long)wbfsid.get_fsid() * 4096, SeekOrigin.Begin);
                dfile.Write(wbfsid.data, 0, 4096);
                dfile.Flush();
                wbfsid.set_dirty(false);
            }
            return true;
        }

        public bool read(Red_Buffer wb)
        {
            if (!initialized) return false;
            //Array.Clear(wb.buf_to_data(), 0, 4096);
            OPS.BZERO(wb.buf_to_data());

            total_disk_reads++;

            if (wb.get_ondisk_dbn() == 0) 
            {               
                return true;
            }

            lock (dfile)
            {
                //DEFS.DEBUG("RAID", "Reading dbn : " + wb.get_ondisk_dbn() + " level : " + wb.get_level());
                dfile.Seek((long)wb.get_ondisk_dbn() * 4096, SeekOrigin.Begin);
                dfile.Read(wb.buf_to_data(), 0, 4096);
            }
            return true;
        }

        public bool write(RedFS_Inode wip, int fbn, int dbn, byte[] buffer, int offset)
        {
            if (!initialized) return false;
            total_disk_writes++;
            lock (dfile)
            {
                dfile.Seek((long)dbn * 4096, SeekOrigin.Begin);
                dfile.Write(buffer, offset, 4096);
                dfile.Flush();
                //CheckSumBuf(wip, wb);
                CheckSumBuf(wip, fbn, dbn, buffer, offset);
                //DEFS.DEBUG("FASTWRITE", "dbn, bufoffset = " + dbn + "," + offset);
            }

            return true;       
        }

        public bool read(RedFS_Inode wip, int dbn, byte[] buffer, int offset)
        {
            if (!initialized) return false;
            total_disk_reads++;

            if (dbn == 0) 
            {
                Array.Clear(buffer, offset, 4096);
                return true;
            }

            lock (dfile)
            {
                dfile.Seek((long)dbn * 4096, SeekOrigin.Begin);
                dfile.Read(buffer, offset, 4096);
                dfile.Flush();
            }

            return true;
        }

        public bool write(RedFS_Inode wip, Red_Buffer wb)
        {
            if (!initialized) return false;
            total_disk_writes++;
            lock (dfile)
            {
                //DEFS.DEBUG("RAID", "Writing dbn : " + wb.get_ondisk_dbn() + " level : " + wb.get_level());
                dfile.Seek((long)wb.get_ondisk_dbn() * 4096, SeekOrigin.Begin);
                dfile.Write(wb.buf_to_data(), 0, 4096);
                dfile.Flush();
                wb.set_dirty(false);
                CheckSumBuf(wip, wb);
            }
            
            return true;
        }

        public void shut_down()
        {
            DEFS.DEBUG("SHUTDOWN", "Calling RedFSPersistantStorage() shut down, fpcache_cnt = " + fpcache_cnt);
            if (initialized == false) return;
            initialized = false;
            dfile.Flush();
            dfile.Close();
            flush_clog();
            clogfile.Flush();
            clogfile.Close();
            dfile = null;
            clogfile = null;
            DEFS.DEBUG("SHUTDOWN", "Finishing RedfsPersistantStorage() shut down");
        }
    }
}
