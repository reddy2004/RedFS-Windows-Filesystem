using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VDSDKLib;

namespace redfs_initiator
{
    public static class GLOBAL
    {
        public static RedFSCommStack icomm;
    }

    public class DiskItem
    {
        public RemoteVolumeInfo m_rvi;

        private char diskLetter = 'C';
        private VirtualDiskHandler m_pHandler1;
        private int m_driveHandle1;
        private RemoteRedFSFileDiskHandler pFileDiskHandler1 = null;

        public DiskItem(char dl, RemoteVolumeInfo rvi)
        {
            diskLetter = dl;
            m_rvi = rvi;
        }

        public void launch(VirtualDrivesManager m_pManager)
        {
            uint diskSize = (uint)(m_rvi.drive_size/(1024*1024)); // MB

            pFileDiskHandler1 = new RemoteRedFSFileDiskHandler(m_rvi, "C:\\Users\\reddyv16\\Desktop\\disk_" + diskLetter);
            m_pHandler1 = pFileDiskHandler1;
            pFileDiskHandler1.PreAllocate(diskSize);

            m_driveHandle1 = m_pManager.CreateVirtualDrive((byte)diskLetter, diskSize, m_pHandler1);
            if (m_driveHandle1 == -1)
            {
                m_pHandler1 = null;
                return;
            }      
        }

        public void init_scrubmode(bool flag)
        {
            if (pFileDiskHandler1 != null)
            {
                pFileDiskHandler1.init_scrubmode(flag);
            }
        }

        public void unlaunch(VirtualDrivesManager m_pManager)
        {
            if (m_pHandler1 != null && m_pManager != null)
            {
                m_pManager.DestroyVirtualDrive(m_driveHandle1, 1);
                m_pHandler1.Release();
                m_pHandler1 = null;
            }
        }
    }

}
