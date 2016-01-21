using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.IO;

namespace redfs_v2
{
    public class DEFSX
    {
        private static MD5 md5 = System.Security.Cryptography.MD5.Create();

        public static void ASSERT(bool value, string msg)
        {
            if (value == false)
            {
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
        public RCS_OP optype;
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
            payload_size = (op == RCS_OP.READ_REPLY) ? size : 0;
            driveid = did;
            driveoffset = offset;
            datasize = size;
        }

        public void headerobj_to_bytestream(byte[] buffer, int offset)
        {
            DEFSX.set_int(buffer, offset + 0, (int)optype);
            DEFSX.set_long(buffer, offset + 4, payload_size);
            DEFSX.set_int(buffer, offset + 8, driveid);
            DEFSX.set_long(buffer, offset + 12, driveoffset);
            DEFSX.set_int(buffer, offset + 20, datasize);
            DEFSX.set_long(buffer, offset + 24, seq_num);
        }

        public void bytestream_to_headerobj(byte[] buffer, int offset)
        {
            optype = RCS_OP.NO_OP + DEFSX.get_int(buffer, offset + 0);
            payload_size = DEFSX.get_int(buffer, offset + 4);
            driveid = DEFSX.get_int(buffer, offset + 8);
            driveoffset = DEFSX.get_long(buffer, offset + 12);
            datasize = DEFSX.get_int(buffer, offset + 20);
            seq_num = DEFSX.get_long(buffer, offset + 24);
        }
    }

    public class Lun_Item
    {
        public Socket client;
        public int drive_id;
        public long drive_io_in;
        public long drive_io_out;
        public long drive_size;
        public string drive_state;
        public string remote_ip = "-";
        public string current_op = "-";
        public long ctime;

        public Lun_Item() { }
        public string[] get_data_for_datagridview()
        {
            string[] strx = new string[8];
            strx[0] =  drive_id.ToString();
            strx[1] = DEFS.getDataInStringRep(drive_size);
            strx[2] = DateTime.FromBinary(ctime).ToShortTimeString() + " " + DateTime.FromBinary(ctime).ToShortDateString();
            strx[3] =  "-";
            strx[4] =  "-";
            strx[5] =  drive_state;
            strx[6] = remote_ip;
            strx[7] = "-";
            return strx;
        }
    }

    public class ParticipantComm
    {
        int curr_drive_id = 0;
        public LinkedList<Lun_Item> m_lunlist = new LinkedList<Lun_Item>();
        public bool refresh_required = false;

        public ParticipantComm()
        {

        }

        private void Prepare_Header2(byte[] buf, RCS_OP op, int did, long offset, int size, long seqn)
        {
            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] = 0;
            }
            switch (op)
            {
                case RCS_OP.NO_OP:
                case RCS_OP.CONNECT_ACK_FAIL:
                case RCS_OP.CONNECT_ACK_OKAY:
                case RCS_OP.READ_REPLY:
                case RCS_OP.WRITE_ACK:
                    RFSCommHeader hobj = new RFSCommHeader(op, did, offset, size);
                    hobj.seq_num = seqn;
                    hobj.headerobj_to_bytestream(buf, 0);
                    break;
                //assert otherwize
            }
        }

        private void mark_end_connection(int driveid)
        {
            lock (m_lunlist)
            {
                for (int i = 0; i < m_lunlist.Count; i++)
                {
                    Lun_Item li = m_lunlist.ElementAt(i);
                    if (li.drive_id == driveid)
                    {
                        li.client = null;
                        li.drive_state = "-";
                        li.remote_ip = "-";
                        refresh_required = true;
                    }
                }
            }
        }

        private Lun_Item create_new_conn_entry(int driveid, Socket c)
        {
            lock (m_lunlist)
            {
                for (int i = 0; i < m_lunlist.Count; i++)
                {
                    Lun_Item li = m_lunlist.ElementAt(i);
                    if (li.drive_id == driveid)
                    {
                        li.client = c;
                        li.drive_state = "mounted";
                        li.remote_ip = c.RemoteEndPoint.ToString();
                        refresh_required = true;
                        return li;
                    }
                }
            }
            DEFS.ASSERT(false, "Something went wrong in new conn");
            return null;
        }

        /*
         * Accept requests for any drive ID serially, and reply back to them in the
         * tcp port. This runs as a new thread.
         */
        private void start_servicing(object parameter1)
        {
            Lun_Item ci = (Lun_Item)parameter1;
            Socket client = ci.client;

            byte[] remote_hdr = new byte[128];
            byte[] my_hdr = new byte[128];

            while (true)
            {
                RFSCommHeader hobj = new RFSCommHeader();
                long seqn = 0;
                try
                {
                    client.Receive(remote_hdr);
                    hobj.bytestream_to_headerobj(remote_hdr, 0);
                    seqn = hobj.seq_num;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Encountered error : " + e.Message);
                    return;
                }
                switch (hobj.optype)
                {
                    case RCS_OP.NO_OP:
                        break;
                    case RCS_OP.DISCONNECT:
                        Console.WriteLine("Client has disconnected!");
                        mark_end_connection(hobj.driveid);
                        client.Close();
                        return;

                    case RCS_OP.READ:

                        Prepare_Header2(my_hdr, RCS_OP.READ_REPLY, hobj.driveid, hobj.driveoffset, hobj.datasize, seqn);
                        client.Send(my_hdr);

                        uint sxsize = 0;
                        byte[] tmpbuf = new byte[hobj.datasize];

                        string fname = hobj.driveid.ToString();
                        REDDY.ptrIFSDMux.ReadFile(1, fname, tmpbuf, ref sxsize, hobj.driveoffset, null);

                        int send_ds = 0;
                        while (send_ds < hobj.datasize)
                        {
                            int sx = client.Send(tmpbuf, send_ds, hobj.datasize - send_ds, SocketFlags.None);
                            send_ds += sx;
                        }
                        break;
                    case RCS_OP.WRITE:

                        Prepare_Header2(my_hdr, RCS_OP.WRITE_ACK, hobj.driveid, hobj.driveoffset, hobj.datasize,seqn);
                        int recv_ds = 0;
                        byte[] tmpbuf2 = new byte[hobj.datasize];

                        while (recv_ds < hobj.datasize)
                        {
                            int sx = client.Receive(tmpbuf2, recv_ds, hobj.datasize - recv_ds, SocketFlags.None);
                            recv_ds += sx;
                        }

                        uint sxsize2 = 0;
                        string fname2 = hobj.driveid.ToString();
                        REDDY.ptrIFSDMux.WriteFile(1, fname2, tmpbuf2, ref sxsize2, hobj.driveoffset, null);

                        client.Send(my_hdr);
                        break;
                    //assert otherwise
                }
            }
        }

        public bool check_if_driveid_exists(int id, ref bool alreadymounted, ref long disksize)
        {
            lock (m_lunlist)
            {
                for (int i = 0; i < m_lunlist.Count; i++) 
                {
                    Lun_Item li = m_lunlist.ElementAt(i);
                    if (li.drive_id == id) {
                        alreadymounted = (li.client != null) ? true : false;
                        disksize = li.drive_size;
                        return true;
                    }
                }
            }
            return false;
        }

        /*
         * Just get the list of all files and their properties. If a new lun is created
         * then the list must be reloaded. After the list is reloaded, then it can be displayed
         * on the UI.
         */ 
        public void load_lun_list(bool freshload)
        {
            Inode_Info[] inodes = REDDY.ptrIFSDMux.FindFilesInternalAPI(1, "\\");
            DEFS.DEBUG("lun", "Found " + inodes.Length + " luns in load_lun_list");
            lock (m_lunlist)
            {
                if (freshload) 
                {
                    DEFS.ASSERT(m_lunlist.Count == 0, "some Lun_Items cannot already exist");
                }
                for (int i = 0; i < inodes.Length; i++)
                {
                    DEFS.DEBUG("lun", inodes[i].name);
                    bool exists = false;
                    if (freshload) exists = false;
                    else { 
                        //find out..
                        for (int j = 0; j < m_lunlist.Count; j++) {
                            try
                            {
                                Lun_Item li = m_lunlist.ElementAt(j);
                                int did = Int32.Parse(inodes[i].name);

                                if (li.drive_id == did)
                                {
                                    exists = true;
                                    break;
                                }
                            }
                            catch (Exception extp)
                            {
                                DEFS.DEBUG("lun", "Error in filename from LUNdisk");
                                DEFS.DEBUG("lun", "Exception:" + extp.Message);
                                exists = true; //just to skip adding this.
                            }
                        }
                    }

                    if (!exists)
                    { 
                        //create item and insert.
                        try
                        {
                            Lun_Item li = new Lun_Item();
                            li.drive_id = Int32.Parse(inodes[i].name);
                            li.drive_size = inodes[i].size;
                            li.ctime = inodes[i].CreationTime.Ticks;
                            m_lunlist.AddFirst(li);
                        }
                        catch (Exception ep)
                        {
                            DEFS.DEBUG("lun", ep.Message);
                        }
                    }
                }
            }//lock
        }

        public void init()
        {
            load_lun_list(true);

            Thread tx = new Thread(start_listening);
            tx.Start();
        }

        private void start_listening()
        {
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 11111);
            Socket newsock = new Socket(AddressFamily.InterNetwork,
                               SocketType.Stream, ProtocolType.Tcp);
            newsock.Bind(localEndPoint);
            newsock.Listen(10);

            Console.WriteLine("participant started listening..");
            while (true)
            {

                Socket client = newsock.Accept();
                byte[] remotehdr = new byte[128];

                client.Receive(remotehdr);
                RFSCommHeader hobj = new RFSCommHeader();
                hobj.bytestream_to_headerobj(remotehdr, 0);
                curr_drive_id = hobj.driveid;
                long seqn = hobj.seq_num;
                Console.WriteLine("recived new connection attempt");

                if (hobj.optype == RCS_OP.CONNECT)
                {

                    bool alreadymounted = false;
                    long drivesize = 0;
                    bool drivepresent = check_if_driveid_exists(hobj.driveid, ref alreadymounted, ref drivesize);

                    if (drivepresent == true && alreadymounted == false)
                    {
                        byte[] replyhdr = new byte[128];
                        Prepare_Header2(replyhdr, RCS_OP.CONNECT_ACK_OKAY, 1, drivesize, 0, seqn);
                        client.Send(replyhdr);

                        Lun_Item ci = create_new_conn_entry(hobj.driveid, client);
                        Thread t = new Thread(start_servicing);
                        t.Start(ci);
                        Console.WriteLine("sent new connection okay");
                    }
                    else
                    {
                        byte[] replyhdr = new byte[128];
                        Prepare_Header2(replyhdr, RCS_OP.CONNECT_ACK_FAIL, 1, 0, 0, seqn);
                        client.Send(replyhdr);
                        client.Close();
                        Console.WriteLine("sent new connection failed");
                    }
                }
            }
        }
    }
}
