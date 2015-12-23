using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using VDSDKLib;
using System.IO;
using Microsoft.Win32.SafeHandles;

namespace redfs_initiator
{
    public class RemoteRedFSFileDiskHandler : VirtualDiskHandler
    {
        const uint FILE_SHARE_READ = 1;
        const uint GENERIC_READ = 0x80000000;
        const uint GENERIC_WRITE = 0x40000000;
        const uint OPEN_ALWAYS = 4;
        const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
        const uint FILE_FLAG_RANDOM_ACCESS = 0x10000000;
        const uint FILE_BEGIN = 0;

        RemoteVolumeInfo m_rvi;
        private bool scrubmode = false;

        public RemoteRedFSFileDiskHandler(RemoteVolumeInfo rvi, string path)
        {
            m_rvi = rvi;
        }

        override public void Release()
        {
            //Close socket connections also
            GLOBAL.icomm.close(m_rvi);
        }

        ~RemoteRedFSFileDiskHandler()
        {
            Release();
        }

        // preallocate file space
        public void PreAllocate(uint diskSizeInMB)
        {

        }

        public void init_scrubmode(bool flag)
        {
            scrubmode = flag;
        }

        override protected uint OnReadDataImpl(System.UInt64 Offset, uint Size, out System.Array BufferX)
        {
           
            byte[] buf = new byte[Size];
            lock (m_rvi)
            {
                GLOBAL.icomm.read(m_rvi, (long)Offset, buf, 0, (int)Size);
            }
            BufferX = buf;
            Console.WriteLine("Read (" + GLOBAL.icomm.seq_num + " )" + Offset + " size = " + Size + " hash:" + DEFS.checksum_buf((byte[])BufferX, 0, (int)Size));
            return Size;
        }

        override protected uint OnWriteDataImpl(System.UInt64 Offset, uint Size, ref System.Array Buffer)
        {
            byte[] buf = (byte[])Buffer;
            Console.WriteLine("write (" + GLOBAL.icomm.seq_num + ") " + Offset + " size = " + Buffer.Length + " hash:" + DEFS.checksum_buf(buf, 0, (int)Size));

            bool dopuchhole = false;
            if (scrubmode) 
            { 
                dopuchhole = true;
                for (int i = 0; i < buf.Length; i++) { if (buf[i] != 0) { dopuchhole = false; break;} }
            }

            lock (m_rvi)
            {
                if (scrubmode && dopuchhole) 
                {
                    GLOBAL.icomm.write_punch_hole(m_rvi, (long)Offset, (int)Size);
                }
                else
                {
                    GLOBAL.icomm.write(m_rvi, (long)Offset, (byte[])buf, 0, (int)Size);
                }
            }
            return Size;
        }
    }
}