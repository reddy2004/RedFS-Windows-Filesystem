using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace redfs_initiator
{
    public class DEFS
    {
        private static MD5 md5 = System.Security.Cryptography.MD5.Create();

        public static void ASSERT(bool value, string msg)
        {
            if (value == false) {
                Console.WriteLine("ASSERT: " + msg);
                System.Threading.Thread.Sleep(20000);
                
            }
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
            return BitConverter.ToInt32(data, byteoffset); ;
        }
        public static void set_long(byte[] data, int byteoffset, long value)
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
        }
        public static long get_long(byte[] data, int byteoffset)
        {
            return BitConverter.ToInt64(data, byteoffset); ;
        }

        public static string checksum_buf(byte[] buf, int offset, int size)
        {
            byte[] hash = md5.ComputeHash(buf, offset, size);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("X2"));
            }
            return sb.ToString();
        }
    }

    public enum RCS_OP
    { 
        NO_OP,
        CONNECT,
        CONNECT_ACK_OKAY,
        CONNECT_ACK_FAIL,
        DISCONNECT,
        READ,
        READ_REPLY,
        WRITE,
        WRITE_ACK,
        WRITE_PUNCH_HOLE
    };

    public class RFSCommHeader
    {
        public  RCS_OP optype;
        public int payload_size;    //next to read the size

        public int driveid;
        public long driveoffset;
        public int datasize;

        public long seq_num = 0;

        public RFSCommHeader()
        { 
        
        }

        public RFSCommHeader(RCS_OP op, int did, long offset, int size)
        {
            optype = op;
            payload_size = (op == RCS_OP.WRITE)?size:0;
            driveid = did;
            driveoffset = offset;
            datasize = size;
        }

        public void headerobj_to_bytestream(byte[] buffer, int offset) 
        {
            DEFS.set_int(buffer, offset + 0, (int)optype);
            DEFS.set_long(buffer, offset + 4, payload_size);
            DEFS.set_int(buffer, offset + 8, driveid);
            DEFS.set_long(buffer, offset + 12, driveoffset);
            DEFS.set_int(buffer, offset + 20, datasize);
            DEFS.set_long(buffer, offset + 24, seq_num);
        }

        public void bytestream_to_headerobj(byte[] buffer, int offset)
        {
            optype = RCS_OP.NO_OP + DEFS.get_int(buffer, offset + 0);
            payload_size = DEFS.get_int(buffer, offset + 4);
            driveid = DEFS.get_int(buffer, offset + 8);
            driveoffset = DEFS.get_long(buffer, offset + 12);
            datasize = DEFS.get_int(buffer, offset + 20);
            seq_num = DEFS.get_long(buffer, offset + 24);
        }
    }

    /*
     * Has socket, driveid etc information to communicate with
     * the remote host.
     */ 
    public class RemoteVolumeInfo
    {
        public Socket server;
        public IPAddress ipaddr;
        public int port;
        public int drive_id;
        public long drive_size;
    }

    public class RedFSCommStack
    {
        public int seq_num = 0;

        public RedFSCommStack()
        { 
        
        
        }

        private long Prepare_Header(byte[] buf, RCS_OP op, int did, long offset, int size)
        {
            long retval = seq_num++;
            switch (op)
            { 
                case RCS_OP.NO_OP:
                case RCS_OP.CONNECT:
                case RCS_OP.READ:
                case RCS_OP.WRITE:
                case RCS_OP.DISCONNECT:
                case RCS_OP.WRITE_PUNCH_HOLE:
                    RFSCommHeader hobj = new RFSCommHeader(op, did, offset, size);
                    hobj.seq_num = retval;
                    hobj.headerobj_to_bytestream(buf, 0);
                    break;
                //assert otherwize
            }
            return retval;
        }

        /*
         * Connect to the tcp port and establish the connection.
         * For all reads/writes, use the udp port.
         */ 
        public RemoteVolumeInfo connect(IPAddress ipaddr, int port, int driveid)
        {
            Console.WriteLine("in icomm connect " + ipaddr + "," + port + " driveid=" + driveid);

            try
            {
                IPEndPoint ipep = new IPEndPoint(ipaddr, port);
                Socket server = new Socket(AddressFamily.InterNetwork,
                                  SocketType.Stream, ProtocolType.Tcp);
                server.Connect(ipep);
               
                Console.WriteLine("icomm, connecting to server");

                RemoteVolumeInfo rvi = new RemoteVolumeInfo();
                rvi.server = server;
                rvi.ipaddr = ipaddr;
                rvi.port = port;
                rvi.drive_id = driveid;

                byte[] header = new byte[128];
                byte[] header_recv = new byte[128];

                long seqn = Prepare_Header(header, RCS_OP.CONNECT, rvi.drive_id, 0, 0);
                server.Send(header);
                Console.WriteLine("icomm, sent CONNECT, waiting for reply");

                server.Receive(header_recv);
                RFSCommHeader hobj = new RFSCommHeader();
                hobj.bytestream_to_headerobj(header_recv, 0);

                DEFS.ASSERT(hobj.seq_num == seqn, "Sequence number mismatch in connect");

                if (hobj.optype == RCS_OP.CONNECT_ACK_OKAY)
                {
                    Console.WriteLine("icomm, connected to server");
                    Console.WriteLine("icomm, remote says drive size = " + hobj.driveoffset);
                    rvi.drive_size = hobj.driveoffset; //overloaded value;
                    return rvi;
                }
                else
                {
                    Console.WriteLine("icomm, connecting to server failed");
                    return null;
                }
            }
            catch (Exception e) 
            {
                Console.WriteLine("icomm, exception " + e.Message);
                return null;
            }
        }

        public int read(RemoteVolumeInfo rvi, long disk_offset, byte[] buffer, int bufoffset, int size)
        {
            byte[] header = new byte[128];
            byte[] header_recv = new byte[128];

            long seqn = Prepare_Header(header, RCS_OP.READ, rvi.drive_id, disk_offset, size);
            rvi.server.Send(header);

            rvi.server.Receive(header_recv);
            RFSCommHeader hobj = new RFSCommHeader();
            hobj.bytestream_to_headerobj(header_recv, 0);
            if (hobj.optype == RCS_OP.READ_REPLY)
            {
                int recv_ds = 0;
                DEFS.ASSERT(hobj.seq_num == seqn && hobj.datasize == size, "Sequence number mismatchin read");
                while (recv_ds < hobj.datasize)
                {
                    int sx = rvi.server.Receive(buffer, recv_ds, hobj.datasize - recv_ds, SocketFlags.None);
                    recv_ds += sx;
                }
                return size;
            }
            else
            {
                Console.WriteLine("read error: + " + hobj.optype + " seq=" + hobj.seq_num + " size=" + hobj.datasize);
                return -1;
            }
        }

        public int write(RemoteVolumeInfo rvi, long disk_offset, byte[] buffer, int bufoffset, int size)
        {
            byte[] header = new byte[128];
            byte[] header_recv = new byte[128];

            long seqn = Prepare_Header(header, RCS_OP.WRITE, rvi.drive_id, disk_offset, size);
            rvi.server.Send(header);

            int send_ds = 0;
            while (send_ds < size)
            {
                int sz = rvi.server.Send(buffer, bufoffset + send_ds, size - send_ds, SocketFlags.None);
                send_ds += sz;
            }
            try
            {
                rvi.server.Receive(header_recv);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in write " + ex.Message);
                return -1;
            }
            RFSCommHeader hobj = new RFSCommHeader();
            hobj.bytestream_to_headerobj(header_recv, 0);

            DEFS.ASSERT(hobj.seq_num == seqn && hobj.datasize == size, "Sequence number mismatchin write");

            if (hobj.optype == RCS_OP.WRITE_ACK)
            {
                return size;
            }
            else
            {
                Console.WriteLine("write error: + " + hobj.optype);
                return -1;
            }
        }

        public int write_punch_hole(RemoteVolumeInfo rvi, long disk_offset, int size)
        {
            byte[] header = new byte[128];
            byte[] header_recv = new byte[128];

            long seqn = Prepare_Header(header, RCS_OP.WRITE_PUNCH_HOLE, rvi.drive_id, disk_offset, size);
            rvi.server.Send(header);

            try
            {
                rvi.server.Receive(header_recv);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in recv hdr for punchhole " + ex.Message);
                return -1;
            }
            RFSCommHeader hobj = new RFSCommHeader();
            hobj.bytestream_to_headerobj(header_recv, 0);

            DEFS.ASSERT(hobj.seq_num == seqn && hobj.datasize == size, "Sequence number mismatchin write 2");

            if (hobj.optype == RCS_OP.WRITE_ACK)
            {
                return size;
            }
            else
            {
                Console.WriteLine("write error: + " + hobj.optype);
                return -1;
            }        
        }

        public bool close(RemoteVolumeInfo rvi)
        {
            byte[] hdr = new byte[128];
            Prepare_Header(hdr, RCS_OP.DISCONNECT, rvi.drive_id, 0, 0);
            try
            {
                rvi.server.Send(hdr);
                rvi.server.Close();
            }
            catch (Exception e) 
            {
                Console.WriteLine("error in close : " + e.Message);
                return false;
            }
            return true;
        }
    }
}

