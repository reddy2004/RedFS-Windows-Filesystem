using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;

namespace redfs_v2
{
    public enum CONFIGERROR
    { 
        OKAY,
        HASH_CHECK_FAILED,
        FILE_SIZE_MISMATCH,
        FILE_MISSING
    }

    public class CONFIG
    {
        public CONFIG()
        { 
        
        }

        private static string CalculateMD5Hash(string input)
        {
            // step 1, calculate MD5 hash from input
            MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            byte[] hash = md5.ComputeHash(inputBytes);

            // step 2, convert byte array to hex string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("X2"));
            }
            return sb.ToString();
        }

        /*
         * Create a config.txt file here, with the disk information and
         * a secret hash that can be verified.
         * Hash depends on file create times of all 3 files + the config.text information
         * itself.
         */
        private static string GetConfigString(string path)
        {
            FileInfo f1 = new FileInfo(path + "\\disk2");
            FileInfo f2 = new FileInfo(path + "\\RFI2.dat");
            FileInfo f3 = new FileInfo(path + "\\allocationmap");

            string string1 = f1.CreationTime.ToLongTimeString() + "::" + f2.CreationTime.ToLongTimeString() + "::" + f3.CreationTime.ToLongTimeString();
            string string2 = f1.FullName + "::" + f2.FullName + "::" + f3.FullName;
            string string3 = "todo";// f2.Length + "::" + f3.Length;

            string final = string1 + "::" + string2 + "::" + string3;

            return final;
        }

        public static bool CreateConfigInformation2(string path)
        {
            string final = GetConfigString(path);
            string configkey = CalculateMD5Hash(final);
            
            FileStream fs = new FileStream(path + "\\config.txt", FileMode.Create);
            StreamWriter swr = new StreamWriter(fs);
            swr.WriteLine(configkey);
            //swr.WriteLine(final);
            swr.WriteLine("Please do not modify this file in any way!");
            swr.Flush();
            swr.Close();
            return true;
        }

        /*
         * Do the actual loading and save the contents in static variables of this class.
         * Other objects/classes can query the CONFIG.*functions and get the info they need.
         */
        private static string basepath = "";
        private static byte[] XORBUF = new byte[4096];

        public static string GetBasePath() { return basepath + "\\"; }
        public static string GetTFilePath() {return basepath + "\\tfile";}
        public static string GetDLogFilePath() { return basepath + "\\dellog"; }
        public static string GetRefCntFilePath() { return basepath + "\\RFI2.dat"; }
        //public static string GetRefCntFilePath() { return "F:\\RFI2.dat"; }
        public static string GetAllocationMapPath() { return basepath + "\\allocationmap";}

        public static void Decrypt_Read_WRBuf(byte[] readdata, byte[] incore)
        {
            for (int i = 0; i < 4096; i++) 
            {
                incore[i] = (byte)(readdata[i] ^ XORBUF[i]);
                //incore[i] = readdata[i];
            }
        }

        public static void Encrypt_Data_ForWrite(byte[] writedata, byte[] incore)
        {
            for (int i = 0; i < 4096; i++)
            {
                writedata[i] = (byte)(incore[i] ^ XORBUF[i]);
                //writedata[i] = incore[i];
            }        
        }

        public static void GenerateXORBuf(string key, byte[] buffer)
        {
            MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(key);
            byte[] hash = md5.ComputeHash(inputBytes);

            
            byte smallbyte = 0;

            for (int i = 0; i < hash.Length; i++)
            {
                if (smallbyte > hash[i]) smallbyte = hash[i];
            }
            for (int i = 0; i < hash.Length; i++)
            {
                hash[i] -= smallbyte;
            }
            for (int i = 0; i < 256; i++)
            {
                for (int j = 0; j < hash.Length; j++)
                    buffer[i * 16 + j] = hash[j];
            }        
        }

        public static void LoadConfigForVirtualDisk(string path)
        {
            basepath = path;
            GenerateXORBuf(path, XORBUF);
        }

        public static void ClearIncoreConfigInformation()
        {
            basepath = "";
        }

        public static CONFIGERROR CheckIfValidFolder(string path)
        {
            if (!File.Exists(path + "\\disk2") || !File.Exists(path + "\\RFI2.dat") ||
                !File.Exists(path + "\\allocationmap"))
            {
                return CONFIGERROR.FILE_MISSING;
            }
            else
            {
                /*
                 * optional files may/maynot exists.
                 * clog, tfile, allocationmap.x, log_* files.
                 */
                FileInfo f1 = new FileInfo(path + "\\disk2");
                FileInfo f2 = new FileInfo(path + "\\RFI2.dat");
                FileInfo f3 = new FileInfo(path + "\\allocationmap");

                int blkcnt1 = (int)(f1.Length/4096);
                int blkcnt2 = (int)(f2.Length/8);
                int blkcnt3 = (int)(f3.Length*8);

                //if (blkcnt1 == blkcnt2 && blkcnt2 == blkcnt3) return true;
                //return false;
                string final = GetConfigString(path);
                //DEFS.DEBUGYELLOW("S", final);
                string configkey = CalculateMD5Hash(final);

                string savedconfig = "";
                try
                {
                    FileStream fs = new FileStream(path + "\\config.txt", FileMode.Open);
                    StreamReader sr = new StreamReader(fs);
                    savedconfig = sr.ReadLine();
                    sr.Close();
                }
                catch (Exception e)
                {
                    DEFS.DEBUG("EXCEPTION", e.Message);
                }

                if (savedconfig == configkey) return CONFIGERROR.OKAY;
                return CONFIGERROR.HASH_CHECK_FAILED;
            }
        }
    }
}
