using System;
using System.Security.Cryptography;

namespace redfs_initiator
{
    abstract public class VirtualDiskHandler : VDSDKLib.IVirtualDriveHandler2
    {
        // =============================================
        // Abstract methods. Must be overridden.
        // =============================================

        // Release resources
        abstract public void Release();

        // Read data from the storage
        abstract protected uint OnReadDataImpl(ulong Offset, uint BufferSize, out Array Buffer);

        // Write data to the storage
        abstract protected uint OnWriteDataImpl(ulong Offset, uint BufferSize, ref Array Buffer);


        // =============================================
        // VirtualDiskHandler methods
        // =============================================
        public VirtualDiskHandler()
        {
        }

        // =============================================
        // VDSDKLib.IVirtualDriveHandler2 methods
        // =============================================
        public uint OnReadData(ulong Offset, uint BufferSize, out Array Buffer)
        {
            uint result = OnReadDataImpl(Offset, BufferSize, out Buffer);
            return result;
        }

        public uint OnWriteData(ulong Offset, uint BufferSize, ref Array Buffer)
        {
            Array BufferToWrite = Buffer;
            uint result = OnWriteDataImpl(Offset, BufferSize, ref BufferToWrite);
            return result;
        }
    };
}
