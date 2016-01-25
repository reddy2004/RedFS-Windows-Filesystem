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
using System.Threading;

namespace redfs_v2
{
    public enum DEDUP_RETURN_CODE
    {
        DEDUP_UNKNOWN,
        DEDUP_BUFINCORE,
        DEDUP_SOURCE_DBN_HAS_CHANGED,
        DEDUP_DONE_INOFBN
    }

    public enum DEDUP_SORT_ORDER
    {
        UNDEFINED_PLACEHOLDER,
        INO_FBN_BASED,
        FINGERPRINT_BASED,
        DBN_BASED
    }

    public enum DEDUP_ENGINE_STAGE
    { 
        NOT_STARTED,
        SORT_CLOG_INOFBN,
        CLEAN_CLOG_INOFBNSTALES,
        SORT_CLOG_DBN,
        CLEAN_CLOG_DBNSTALES,
        SORT_CLOG_FPORDER,
        CLEAN_FPDB_DBNSTALES,
        SORT_FPDB_FPORDER,
        GENERATE_DEDUPE_SCRIPT,
        SORT_FPDBT_DBN,
        SORT_SCRIPT_INOFBN,
        DEDUPE_MESSAGES,
        DEDUPE_DONE
    }

    /*
     * keep this < 1000 lines.
     * find duplicates in the form of <source=ino,fbn> versus <dbn>
     * donor will always be a block that already exists on disk.
     * 
     * Impliment RAID-LEVEL snapshot
     */
    public class DedupeEngine
    {
        DEDUP_ENGINE_STAGE m_stage = DEDUP_ENGINE_STAGE.NOT_STARTED;
        Dedupe_UI ui;

        public DedupeEngine(Dedupe_UI u)
        {
            ui = u;
        }

        public DEDUP_ENGINE_STAGE get_stage2()
        {
            return m_stage;
        }

        private void loop()
        {
            Random r = new Random();
            for (int i = 0; i < 100; i++)
            {
                Thread.Sleep(500);
                ui.Update_DedupeUI(DEDUP_ENGINE_STAGE.NOT_STARTED + r.Next(12), i);
            }
        }

        public void start_dedupe_async()
        {
            Thread t = new Thread(start_operation);
            t.Start();
        }


        private void start_operation()
        {
            string input_clog = CONFIG.GetBasePath() + "clog1";
            string clog_inofbn = CONFIG.GetBasePath() + "clog.inofbn";
            string clog_inofbnclean = CONFIG.GetBasePath() + "clog.inofbn.clean";
            string clog_dbn = CONFIG.GetBasePath() + "clog.dbn";
            string clog_dbnclean = CONFIG.GetBasePath() + "clog.dbn.clean";
            string clog_fporder = CONFIG.GetBasePath() + "clog.fporder";

            string fpdb_input = CONFIG.GetBasePath() + "fpdb1";

            string fpdb_dbncleaned = CONFIG.GetBasePath() + "fpdb.dbn.clean";
            string fpdb_fporder = CONFIG.GetBasePath() + "fpdb.fporder";

            string fpdb_temp = CONFIG.GetBasePath() + "fpdb.temp";

            string dedupeScript_a = CONFIG.GetBasePath() + "script.dedupeop.temp";
            string dedupeScript = CONFIG.GetBasePath() + "script.dedupeop";

            REDDY.ptrRedFS.dedupe_swap_changelog();

            if (File.Exists(input_clog) == false)
            {
                DEFS.DEBUG("DE", "Input clog does not exist, so no dedupe op now!");
                return;
            }

            /*
             * Prepare the changelog.
             */
            print_file(RECORD_TYPE.FINGERPRINT_RECORD_CLOG, DEDUP_SORT_ORDER.DBN_BASED, input_clog, input_clog + ".txt");
            m_stage = DEDUP_ENGINE_STAGE.SORT_CLOG_INOFBN;
            sort_dedupe_file(RECORD_TYPE.FINGERPRINT_RECORD_CLOG, input_clog, clog_inofbn, DEDUP_SORT_ORDER.INO_FBN_BASED);
            ui.Update_DedupeUI(DEDUP_ENGINE_STAGE.SORT_CLOG_INOFBN, 100);

            print_file(RECORD_TYPE.FINGERPRINT_RECORD_CLOG, DEDUP_SORT_ORDER.INO_FBN_BASED, clog_inofbn, clog_inofbn + ".txt");
            m_stage = DEDUP_ENGINE_STAGE.CLEAN_CLOG_INOFBNSTALES;
            clean_clog_file_P1(clog_inofbn, clog_inofbnclean);
            ui.Update_DedupeUI(DEDUP_ENGINE_STAGE.CLEAN_CLOG_INOFBNSTALES, 100);

            print_file(RECORD_TYPE.FINGERPRINT_RECORD_CLOG, DEDUP_SORT_ORDER.INO_FBN_BASED, clog_inofbnclean, clog_inofbnclean + ".txt");
            m_stage = DEDUP_ENGINE_STAGE.SORT_CLOG_DBN;
            sort_dedupe_file(RECORD_TYPE.FINGERPRINT_RECORD_CLOG, clog_inofbnclean, clog_dbn, DEDUP_SORT_ORDER.DBN_BASED);
            ui.Update_DedupeUI(DEDUP_ENGINE_STAGE.SORT_CLOG_DBN, 100);

            //commenting this off for now.
            print_file(RECORD_TYPE.FINGERPRINT_RECORD_CLOG, DEDUP_SORT_ORDER.DBN_BASED, clog_dbn, clog_dbn + ".txt");
            m_stage = DEDUP_ENGINE_STAGE.CLEAN_CLOG_DBNSTALES;
            clean_clog_file_P2(clog_dbn, clog_dbnclean);
            ui.Update_DedupeUI(DEDUP_ENGINE_STAGE.CLEAN_CLOG_DBNSTALES, 100);

            print_file(RECORD_TYPE.FINGERPRINT_RECORD_CLOG, DEDUP_SORT_ORDER.DBN_BASED, clog_dbnclean, clog_dbnclean + ".txt");
            m_stage = DEDUP_ENGINE_STAGE.SORT_CLOG_FPORDER;
            sort_dedupe_file(RECORD_TYPE.FINGERPRINT_RECORD_CLOG, clog_dbnclean, clog_fporder, DEDUP_SORT_ORDER.FINGERPRINT_BASED);
            print_file(RECORD_TYPE.FINGERPRINT_RECORD_CLOG, DEDUP_SORT_ORDER.FINGERPRINT_BASED, clog_fporder, clog_fporder + ".txt");
            ui.Update_DedupeUI(DEDUP_ENGINE_STAGE.SORT_CLOG_FPORDER, 100);

            remove_file(input_clog);
            remove_file(clog_inofbn);
            remove_file(clog_inofbnclean);
            remove_file(clog_dbn);
            remove_file(clog_dbnclean);

            /*
             * Prepare the fpdb. we have 'clog_fporder' from above steps
             * We must have a delete log that should be applied to clean the FPDB,
             */
            if (File.Exists(fpdb_input)) 
            {
                print_file(RECORD_TYPE.FINGERPRINT_RECORD_FPDB, DEDUP_SORT_ORDER.DBN_BASED, fpdb_input, fpdb_input + ".old.txt");

                m_stage = DEDUP_ENGINE_STAGE.CLEAN_FPDB_DBNSTALES;
                clean_fpdb_P1(fpdb_input, fpdb_dbncleaned);
                remove_file(fpdb_input);
                ui.Update_DedupeUI(DEDUP_ENGINE_STAGE.CLEAN_FPDB_DBNSTALES, 100);
                print_file(RECORD_TYPE.FINGERPRINT_RECORD_FPDB, DEDUP_SORT_ORDER.DBN_BASED, fpdb_dbncleaned, fpdb_dbncleaned + ".txt");

                m_stage = DEDUP_ENGINE_STAGE.SORT_FPDB_FPORDER;
                sort_dedupe_file(RECORD_TYPE.FINGERPRINT_RECORD_FPDB, fpdb_dbncleaned, fpdb_fporder, DEDUP_SORT_ORDER.FINGERPRINT_BASED);
                remove_file(fpdb_dbncleaned);
                print_file(RECORD_TYPE.FINGERPRINT_RECORD_FPDB, DEDUP_SORT_ORDER.FINGERPRINT_BASED, fpdb_fporder, fpdb_fporder + ".txt");
                ui.Update_DedupeUI(DEDUP_ENGINE_STAGE.SORT_FPDB_FPORDER, 100);
            }

            /*
             * Do the dedupe operation. we have 'fpdb_fporder' from above steps.
             */
            m_stage = DEDUP_ENGINE_STAGE.GENERATE_DEDUPE_SCRIPT;
            GenDedupeScript(fpdb_fporder, clog_fporder, fpdb_temp, dedupeScript_a);
            print_file(RECORD_TYPE.FINGERPRINT_RECORD_MSG, DEDUP_SORT_ORDER.FINGERPRINT_BASED, dedupeScript_a, dedupeScript_a + ".txt");
            print_file(RECORD_TYPE.FINGERPRINT_RECORD_FPDB, DEDUP_SORT_ORDER.FINGERPRINT_BASED, fpdb_temp, fpdb_temp + ".txt");
            remove_file(fpdb_fporder);
            remove_file(clog_fporder);
            ui.Update_DedupeUI(DEDUP_ENGINE_STAGE.GENERATE_DEDUPE_SCRIPT, 100);

            /*
            * Create the new fpdb.
            */
            m_stage = DEDUP_ENGINE_STAGE.SORT_FPDBT_DBN;
            sort_dedupe_file(RECORD_TYPE.FINGERPRINT_RECORD_FPDB, fpdb_temp, fpdb_input, DEDUP_SORT_ORDER.DBN_BASED);
            print_file(RECORD_TYPE.FINGERPRINT_RECORD_FPDB, DEDUP_SORT_ORDER.DBN_BASED, fpdb_input, fpdb_input + ".txt");
            remove_file(fpdb_temp);
            ui.Update_DedupeUI(DEDUP_ENGINE_STAGE.SORT_FPDBT_DBN, 100);

            m_stage = DEDUP_ENGINE_STAGE.SORT_SCRIPT_INOFBN;
            sort_dedupe_file(RECORD_TYPE.FINGERPRINT_RECORD_MSG, dedupeScript_a, dedupeScript, DEDUP_SORT_ORDER.INO_FBN_BASED);
            print_file(RECORD_TYPE.FINGERPRINT_RECORD_MSG, DEDUP_SORT_ORDER.INO_FBN_BASED, dedupeScript, dedupeScript + ".txt");
            ui.Update_DedupeUI(DEDUP_ENGINE_STAGE.SORT_SCRIPT_INOFBN, 100);


            //rename the file and get over with it.
            m_stage = DEDUP_ENGINE_STAGE.DEDUPE_MESSAGES;
            ACTUAL_DEDUPE(dedupeScript);
            remove_file(dedupeScript);
            remove_file(dedupeScript_a);
            ui.Update_DedupeUI(DEDUP_ENGINE_STAGE.DEDUPE_MESSAGES, 100);
            m_stage = DEDUP_ENGINE_STAGE.DEDUPE_DONE;

            System.Threading.Thread.Sleep(2000);
            ui.Update_DedupeUI(DEDUP_ENGINE_STAGE.DEDUPE_DONE, 100);
        }

        

        private fingerprintDMSG DUP(fingerprintDMSG msg2)
        {
            fingerprintDMSG obj = new fingerprintDMSG(DEDUP_SORT_ORDER.UNDEFINED_PLACEHOLDER);
            obj.fsid = msg2.fsid;
            obj.inode = msg2.inode;
            obj.fbn = msg2.fbn;
            obj.sourcedbn = msg2.sourcedbn;
            obj.destinationdbn = msg2.destinationdbn;
            //obj.fp
            return obj;
        }

        private int top = -1;
        private int counter = 0;
        private fingerprintDMSG[] msglist = new fingerprintDMSG[1024];
        private void MSG_AGGREGATE(fingerprintDMSG msg)
        {
            if (msg == null) 
            {
                if (counter > 0)
                    REDDY.ptrIFSDMux.DoDedupeBatch(msglist);

                DEFS.DEBUGYELLOW("BATCH", "DoDedupeBatch, counter = " + counter);
                for (int i = 0; i < 1024; i++) msglist[i] = null;
                counter = 0;
                top = -1;
                return;
            }
            else if ((top == -1) || (msg.fsid == msglist[top].fsid && msg.inode == msglist[top].inode &&
                 OPS.SomeFBNToStartFBN(1, msg.fbn) == OPS.SomeFBNToStartFBN(1, msglist[top].fbn)))
            {
                int idx = (int)(msg.fbn % 1024);
                DEFS.ASSERT(msglist[idx] == null, "This cannot be populated already");
                msglist[idx] = DUP(msg);
                top = idx;
                counter++;
            }
            else
            { 
                //send msg here.
                MSG_AGGREGATE(null);
                MSG_AGGREGATE(msg);
                return;
            }
        }
        /*
         * Send the messages and get work done.
         */ 
        private void ACTUAL_DEDUPE(string script)
        {
            FileStream fsrc = new FileStream(script, FileMode.Open);
            Item record = new fingerprintDMSG(DEDUP_SORT_ORDER.UNDEFINED_PLACEHOLDER);

            int count = (int)(fsrc.Length / record.get_size());
            byte[] buffer = new byte[record.get_size()];

            for (int i = 0; i < count; i++)
            {
                fsrc.Read(buffer, 0, record.get_size());
                record.parse_bytes(buffer, 0);

                fingerprintDMSG msg = (fingerprintDMSG)record;
                //REDDY.ptrIFSDMux.DoDedupe(msg.fsid, msg.inode, msg.fbn, msg.sourcedbn, msg.destinationdbn);
                MSG_AGGREGATE(msg);

                int progress = (i * 100) / count;
                ui.Update_DedupeUI(m_stage, progress);
            }
            MSG_AGGREGATE(null);
            fsrc.Close();
        }

        private void print_file(RECORD_TYPE type, DEDUP_SORT_ORDER order, string fpath, string txtfile)
        {
            Item record = null;
            switch (type)
            {
                case RECORD_TYPE.FINGERPRINT_RECORD_CLOG:
                    record = new fingerprintCLOG(order);
                    break;
                case RECORD_TYPE.FINGERPRINT_RECORD_FPDB:
                    record = new fingerprintFPDB(order);
                    break;
                case RECORD_TYPE.FINGERPRINT_RECORD_MSG:
                    record = new fingerprintDMSG(order);
                    break;
            }

            FileStream fsrc = new FileStream(fpath, FileMode.Open);
            FileStream fdest = new FileStream(txtfile, FileMode.Create);
            StreamWriter log = new StreamWriter(fdest);

            int count = (int)(fsrc.Length / record.get_size());
            byte[] buffer = new byte[record.get_size()];

            for (int i = 0; i < count; i++) 
            {
                fsrc.Read(buffer, 0, record.get_size());
                record.parse_bytes(buffer, 0);
                log.WriteLine(record.get_string_rep());
            }
            log.Flush();
            log.Close();
            fsrc.Close();
        }

        private void remove_file(string file)
        {
            File.Delete(file);
            DEFS.DEBUG("DEDUPE", "Removing file " + file);
        }

        private void dup_file(string sourcefile, string destfile)
        {
            DEFS.DEBUG("DEDUPE", "File dup starting : " + sourcefile + " -> " + destfile);
            byte[] buffer = new byte[4096 * 1024];

            FileStream fsrc = new FileStream(sourcefile, FileMode.Open);
            FileStream fdest = new FileStream(destfile, FileMode.Create);

            long work = fsrc.Length;
            while (work > 0)
            {
                int itr = fsrc.Read(buffer, 0, buffer.Length);
                fdest.Write(buffer, 0, itr);
                work -= itr;
            }
            fsrc.Close();
            fdest.Flush();
            fdest.Close();
            DEFS.DEBUG("DEDUPE", "File dup : " + sourcefile + " -> " + destfile + " done");
        }

        private void sort_dedupe_file(RECORD_TYPE type, string input, string output, DEDUP_SORT_ORDER order)
        {
            Item ix = null;
            switch (type)
            { 
                case RECORD_TYPE.FINGERPRINT_RECORD_CLOG:
                    ix = new fingerprintCLOG(order);
                    break;
                case RECORD_TYPE.FINGERPRINT_RECORD_FPDB:
                    ix = new fingerprintFPDB(order);
                    break;
                case RECORD_TYPE.FINGERPRINT_RECORD_MSG:
                    ix = new fingerprintDMSG(order);
                    break;
            }

            SortAPI sapi = new SortAPI(input, output, ix);
            sapi.do_chunk_sort();
            sapi.do_merge_work();
            sapi.close_streams();
        }

        /*
         * Need to look up refcount file/bitmap file.
         */ 
        private void clean_clog_file_P2(string input, string output)
        {
            DEFS.DEBUG("DEDUPE", "Starting clog P2");

            Item fp = new fingerprintCLOG(DEDUP_SORT_ORDER.UNDEFINED_PLACEHOLDER);
            byte[] buffer = new byte[fp.get_size()];

            FileStream fsrc = new FileStream(input, FileMode.Open);
            FileStream fdest = new FileStream(output, FileMode.Create);

            long count = fsrc.Length / fp.get_size();
            int final = 0;

            for (int i = 0; i < count; i++)
            {
                fsrc.Read(buffer, 0, fp.get_size());
                fp.parse_bytes(buffer, 0);
                
                fingerprintCLOG fpt = (fingerprintCLOG)fp;

                if (REDDY.ptrRedFS != null && REDDY.ptrRedFS.is_block_free(fpt.dbn))
                    continue;

                fdest.Write(buffer, 0, fp.get_size());
                final++;
            }
            fsrc.Close();
            fdest.Flush();
            fdest.Close();
            DEFS.DEBUG("DEDUPE", "Finished clog P2, count = " + count + " to " + final);

        }

        /*
         * Remove duplicate ino, fbn entries. keep the lastest one always
         */ 
        private void clean_clog_file_P1(string input, string output)
        {
            DEFS.DEBUG("DEDUPE", "Starting clog P1");

            Item fp = new fingerprintCLOG(DEDUP_SORT_ORDER.UNDEFINED_PLACEHOLDER);
            int last_ino = 0;
            int last_fbn = 0;
            byte[] buffer = new byte[fp.get_size()];

            FileStream fsrc = new FileStream(input, FileMode.Open);
            FileStream fdest = new FileStream(output, FileMode.Create);

            long count = fsrc.Length/fp.get_size();
            int final = 0;

            for (int i = 0; i < count; i++) 
            {
                fsrc.Read(buffer, 0, fp.get_size());
                fp.parse_bytes(buffer, 0);

                fingerprintCLOG fpt = (fingerprintCLOG)fp;
                if (fpt.inode == last_ino && fpt.fbn == last_fbn)
                    continue;
                fdest.Write(buffer, 0, fp.get_size());
                last_ino = fpt.inode;
                last_fbn = fpt.fbn;
                final++;
            }
            fsrc.Close();
            fdest.Flush();
            fdest.Close();
            DEFS.DEBUG("DEDUPE", "Finished clog P1, count = " + count + " to " + final);
        }

        private void clean_fpdb_P1(string input, string output)
        {
            DEFS.DEBUG("DEDUPE", "Starting fpdb P1");
            Item fp = new fingerprintFPDB(DEDUP_SORT_ORDER.INO_FBN_BASED);
            byte[] buffer = new byte[fp.get_size()];

            FileStream fsrc = new FileStream(input, FileMode.Open);
            FileStream fdest = new FileStream(output, FileMode.Create);   
            
            long count = fsrc.Length / fp.get_size();
            int final = 0;

            for (int i = 0; i < count; i++)
            {
                fsrc.Read(buffer, 0, fp.get_size());
                fp.parse_bytes(buffer, 0);
                fingerprintFPDB fpt = (fingerprintFPDB)fp;

                if (REDDY.ptrRedFS != null && REDDY.ptrRedFS.is_block_free(fpt.dbn))
                    continue;

                fdest.Write(buffer, 0, fp.get_size());
                final++;
            }
            fsrc.Close();
            fdest.Flush();
            fdest.Close();
            DEFS.DEBUG("DEDUPE", "Finished fpdb P1, count = " + count + " to " + final);        
        }

        private int compare_fp(byte[] fp1, byte[] fp2)
        {
            for (int i = 0; i < 16; i++)
            {
                if (fp1[i] < fp2[i]) return -1;
                else if (fp1[i] > fp2[i]) return 1;
            }
            return 0;               
        }

        /*
         * The crux.
         */ 
        private void GenDedupeScript(string fpdbdata, string clogdata, string mergedfile, string scriptfilepath)
        {
            int existing_dbn_counter = 0;
            DEFS.DEBUG("DEDUPE", "starting dedupe op genscript");
            FileStream fsrcfpdb = null;
            if (File.Exists(fpdbdata) == true) 
            {
                fsrcfpdb = new FileStream(fpdbdata, FileMode.Open);
            }
            FileStream fsrcclog = new FileStream(clogdata, FileMode.Open);
            FileStream fdest = new FileStream(mergedfile, FileMode.Create);
            FileStream scriptfile = new FileStream(scriptfilepath, FileMode.Create);

            fingerprintCLOG fpclog = new fingerprintCLOG(DEDUP_SORT_ORDER.UNDEFINED_PLACEHOLDER);
            fingerprintFPDB fpfpdb = new fingerprintFPDB(DEDUP_SORT_ORDER.UNDEFINED_PLACEHOLDER);
            fingerprintDMSG fpmsg = new fingerprintDMSG(DEDUP_SORT_ORDER.UNDEFINED_PLACEHOLDER);

            int clogrecsize = ((Item)fpclog).get_size();
            int fpdbrecsize = ((Item)fpfpdb).get_size();
            int fpmsgrecsize = ((Item)fpmsg).get_size();

            int clogcnt = (int)(fsrcclog.Length / clogrecsize);

            byte[] currfp = new byte[16];
            int currdbn = -1;

            byte[] buffer1 = new byte[clogrecsize];
            byte[] buffer2 = new byte[fpdbrecsize];
            byte[] buffer3 = new byte[fpmsgrecsize];

            while (clogcnt-- > 0)
            { 
                fsrcclog.Read(buffer1, 0, clogrecsize);
                ((Item)fpclog).parse_bytes(buffer1, 0);

                if (compare_fp(currfp, fpclog.fp) != 0)
                {
                    for (int i = 0; i < 16; i++) { currfp[i] = fpclog.fp[i]; }
                    currdbn = fpclog.dbn;

                    //check if this fp is thre in fpdb, if yes get the dbn.
                    bool foundinfpdb = false;

                    if (fsrcfpdb != null && fsrcfpdb.Position < fsrcfpdb.Length)
                    {
                        do
                        {
                            fsrcfpdb.Read(buffer2, 0, fpdbrecsize);
                            ((Item)fpfpdb).parse_bytes(buffer2, 0);
                            fdest.Write(buffer2, 0, fpdbrecsize);
                            //Console.WriteLine("Read FPDB file : " + fsrcfpdb.Position + " : " + ((Item)fpfpdb).get_string_rep() + " curr=" + currdbn);

                            if (compare_fp(currfp, fpfpdb.fp) == 0)
                            {
                                currdbn = fpfpdb.dbn; //let dedupe to old block preferably
                                foundinfpdb = true;
                                existing_dbn_counter++;
                                break;
                            }
                            else if ((compare_fp(currfp, fpfpdb.fp) > 0))
                            {
                                fsrcfpdb.Position -= fpdbrecsize;
                                break;
                            }
                        } while (fsrcfpdb.Position < fsrcfpdb.Length);
                    }

                    if (foundinfpdb == false)
                    {
                        //write to new fpdb, which was encounted from newly written data.
                        fpfpdb.dbn = currdbn;
                        for (int i = 0; i < 16; i++) { fpfpdb.fp[i] = currfp[i]; }
                        ((Item)fpfpdb).get_bytes(buffer2, 0);
                        fdest.Write(buffer2, 0, fpdbrecsize);
                    }
                }

                //dont have to copy the same duplicates. i.e the first dbn which we saw from some file
                //need not be deduped to the same file right?

                if (currdbn != fpclog.dbn)
                {
                    //push this to the messagequeue scriptfile
                    fpmsg.fsid = fpclog.fsid;
                    fpmsg.inode = fpclog.inode;
                    fpmsg.fbn = fpclog.fbn;
                    fpmsg.sourcedbn = fpclog.dbn;
                    fpmsg.destinationdbn = currdbn;
                    for (int i = 0; i < 16; i++) { fpmsg.fp[i] = fpclog.fp[i]; }

                    ((Item)fpmsg).get_bytes(buffer3, 0);
                    scriptfile.Write(buffer3, 0, fpmsgrecsize);
                }
            }

            if(fsrcfpdb != null) fsrcfpdb.Close();
            fsrcclog.Close();
            fdest.Flush();
            fdest.Close();
            scriptfile.Flush();
            scriptfile.Close();

            DEFS.DEBUG("DEDUPE", "finishing dedupe op Genscript : EXISTING : " + existing_dbn_counter);
        }
    }
}
