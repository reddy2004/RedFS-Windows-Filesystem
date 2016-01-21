using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace redfs_v2
{
    //
    // three types of fingerprints are avaiable as defined below.
    // Each is a different class defined seperately.
    //
    public enum RECORD_TYPE
    { 
        FINGERPRINT_RECORD_CLOG,
        FINGERPRINT_RECORD_FPDB,
        FINGERPRINT_RECORD_MSG,
        FINGERPRINT_RECORD_BACKUP
    }

    public interface Item
    {
        int get_size();
        Item create_new_obj();
        void set_cookie(int c);
        int get_cookie();
        void get_bytes(byte[] buffer, int offset);
        void parse_bytes(byte[] buffer, int offset);
        void randomize(int seed);
        IComparer get_comparator();
        DEDUP_SORT_ORDER get_sorttype();
        RECORD_TYPE get_itemtype();
        string get_string_rep();
    }

    public class fingerprintBACKUP : Item
    { 
        public int inode, fbn;
        public byte[] fp = new byte[16];


        public fingerprintBACKUP() { }
        int Item.get_size() { return 24; }
        Item Item.create_new_obj() { return new fingerprintBACKUP(); }
        void Item.set_cookie(int c) {  }
        int Item.get_cookie() { return 0; }

        void Item.get_bytes(byte[] buffer, int offset) 
        {
            OPS.set_int(buffer, offset + 0, inode);
            OPS.set_int(buffer, offset + 4, fbn);
            for (int i = (offset + 8), k = 0; i < (offset + 24); i++, k++)
            {
                buffer[i] = fp[k];
            }
        }

        void Item.parse_bytes(byte[] buffer, int offset) 
        {
            inode = OPS.get_int(buffer, offset + 0);
            fbn = OPS.get_int(buffer, offset + 4);
            for (int i = (offset + 8), k = 0; i < (offset + 24); i++, k++)
            {
                fp[k] = buffer[i];
            }
        }

        void Item.randomize(int seed) { }
        IComparer Item.get_comparator() 
        {
            return null;
        }
        DEDUP_SORT_ORDER Item.get_sorttype() { return DEDUP_SORT_ORDER.UNDEFINED_PLACEHOLDER; }
        RECORD_TYPE Item.get_itemtype() { return RECORD_TYPE.FINGERPRINT_RECORD_BACKUP; }
        string Item.get_string_rep()
        {
            return (inode + "," + fbn + ",\t" + OPS.HashToString(fp));
        }
    }

    public class fingerprintDMSG : Item
    {
        public int fsid, inode, fbn;
        public int sourcedbn, destinationdbn;
        public byte[] fp = new byte[16];

        int _cookie;
        DEDUP_SORT_ORDER stype;

        public fingerprintDMSG(DEDUP_SORT_ORDER type) { stype = type;}
        int Item.get_size() { return 36; }
        Item Item.create_new_obj() { return new fingerprintDMSG(DEDUP_SORT_ORDER.UNDEFINED_PLACEHOLDER); }
        void Item.set_cookie(int c) { _cookie = c; }
        int Item.get_cookie() { return _cookie; }

        void Item.get_bytes(byte[] buffer, int offset) 
        {
            OPS.set_int(buffer, offset + 0, fsid);
            OPS.set_int(buffer, offset + 4, inode);
            OPS.set_int(buffer, offset + 8, fbn);
            OPS.set_int(buffer, offset + 12, sourcedbn);
            OPS.set_int(buffer, offset + 16, destinationdbn);
            for (int i = (offset + 20), k = 0; i < (offset + 36); i++, k++)
            {
                buffer[i] = fp[k];
            }
        }

        void Item.parse_bytes(byte[] buffer, int offset) 
        {
            fsid = OPS.get_int(buffer, offset + 0);
            inode = OPS.get_int(buffer, offset + 4);
            fbn = OPS.get_int(buffer, offset + 8);
            sourcedbn = OPS.get_int(buffer, offset + 12);
            destinationdbn = OPS.get_int(buffer, offset + 16);
            for (int i = (offset + 20), k = 0; i < (offset + 36); i++, k++)
            {
                fp[k] = buffer[i];
            }
        }

        void Item.randomize(int seed) { }
        IComparer Item.get_comparator() 
        {
            switch (stype)
            {
                case DEDUP_SORT_ORDER.DBN_BASED:
                    return (IComparer)new fpcomparator_dbn();
                case DEDUP_SORT_ORDER.FINGERPRINT_BASED:
                    return (IComparer)new fpcomparator_fp();
                case DEDUP_SORT_ORDER.INO_FBN_BASED:
                    return (IComparer)new fpcomparator_inofbn();
                case DEDUP_SORT_ORDER.UNDEFINED_PLACEHOLDER:
                    return null;
            }
            return null;
        }
        DEDUP_SORT_ORDER Item.get_sorttype() { return stype; }
        RECORD_TYPE Item.get_itemtype() { return RECORD_TYPE.FINGERPRINT_RECORD_MSG; }
        string Item.get_string_rep()
        {
            switch (stype)
            {
                case DEDUP_SORT_ORDER.DBN_BASED:
                case DEDUP_SORT_ORDER.UNDEFINED_PLACEHOLDER:
                    return (sourcedbn + "\t" + destinationdbn + "\t" + OPS.HashToString(fp) + "\t" + fsid + "," + inode + "," + fbn);
                case DEDUP_SORT_ORDER.FINGERPRINT_BASED:
                    return (OPS.HashToString(fp) + "\t" + sourcedbn + "\t" + destinationdbn + "\t" + fsid + "," + inode + "," + fbn);
                case DEDUP_SORT_ORDER.INO_FBN_BASED:
                    return (fsid + "," + inode + "," + fbn + ",\t" + OPS.HashToString(fp) + "\t" + sourcedbn + "\t" + destinationdbn);
            }
            return null;
        }
    }

    public class fingerprintFPDB : Item
    {
        public int dbn;
        public byte[] fp = new byte[16];

        int _cookie;
        DEDUP_SORT_ORDER stype;

        public fingerprintFPDB(DEDUP_SORT_ORDER type)
        {
            stype = type;
        }

        int Item.get_size() { return 20; }
        Item Item.create_new_obj() { return new fingerprintFPDB(DEDUP_SORT_ORDER.UNDEFINED_PLACEHOLDER); }
        void Item.set_cookie(int c) { _cookie = c; }
        int Item.get_cookie() { return _cookie; }

        void Item.get_bytes(byte[] buffer, int offset) 
        {
            OPS.set_int(buffer, offset + 0, dbn);
            for (int i = (offset + 4), k = 0; i < (offset + 20); i++, k++)
            {
                buffer[i] = fp[k];
            } 
        }
        void Item.parse_bytes(byte[] buffer, int offset)
        {
            dbn = OPS.get_int(buffer, offset + 0);
            for (int i = (offset + 4), k = 0; i < (offset + 20); i++, k++)
            {
                fp[k] = buffer[i];
            }
        }

        void Item.randomize(int seed) 
        {
            Random r = (seed!=0)? new Random(seed) :new Random();
            dbn = r.Next(1000000);
            r.NextBytes(fp);
        }
        IComparer Item.get_comparator()
        {
            switch (stype)
            {
                case DEDUP_SORT_ORDER.DBN_BASED:
                    return (IComparer)new fpcomparator_dbn();
                case DEDUP_SORT_ORDER.FINGERPRINT_BASED:
                    return (IComparer)new fpcomparator_fp();
                case DEDUP_SORT_ORDER.INO_FBN_BASED:
                    return (IComparer)new fpcomparator_inofbn();
                case DEDUP_SORT_ORDER.UNDEFINED_PLACEHOLDER:
                    return null;
            }
            return null;
        }
        DEDUP_SORT_ORDER Item.get_sorttype() { return stype; }
        RECORD_TYPE Item.get_itemtype() { return RECORD_TYPE.FINGERPRINT_RECORD_FPDB; }
        string Item.get_string_rep()
        {
            switch (stype)
            {
                case DEDUP_SORT_ORDER.UNDEFINED_PLACEHOLDER:
                case DEDUP_SORT_ORDER.DBN_BASED:
                    return (dbn + "\t" + OPS.HashToString(fp));
                case DEDUP_SORT_ORDER.FINGERPRINT_BASED:
                    return (OPS.HashToString(fp) + "\t" + dbn);
                case DEDUP_SORT_ORDER.INO_FBN_BASED:
                    return "Wrong type set";
            }
            return null;
        }
    }

    public class fingerprintCLOG : Item
    {
        public int fsid, inode, fbn;
        public int dbn, cnt;
        public byte[] fp = new byte[16];

        int _cookie;
        DEDUP_SORT_ORDER stype;

        public fingerprintCLOG(DEDUP_SORT_ORDER type)
        {
            stype = type;
        }

        DEDUP_SORT_ORDER Item.get_sorttype() { return stype; }

        void Item.randomize(int seed)
        {
            Random r = (seed != 0) ? new Random(seed) : new Random();
            dbn = r.Next(1000000);
            r.NextBytes(fp);
            fsid = r.Next() % 1024;
            inode = r.Next();
            fbn = r.Next();
            dbn = r.Next();
            cnt = r.Next();
        }

        void Item.get_bytes(byte[] buffer, int offset)
        {
            OPS.set_int(buffer, offset + 0, fsid);
            OPS.set_int(buffer, offset + 4, inode);
            OPS.set_int(buffer, offset + 8, fbn);
            OPS.set_int(buffer, offset + 12, dbn);
            OPS.set_int(buffer, offset + 16, cnt);

            for (int i = (offset + 20), k = 0; i < (offset + 36); i++, k++)
            {
                buffer[i] = fp[k];
            }
        }

        void Item.parse_bytes(byte[] buffer, int offset)
        {
            fsid = OPS.get_int(buffer, offset + 0);
            inode = OPS.get_int(buffer, offset + 4);
            fbn = OPS.get_int(buffer, offset + 8);
            dbn = OPS.get_int(buffer, offset + 12);
            cnt = OPS.get_int(buffer, offset + 16);
            for (int i = (offset + 20), k = 0; i < (offset + 36); i++, k++)
            {
                fp[k] = buffer[i];
            }
        }

        Item Item.create_new_obj() { return new fingerprintCLOG(stype); }
        void Item.set_cookie(int c) { _cookie = c; }
        int Item.get_cookie() { return _cookie; }
        int Item.get_size() { return 36; }
        IComparer Item.get_comparator()
        {
            switch (stype)
            {
                case DEDUP_SORT_ORDER.DBN_BASED:
                    return (IComparer)new fpcomparator_dbn();
                case DEDUP_SORT_ORDER.FINGERPRINT_BASED:
                    return (IComparer)new fpcomparator_fp();
                case DEDUP_SORT_ORDER.INO_FBN_BASED:
                    return (IComparer)new fpcomparator_inofbn();
                case DEDUP_SORT_ORDER.UNDEFINED_PLACEHOLDER:
                    return null;
            }
            return null;
        }
        RECORD_TYPE Item.get_itemtype() { return RECORD_TYPE.FINGERPRINT_RECORD_CLOG; }
        string Item.get_string_rep()
        {
            switch (stype)
            {
                case DEDUP_SORT_ORDER.DBN_BASED:
                    return (dbn + "\t" + OPS.HashToString(fp) + "\t" + fsid + "," + inode + "," + fbn + ",\t" + cnt);
                case DEDUP_SORT_ORDER.FINGERPRINT_BASED:
                    return (OPS.HashToString(fp) + "\t" + dbn + "\t" + fsid + "," + inode + "," + fbn + ",\t" + cnt);
                case DEDUP_SORT_ORDER.INO_FBN_BASED:
                    return (fsid + "," + inode + "," + fbn + ",\t" + cnt + ",\t" + OPS.HashToString(fp) + "\t" + dbn);
                case DEDUP_SORT_ORDER.UNDEFINED_PLACEHOLDER:
                    return "Wrong type set";                
            }
            return null;
        }
    }

    //
    // Comparators are defined  below.
    //
    public class fpcomparator_dbn : IComparer
    {
        int IComparer.Compare(object obj1, object obj2)
        {
            switch (((Item)obj1).get_itemtype())
            {
                case RECORD_TYPE.FINGERPRINT_RECORD_CLOG:
                    {
                        fingerprintCLOG c1 = (fingerprintCLOG)obj1;
                        fingerprintCLOG c2 = (fingerprintCLOG)obj2;

                        if (c1.dbn < c2.dbn) return -1;
                        else if (c1.dbn > c2.dbn) return 1;
                        return 0;
                    }
                    //break; unreachable
                case RECORD_TYPE.FINGERPRINT_RECORD_FPDB:
                    {
                        fingerprintFPDB c1 = (fingerprintFPDB)obj1;
                        fingerprintFPDB c2 = (fingerprintFPDB)obj2;

                        if (c1.dbn < c2.dbn) return -1;
                        else if (c1.dbn > c2.dbn) return 1;
                        return 0;                   
                    }
                case RECORD_TYPE.FINGERPRINT_RECORD_MSG:
                    {
                        fingerprintDMSG c1 = (fingerprintDMSG)obj1;
                        fingerprintDMSG c2 = (fingerprintDMSG)obj2;

                        if (c1.sourcedbn < c2.sourcedbn) return -1;
                        else if (c1.sourcedbn > c2.sourcedbn) return 1;
                        return 0;
                    }
                    //break; 
            }
            DEFS.ASSERT(false, "Shouldnt have come here 3423423");
            return 0;
        }
    }

    public class fpcomparator_inofbn : IComparer
    {
        int IComparer.Compare(object obj1, object obj2)
        {
            switch (((Item)obj1).get_itemtype())
            {
                case RECORD_TYPE.FINGERPRINT_RECORD_CLOG:
                    {
                        fingerprintCLOG c1 = (fingerprintCLOG)obj1;
                        fingerprintCLOG c2 = (fingerprintCLOG)obj2;

                        if (c1.fsid < c2.fsid) return -1;
                        else if (c1.fsid > c2.fsid) return 1;
                        else
                        {
                            if (c1.inode < c2.inode) return -1;
                            else if (c1.inode > c2.inode) return 1;
                            else
                            {
                                if (c1.fbn < c2.fbn) return -1;
                                else if (c1.fbn > c2.fbn) return 1;
                                else
                                {
                                    if (c1.cnt > c2.cnt) return -1;
                                    else if (c1.cnt < c2.cnt) return 1;
                                    else return 0; //can actually assert!
                                }
                            }
                        }
                    }
                    //break; unreachable
                case RECORD_TYPE.FINGERPRINT_RECORD_MSG:
                    {
                        fingerprintDMSG c1 = (fingerprintDMSG)obj1;
                        fingerprintDMSG c2 = (fingerprintDMSG)obj2;

                        if (c1.fsid < c2.fsid) return -1;
                        else if (c1.fsid > c2.fsid) return 1;
                        else
                        {
                            if (c1.inode < c2.inode) return -1;
                            else if (c1.inode > c2.inode) return 1;
                            else
                            {
                                if (c1.fbn < c2.fbn) return -1;
                                else if (c1.fbn > c2.fbn) return 1;
                                else return 0;
                            }
                        }                    
                    }
                    //break; unreachable
            }
            DEFS.ASSERT(false, "Shouldnt have come here 34234a23");
            return 0;
        }
    }

    public class fpcomparator_fp : IComparer
    {
        int IComparer.Compare(object obj1, object obj2)
        {
            switch (((Item)obj1).get_itemtype())
            {
                case RECORD_TYPE.FINGERPRINT_RECORD_CLOG:
                    {
                        fingerprintCLOG c1 = (fingerprintCLOG)obj1;
                        fingerprintCLOG c2 = (fingerprintCLOG)obj2;

                        for (int i = 0; i < 16; i++)
                        {
                            if (c1.fp[i] < c2.fp[i]) return -1;
                            else if (c1.fp[i] > c2.fp[i]) return 1;
                        }
                        return 0;                    
                    }
                    //break; unreachable
                case RECORD_TYPE.FINGERPRINT_RECORD_FPDB:
                    {
                        fingerprintFPDB c1 = (fingerprintFPDB)obj1;
                        fingerprintFPDB c2 = (fingerprintFPDB)obj2;

                        for (int i = 0; i < 16; i++)
                        {
                            if (c1.fp[i] < c2.fp[i]) return -1;
                            else if (c1.fp[i] > c2.fp[i]) return 1;
                        }
                        return 0;     
                    }
                    //break; unreachable
                case RECORD_TYPE.FINGERPRINT_RECORD_MSG:
                    {
                        fingerprintDMSG c1 = (fingerprintDMSG)obj1;
                        fingerprintDMSG c2 = (fingerprintDMSG)obj2;

                        for (int i = 0; i < 16; i++)
                        {
                            if (c1.fp[i] < c2.fp[i]) return -1;
                            else if (c1.fp[i] > c2.fp[i]) return 1;
                        }
                        return 0;     
                    }
                    //break; unreachable
            }
            DEFS.ASSERT(false, "Shouldnt have come here wewrwr2");
            return 0;
        }
    }
}
