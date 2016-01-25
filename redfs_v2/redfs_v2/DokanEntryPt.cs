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
using System.Collections;
using Dokan;
using System.Threading;

namespace redfs_v2
{
    public class DokanEntryPt
    {
        private char m_driveletter;
        private WUserLayer _userlayer;
        private int _mountedfsid = 0;

        public DokanEntryPt(int mountedfsid, char driveletter)
        {
            m_driveletter = driveletter;
            _mountedfsid = mountedfsid;
        }

        public void W()
        {
            DokanOptions opt = new DokanOptions();
            opt.DebugMode = true;
            opt.DriveLetter = m_driveletter;
            opt.ThreadCount = 1;
            opt.VolumeLabel = REDDY.FSIDList[REDDY.mountedidx].get_drive_name();

            _userlayer = new WUserLayer(_mountedfsid);
            _userlayer.Init_IFSD();
            DokanNet.DokanMain(opt, _userlayer);
            DEFS.DEBUG("DOKAN", "done with DokanMain() dokan drive: " + m_driveletter);
            System.Threading.Thread.Sleep(10000);
        }

        public void shut_down()
        {
            _userlayer.shut_down();
        }
    }

    class WUserLayer : DokanOperations
    {
        private IFSD_Mux ifs;
        private bool unmountcalled2 = false;
        private int mountedIDX = 0;

        public WUserLayer(int fm)
        {
            mountedIDX = fm;
        }

        public void shut_down()
        {
            /*
             * This assert may not always be true, since DokanUnmount() is called only if 'removable'
             * is set in dokanoptions. but this is not there in C# version of dokan.
             * DEFS.ASSERT(unmountcalled == true, "The drive must have been unmounted by now");
             */
            int counter = 0;
            //ifs.shut_down(); //cannot be called here for Mux

            while (unmountcalled2 == false) 
            {
                Thread.Sleep(100);
                DEFS.DEBUG("DOKAN", "Waiting for dokanUnmount callback to complete, itr =" + (counter++));
                if (counter > 30) break;
            } 
        }

        public void Init_IFSD()
        {
            ifs = REDDY.ptrIFSDMux;
            Thread.Sleep(2000);
            System.Diagnostics.Process.Start("explorer.exe", @"X:\");
        }

        public int CreateFile(String filename, FileAccess access, FileShare share,
            FileMode mode, FileOptions options, DokanFileInfo info)
        {
            int midx = REDDY.mountedidx;
            return ifs.CreateFile(midx, filename, access, share, mode, options, info);
        }

        public int OpenDirectory(String filename, DokanFileInfo info)
        {
            int midx = REDDY.mountedidx;
            return ifs.OpenDirectory(midx, filename, info);
        }

        public int CreateDirectory(String filename, DokanFileInfo info)
        {
            int midx = REDDY.mountedidx;
            return ifs.CreateDirectory(midx, filename, info);
        }

        public int Cleanup(String filename, DokanFileInfo info)
        {
            int midx = REDDY.mountedidx;
            return ifs.Cleanup(midx, filename, info);
        }

        public int CloseFile(String filename, DokanFileInfo info)
        {
            int midx = REDDY.mountedidx;
            return ifs.CloseFile(midx, filename, info);
        }

        public int ReadFile(String filename, Byte[] buffer, ref uint readBytes,
            long offset, DokanFileInfo info)
        {
            int midx = REDDY.mountedidx;
            return ifs.ReadFile(midx, filename, buffer, ref readBytes, offset, info);
        }

        public int WriteFile(String filename, Byte[] buffer,
            ref uint writtenBytes, long offset, DokanFileInfo info)
        {
            int midx = REDDY.mountedidx;
            return ifs.WriteFile(midx, filename, buffer, ref writtenBytes, offset, info);
        }

        public int FlushFileBuffers(String filename, DokanFileInfo info)
        {
            int midx = REDDY.mountedidx;
            return ifs.FlushFileBuffers(midx, filename, info);
        }

        public int GetFileInformation(String filename, FileInformation fileinfo, DokanFileInfo info)
        {
            int midx = REDDY.mountedidx;
            return ifs.GetFileInformation(midx, filename, fileinfo, info);
        }

        public int FindFiles(String filename, ArrayList files, DokanFileInfo info)
        {
            int midx = REDDY.mountedidx;
            return ifs.FindFiles(midx, filename, files, info);
        }

        public int SetFileAttributes(String filename, FileAttributes attr, DokanFileInfo info)
        {
            DEFS.DEBUGCLR("DOKAN", "Trying to set file attributes " + attr + "," + filename);
            return -1;
        }

        public int SetFileTime(String filename, DateTime ctime,
                DateTime atime, DateTime mtime, DokanFileInfo info)
        {
            int midx = REDDY.mountedidx;
            return ifs.SetFileTime(midx, filename, ctime, atime, mtime, info);
        }

        public int DeleteFile(String filename, DokanFileInfo info)
        {
            int midx = REDDY.mountedidx;
            return ifs.DeleteFile(midx, filename, info);
        }

        public int DeleteDirectory(String filename, DokanFileInfo info)
        {
            int midx = REDDY.mountedidx;
            return ifs.DeleteDirectory(midx, filename, info);
        }

        public int MoveFile(String filename, String newname, bool replace, DokanFileInfo info)
        {
            DEFS.DEBUG("DOKAN", "Move file request for " + filename + " to " + newname);
            DEFS.ASSERT(false, "no move possible");
            return 0;// _fs.move_file(filename, newname);
        }

        public int SetEndOfFile(String filename, long length, DokanFileInfo info)
        {
            DEFS.DEBUG("DOKAN", "SetEndOfFile(" + filename + "," + length);
            int midx = REDDY.mountedidx;
            return ifs.SetEndOfFile(midx, filename, length, info);
        }

        public int SetAllocationSize(String filename, long length, DokanFileInfo info)
        {
            return -1;
        }

        public int LockFile(String filename, long offset, long length, DokanFileInfo info)
        {
            DEFS.DEBUGCLR("DOKAN", "Lockfile = " + filename);
            return 0;
        }

        public int UnlockFile(String filename, long offset, long length, DokanFileInfo info)
        {
            DEFS.DEBUGCLR("DOKAN", "Unlockfile = " + filename);
            return 0;
        }

        public int GetDiskFreeSpace(ref ulong freeBytesAvailable, ref ulong totalBytes,
            ref ulong totalFreeBytes, DokanFileInfo info)
        {
            long dummy = 0;
            long dummy2 = 0;
            return REDDY.ptrIFSDMux.GetDiskFreeSpace(ref freeBytesAvailable, ref totalBytes, ref totalFreeBytes, ref dummy2, ref dummy);
        }

        public int Unmount(DokanFileInfo info)
        {
            DEFS.ASSERT(false, "This is not called in C# dokan");
            unmountcalled2 = true;
            return 0;
        }
    }
}
