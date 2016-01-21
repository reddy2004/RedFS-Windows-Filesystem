using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace redfs_v2
{
    public enum CMDCODE
    { 
        UNKNOWN,
        DUMP_INODE_CONTENTS
    }

    public class Option
    {
        public string key;
        public string[] values;
        public int type = -1; //0-int, 1-string
        public bool is_compusory = false;
        public bool marked = false;

        public Option(bool c, string opname, string[] vs)
        {
            key = opname;
            values = vs;
            is_compusory = c;
        }

        public Option(bool c, string opname, int t)
        {
            key = opname;
            type = t;
            is_compusory = c;
        }

        public bool checkIfKeyValueIsValid(string kstr, string vstr)
        {
            if (kstr.Trim() != key) return false;
            if (vstr == null) return true;
            if (type == 0)
            {
                int vint = 0;
                try { vint = Int32.Parse(vstr); }
                catch (Exception e) { DEFS.DEBUG("AFILL", e.Message); return false; }
            }
            return true;
        }

        public void print_contents()
        {
            string toprint = ((is_compusory) ? "*" : "");

            toprint += "\t" + key;
            if (type != -1)
            {
                toprint += "\t" + ((type == 0) ? "?INT?" : "?STRING?");
            }
            else
            {
                string vlist = "";
                for (int k = 0; k < values.Length; k++) { vlist += values[k] + ((k == (values.Length - 1)) ? "" : ","); }
                toprint += "\t" + vlist;
            }
            Console.WriteLine(toprint);
        }

        public string[] get_valuelist(string pkey)
        {
            if (key != pkey) return null;

            if (type != -1)
            {
                string[] ret = new string[1];
                if (type == 0) { ret[0] = "INT"; return ret; }
                else { ret[0] = "STRING"; return ret; }
            }
            else
            {
                return values;
            }
        }
    }

    /*
     * Have couple of cmd option, out of which only one can be choosen.
     */
    public class OptionGroup
    {
        List<Option> opts = new List<Option>();
        public bool is_compusory = false;
        public bool marked = false;

        public OptionGroup(bool c)
        {
            is_compusory = c;
        }

        //not yet done
        public string autofill_value(string partial)
        {
            return null;
        }

        public string autofill_key(string partial)
        {
            for (int i = 0; i < opts.Count; i++)
            {
                Option o = opts.ElementAt(i);
                if (o.key.IndexOf(partial) == 0) return o.key;
            }
            return null;
        }

        public string get_key_rep()
        {
            string keyall = "";
            for (int i = 0; i < opts.Count; i++)
            {
                Option o = opts.ElementAt(i);
                keyall += o.key + ((i == (opts.Count - 1)) ? ' ' : '/');
            }
            return keyall;
        }

        public string[] get_valuelist(string key)
        {
            for (int i = 0; i < opts.Count; i++)
            {
                Option o = opts.ElementAt(i);
                if (o.key == key) return o.get_valuelist(key);
            }
            return null;
        }

        public void insert_cmdoption(Option op)
        {
            opts.Add(op);
        }

        public bool checkIfKeyValueIsValid(string key, string value)
        {
            for (int i = 0; i < opts.Count; i++)
            {
                Option op = opts.ElementAt(i);
                if (op.checkIfKeyValueIsValid(key, value)) return true;
            }
            return false;
        }

        public void print_contents()
        {
            string toprint = ((is_compusory) ? "*" : "") + "\t";

            string keyall = "";
            string valueall = "";

            for (int i = 0; i < opts.Count; i++)
            {
                Option o = opts.ElementAt(i);
                keyall += o.key + ((i == (opts.Count - 1)) ? ' ' : '/');

                string value = "";
                if (o.type != -1)
                {
                    value = (o.type == 0) ? "?INT?" : "?STRING?";
                }
                else
                {
                    string vlist = "";
                    for (int k = 0; k < o.values.Length; k++) { vlist += o.values[k] + ((k == (o.values.Length - 1)) ? "" : ","); }
                    value = vlist;
                }
                valueall += value + ((i == (opts.Count - 1)) ? ' ' : '/');
            }

            toprint += keyall + "\t\t" + valueall;

            Console.WriteLine(toprint);
        }
    }

    public class SubCommand
    {
        public string subcmd_name;
        int[] maincmd_ids;

        List<Option> options = new List<Option>();
        List<OptionGroup> optionsG = new List<OptionGroup>();

        public SubCommand(string name)
        {
            subcmd_name = name;
        }

        public string autofill_value(string key, string partial)
        {
            return null;
        }

        public string autofill_key(string partial)
        {
            for (int i = 0; i < options.Count; i++)
            {
                Option op = options.ElementAt(i);
                if (op.marked == false && op.key.IndexOf(partial) == 0)
                {
                    op.marked = true;
                    return op.key;
                }
            }

            for (int i = 0; i < optionsG.Count; i++)
            {
                OptionGroup op = optionsG.ElementAt(i);
                string str1;
                if (op.marked == false && (str1 = op.autofill_key(partial)) != null)
                {
                    op.marked = true;
                    return str1;
                }
            }
            return null;
        }

        /*
         * 'marked' options will be included, rest need not be.!!
         */
        public string[] get_possible_values(string key)
        {
            for (int i = 0; i < options.Count; i++)
            {
                Option op = options.ElementAt(i);
                if (op.marked == true && op.key == key)
                {
                    return op.get_valuelist(key);
                }
            }

            for (int i = 0; i < optionsG.Count; i++)
            {
                OptionGroup op = optionsG.ElementAt(i);
                if (op.marked == true && op.checkIfKeyValueIsValid(key, null))
                {
                    return op.get_valuelist(key);
                }
            }
            return null;
        }

        public string[] get_all_user_options()
        {
            string[] prepare = new string[100];
            int counter = 0;

            for (int i = 0; i < options.Count; i++)
            {
                Option op = options.ElementAt(i);
                if (op.marked == false) prepare[counter++] = op.key;
            }

            for (int i = 0; i < optionsG.Count; i++)
            {
                OptionGroup op = optionsG.ElementAt(i);
                if (op.marked == false) prepare[counter++] = op.get_key_rep();
            }

            string[] retval = new string[counter];
            for (int i = 0; i < counter; i++)
            {
                retval[i] = "\t" + prepare[i];
            }
            return retval;
        }

        public void clearMarked()
        {
            for (int i = 0; i < options.Count; i++)
            {
                Option op = options.ElementAt(i);
                op.marked = false;
            }

            for (int i = 0; i < optionsG.Count; i++)
            {
                OptionGroup op = optionsG.ElementAt(i);
                op.marked = false;
            }
        }

        public bool checkIfKeyValueIsValid(bool autofillquery, string key, string value)
        {
            for (int i = 0; i < options.Count; i++)
            {
                Option op = options.ElementAt(i);
                if (op.marked == false || autofillquery)
                {
                    if (op.checkIfKeyValueIsValid(key.Trim(), value))
                    {
                        if (!autofillquery) op.marked = true;
                        return true;
                    }
                }
            }

            for (int i = 0; i < optionsG.Count; i++)
            {
                OptionGroup op = optionsG.ElementAt(i);
                if (op.marked == false || autofillquery)
                {
                    if (op.checkIfKeyValueIsValid(key.Trim(), value))
                    {
                        if (!autofillquery) op.marked = true;
                        return true;
                    }
                }
            }
            return false;
        }

        /*
         * could be a single cmdoption, or multiple.
         */
        public void parse_cmdoption_string(string str)
        {
            bool is_comp = false;

            char[] arr = str.ToCharArray(0, 1);
            if (arr[0] == '*') is_comp = true;

            if (str.IndexOf('/') == -1)
            {
                do_single_op_parsing(is_comp, str);
            }
            else
            {
                do_multiple_op_parsing(is_comp, str);
            }
        }

        private void do_single_op_parsing(bool c, string str)
        {
            string newstr = (c) ? str.Substring(1) : str;
            char[] sep = { ' ', '\t' };
            string[] tokens = newstr.Split(sep, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < tokens.Length; i++)
            {
                tokens[i] = tokens[i].Trim();
                Console.WriteLine(tokens[i]);
            }

            string cmdopkey = tokens[0];
            string[] cmdopvalues = tokens[1].Split(',');

            Option newcmdop;
            if (cmdopvalues.Length == 1)
            {
                int type = (tokens[1].IndexOf("INT") != -1) ? 0 : 1;
                newcmdop = new Option(c, cmdopkey, type);
            }
            else
            {
                newcmdop = new Option(c, cmdopkey, cmdopvalues);
            }
            options.Add(newcmdop);
        }

        private void do_multiple_op_parsing(bool c, string str)
        {
            string newstr = (c) ? str.Substring(1) : str;
            char[] sep = { ' ', '\t' };
            string[] tokens = newstr.Split(sep, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < tokens.Length; i++)
            {
                tokens[i] = tokens[i].Trim();
            }

            string[] cmdopkey_all = tokens[0].Split('/');
            string[] cmdopvalues_all = tokens[1].Split('/');

            OptionGroup cmdgroup = new OptionGroup(c);

            for (int i = 0; i < cmdopkey_all.Length; i++)
            {
                string cmdopkey = cmdopkey_all[i];
                string[] cmdopvalues = cmdopvalues_all[i].Split(',');

                Option newcmdop;
                if (cmdopvalues.Length == 1)
                {
                    int type = (tokens[1].IndexOf("INT") != -1) ? 0 : 1;
                    newcmdop = new Option(c, cmdopkey, type);
                }
                else
                {
                    newcmdop = new Option(c, cmdopkey, cmdopvalues);
                }
                cmdgroup.insert_cmdoption(newcmdop);
            }
            optionsG.Add(cmdgroup);
        }

        public void set_maincmd_id(int cmdid)
        {
            if (maincmd_ids == null)
            {
                maincmd_ids = new int[1];
                maincmd_ids[0] = cmdid;
            }
            else
            {
                int[] newlist = new int[maincmd_ids.Length + 1];
                for (int i = 0; i < maincmd_ids.Length; i++) newlist[i] = maincmd_ids[i];
                newlist[maincmd_ids.Length] = cmdid;
                maincmd_ids = newlist;
            }
        }

        public bool is_valid_for_maincmd(int mid)
        {
            for (int i = 0; i < maincmd_ids.Length; i++)
            {
                if (maincmd_ids[i] == mid) return true;
            }
            return false;
        }

        public void print_contents()
        {
            string mids = "";
            for (int i = 0; i < maincmd_ids.Length; i++)
            {
                mids += maincmd_ids[i] + ((i == (maincmd_ids.Length - 1)) ? "" : ",");
            }
            Console.WriteLine("subcmd   " + subcmd_name + " " + mids);

            for (int i = 0; i < options.Count; i++)
            {
                Option o = options.ElementAt(i);
                o.print_contents();
            }

            for (int i = 0; i < optionsG.Count; i++)
            {
                OptionGroup og = optionsG.ElementAt(i);
                og.print_contents();
            }
        }
    }

    public class AutoFill
    {
        string[] maincmd = new string[100];
        int maincmdcnt = 0;
        SubCommand[] subcommands = new SubCommand[100];
        int subcommandscnt = 0;

        public AutoFill(string fpath)
        {
            FileStream fs = new FileStream(fpath, FileMode.Open);
            StreamReader sr = new StreamReader(fs);

            string line = "";
            bool subcmdoptions = false;
            SubCommand currsubcmd = null;

            while ((line = sr.ReadLine()) != null)
            {
                line = line.Trim();

                Console.WriteLine("loop " + line);

                if (line.Length == 0)
                {
                    if (subcmdoptions)
                    {
                        subcmdoptions = false;
                        subcommands[subcommandscnt++] = currsubcmd;
                        currsubcmd = null;
                        Console.WriteLine("     saved subcommand full");
                    }
                    else
                    {
                        Console.WriteLine("     ignore blank line");
                    }
                    continue;
                }
                char[] array = line.ToCharArray(0, 1);
                if (array[0] == '#')
                {
                    Console.WriteLine("     ignore comment line");
                    continue;
                }

                if (line.IndexOf("maincmd", 0) == 0)
                {
                    char[] sep = { ' ', '\t' };
                    string[] tokens = line.Split(sep, StringSplitOptions.RemoveEmptyEntries);

                    for (int i = 0; i < tokens.Length; i++) { tokens[i] = tokens[i].Trim(); }
                    //int idx = Int32.Parse(tokens[2]);
                    maincmd[maincmdcnt++] = tokens[1];
                    Console.WriteLine("     parsed maincmd line");
                    continue;
                }

                if (subcmdoptions)
                {
                    currsubcmd.parse_cmdoption_string(line);
                    Console.WriteLine("     parse key-value line");
                    continue;
                }

                if (line.IndexOf("subcmd", 0) == 0)
                {
                    char[] sep = { ' ', '\t' };
                    string[] tokens = line.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < tokens.Length; i++) { tokens[i] = tokens[i].Trim(); }

                    string[] midstr = tokens[2].Split(',');
                    currsubcmd = new SubCommand(tokens[1]);
                    for (int xi = 0; xi < midstr.Length; xi++)
                    {
                        currsubcmd.set_maincmd_id(Int32.Parse(midstr[xi]));
                    }

                    Console.WriteLine("     Create new subcmd entry");
                    subcmdoptions = true;
                    continue;
                }
            }

            if (subcmdoptions)
            {
                subcmdoptions = false;
                subcommands[subcommandscnt++] = currsubcmd;
                currsubcmd = null;
                Console.WriteLine("     saved subcommand full");
            }
            Console.WriteLine("------------------------------------");
            fs.Close();
        }

        private SubCommand userStrToSubCmdClass(int mid, string token)
        {

            string tokandup = token.Trim();
            Console.WriteLine("userStrToSubCmdClass() " + tokandup);

            for (int i = 0; i < subcommandscnt; i++)
            {
                if (tokandup == subcommands[i].subcmd_name &&
                    subcommands[i].is_valid_for_maincmd(mid))
                    return subcommands[i];
            }
            return null;
        }

        private int userStrToMainCmdID(string userstr)
        {
            string userstrdup = userstr.Trim();
            for (int i = 0; i < maincmdcnt; i++)
            {
                if (userstrdup.IndexOf(maincmd[i]) == 0 && userstrdup.Length == maincmd[i].Length) return i;
            }
            return -1;
        }

        private void clearMarked()
        {
            for (int i = 0; i < subcommandscnt; i++)
            {
                subcommands[i].clearMarked();
            }
        }

        private string[] autofill_useroptions(string userstr)
        {
            char[] sep = { ' ', '\t' };
            string[] tokens = userstr.Split(sep, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length == 0)
            {
                string[] retval = new string[maincmdcnt];
                for (int i = 0; i < maincmdcnt; i++)
                {
                    retval[i] = "\t" + maincmd[i];
                }
                return retval;
            }
            else if (tokens.Length == 1)
            {
                int mid = userStrToMainCmdID(tokens[0]);
                //populate all the subcommands that can go with this one.
                string[] supportedsubcmds = new string[100];
                int counter = 0;
                for (int s = 0; s < subcommandscnt; s++)
                {
                    if (subcommands[s].is_valid_for_maincmd(mid))
                    {
                        supportedsubcmds[counter++] = subcommands[s].subcmd_name;
                    }
                }
                string[] retval = new string[counter];
                for (int i = 0; i < counter; i++)
                {
                    retval[i] = "\t" + supportedsubcmds[i];
                }
                return retval;
            }
            else if (tokens.Length >= 2)
            {
                if (tokens.Length % 2 == 0)
                {
                    int mid = userStrToMainCmdID(tokens[0]);
                    SubCommand subcmd = userStrToSubCmdClass(mid, tokens[1]);
                    return subcmd.get_all_user_options();
                }
                else
                {
                    //odd number, so we must be looking at a key, so show the values to user for that key.
                    string key = tokens[tokens.Length - 1];
                    int mid = userStrToMainCmdID(tokens[0]);
                    SubCommand subcmd = userStrToSubCmdClass(mid, tokens[1]);
                    Console.WriteLine(">>>>>>> key = " + key);
                    return subcmd.get_possible_values(key);
                }
            }
            //no reach
            return null;
        }

        private string autofill_string(string userstr)
        {
            Console.WriteLine("::: In autofill_string = " + userstr);

            char[] sep = { ' ', '\t' };
            string[] tokens = userstr.Split(sep, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length == 1)
            {
                //try to match maincmd
                for (int i = 0; i < maincmdcnt; i++)
                {
                    if (maincmd[i].IndexOf(tokens[0]) != -1) return maincmd[i] + " ";
                }
                return null;
            }
            else if (tokens.Length == 2)
            {
                int mid = userStrToMainCmdID(tokens[0]);
                if (mid == -1) return null;

                //try to match maincmd
                for (int i = 0; i < subcommandscnt; i++)
                {
                    if (subcommands[i].subcmd_name.IndexOf(tokens[1]) == 0 &&
                            subcommands[i].is_valid_for_maincmd(mid))
                        return maincmd[mid] + " " + subcommands[i].subcmd_name + " ";
                }
                return null;
            }
            else if (tokens.Length >= 3) //3 or greater.
            {
                //we have a token, so we have to search for autofill option.
                Console.WriteLine("tokenlen 3+ " + userstr);
                string finalretval = "";

                int mid = userStrToMainCmdID(tokens[0]);
                if (mid == -1) return null;

                //try to match maincmd
                SubCommand subcmd = null;
                for (int i = 0; i < subcommandscnt; i++)
                {
                    if (subcommands[i].subcmd_name == tokens[1] &&
                            subcommands[i].is_valid_for_maincmd(mid))
                    {
                        subcmd = subcommands[i];
                    }
                }
                if (subcmd == null) return null;

                finalretval += maincmd[mid] + " " + subcmd.subcmd_name + " ";

                for (int i = 2; i < tokens.Length; i += 2)
                {
                    Console.WriteLine("I=" + i + " t=" + tokens[i] + " final=" + finalretval);
                    string ukey = tokens[i];
                    string uvalue = ((i + 1) > (tokens.Length - 1)) ? null : tokens[i + 1];

                    //below must be modified to autofill value if the key is correct!. - todo.

                    if (subcmd.checkIfKeyValueIsValid(true, ukey, uvalue) == false)
                    {
                        string str2 = subcmd.autofill_key(tokens[i]);
                        Console.WriteLine("^^^^ failed verificatioin, now do autofill ^^^ str2 = " + str2);
                        if (str2 == null) return null;
                        return finalretval + str2 + ((uvalue == null) ? " " : " " + uvalue);
                    }
                    else
                    {
                        finalretval += (ukey + " " + uvalue + " ");
                    }
                }
            }
            //cannot reach
            return null;
        }

        private bool verifyIntegrity(string userstr)
        {
            char[] sep = { ' ', '\t' };
            string[] tokens = userstr.Split(sep, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length == 0) return true;

            int mid = userStrToMainCmdID(tokens[0]);

            if (mid == -1) return false;
            Console.WriteLine("verify integrity : token[0] = " + tokens[0] + " len = " + tokens.Length);

            if (tokens.Length == 1) return true;

            SubCommand subcmd = userStrToSubCmdClass(mid, tokens[1]);
            Console.WriteLine("verify integrity : token[1] = " + tokens[1] + " sub=null? = " + (subcmd == null));
            if (subcmd == null) return false;

            if (tokens.Length == 2) return true;

            /*
             * Now start working with token pairs
             */
            for (int i = 2; i < tokens.Length; i += 2)
            {
                string ukey = tokens[i];
                string uvalue = ((i + 1) > (tokens.Length - 1)) ? null : tokens[i + 1];

                if (subcmd.checkIfKeyValueIsValid(false, ukey, uvalue) == false)
                {
                    return false;
                }
                else if (uvalue == null)
                {
                    return true;
                }
            }
            return true;
        }
        /*
         * this is the autofill logic. Either we tell the caller to autofill commands or we
         * ask him to display options.
         */
        public string[] get_next_strsforuser(string mystr, ref bool autofill_or_optiondisplay)
        {
            clearMarked();
            if (verifyIntegrity(mystr) == false)
            {
                string autofillstr = autofill_string(mystr);
                clearMarked();
                if (autofillstr != null)
                {
                    autofill_or_optiondisplay = true;
                    string[] retx = { autofillstr };
                    return retx;
                }
                else
                {
                    autofill_or_optiondisplay = false;
                    string[] ret2x = { "Failed to parse command!" };
                    return ret2x;
                }
            }
            else
            {
                //integrity is okay, so we must return only options to display;
                autofill_or_optiondisplay = false;
                string[] ret = autofill_useroptions(mystr);
                clearMarked();
                return ret;
            }
        }

        /*
         * Should print exactly what was understood from the autofill file.
         */
        public void print_contents()
        {
            for (int i = 0; i < maincmdcnt; i++)
            {
                Console.WriteLine("maincmd  " + maincmd[i] + "  " + i);
            }
            Console.WriteLine();

            for (int i = 0; i < subcommandscnt; i++)
            {
                subcommands[i].print_contents();
                Console.WriteLine();
            }
        }
    }

    public class SupportedCommand
    {
        public SupportedCommand(string cmdtemplate)
        { 
        
        
        }
      
        //
        // returns 'n' parameters in order specified in cmdtemplate code. Must do permutation checks etc.
        //
        public string[] check_if_match(string cmduser, ref CMDCODE code)
        {
            code = CMDCODE.UNKNOWN;
            return null;
        }
    }

    public class IFSDCommandParser
    {
        List<SupportedCommand> scmds = new List<SupportedCommand>(10);

        public IFSDCommandParser()
        {
            FileStream fs = new FileStream("supported_cmds.txt", FileMode.Open);
            StreamReader sr = new StreamReader(fs);

            string line = null;
            while ((line = sr.ReadLine()) != null)
            {
                SupportedCommand cmd = new SupportedCommand(line);
                scmds.Add(cmd);
            }
        }

        private string[] parse(string command, ref CMDCODE code)
        {
            for (int i = 0; i < scmds.Count; i++) 
            {
                SupportedCommand curr = scmds.ElementAt(i);
                string[] ret = curr.check_if_match(command, ref code);
                if (ret != null)
                {
                    return ret;
                }
            }
            return null;
        }

        public string[] do_command(string command)
        {
            CMDCODE code = CMDCODE.UNKNOWN;
            string[] parameters = parse(command, ref code);
            string[] retval = new string[1];

            switch (code)
            { 
                case CMDCODE.DUMP_INODE_CONTENTS:

                    break;
            }

            return retval;
        }
    }


    public class StreamString
    {
        private AutoFill autofill;

        private Stream ioStream;
        private UnicodeEncoding streamEncoding;
        private int historyidx = -1;
        private int relative_cursor_position = 0;

        String mystring;
        IFSDCommandParser CMDEXEC = new IFSDCommandParser();

        List<string> history = new List<string>();

        private void rewrite_display_line()
        {
            ioStream.WriteByte((byte)'\r');
            for (int i = 0; i < (8 + mystring.Length + 2); i++)
                write_full_str("\x1B[1C"); //whitespaces i think
            backspaces(mystring.Length + 8 + 2);
            write_full_str("program>");
            write_full_str(mystring);
            cursor_set_relative_position();
        }

        private void cursor_set_relative_position()
        {
            ioStream.WriteByte((byte)'\r');
            for (int i = 0; i < (8 + relative_cursor_position); i++)
                write_full_str("\x1B[1C");
            ioStream.Flush();
        }

        private void cursor_backward()
        {
            //ioStream.WriteByte((byte)27);
            //ioStream.WriteByte((byte)68);
            //ioStream.WriteByte((byte)1);
            //ioStream.WriteByte((byte)'B');
            write_full_str("\x1B[1D");
            ioStream.Flush();
        }

        private void backspaces(int count)
        {
            for (int i = 0; i < count; i++)
            {
                ioStream.WriteByte((byte)127);
            }
        }

        private void new_line_command()
        {
            ioStream.WriteByte((byte)'\r');
            ioStream.WriteByte((byte)'\n');
        }
        private void write_full_str(string str)
        {
            char[] carray = str.ToCharArray();

            for (int i = 0; i < carray.Length; i++)
            {
                ioStream.WriteByte((byte)carray[i]);
            }
        }

        public StreamString(Stream ioStream)
        {
            this.ioStream = ioStream;
            streamEncoding = new UnicodeEncoding();
        }

        public void start_service()
        {
            autofill = new AutoFill("autofill.txt");
            autofill.print_contents();
            mystring = "";

            while (true)
            {
                try
                {
                    if (readchar() == false)
                        break;
                }
                catch (IOException e)
                {
                    Console.WriteLine("ERROR: {0}", e.Message);
                    break;
                }
            }
        }

        private bool readchar()
        {
            byte b = (byte)ioStream.ReadByte();

            if (b == 13)
            {
                /* dont recopy (unmodified) history ones */
                if (historyidx < 0 && mystring != null && mystring.Length > 0)
                {
                    history.Insert(0, mystring);
                }
                else if (historyidx >= 0 && mystring != null && mystring.Length > 0 && mystring != history.ElementAt(historyidx))
                {
                    history.Insert(0, mystring);
                }

                if (mystring == "exit")
                {
                    //System.Environment.Exit(0);
                    return false;
                }
                else
                { 
                    //the command must be processed here. check integrity, if its okay, then pass it on to IFSD_Mux
                    //else display an error to the user.
                    string[] result = CMDEXEC.do_command(mystring);
                    for (int i = 0; i < result.Length; i++) 
                    {
                        new_line_command();
                        write_full_str(result[i]);
                    }
                    new_line_command();
                    new_line_command();
                    write_full_str("program>");
                }
                mystring = "";
                relative_cursor_position = 0;
                historyidx = -1;
            }
            else if (b == 127) //backspace
            {
                if (mystring.Length == 0)
                {
                    mystring = "";
                    relative_cursor_position = 0;
                }
                else
                {
                    if (relative_cursor_position == mystring.Length)
                    {
                        mystring = mystring.Substring(0, mystring.Length - 1);
                        relative_cursor_position--;
                        ioStream.WriteByte(b);
                    }
                    else if (relative_cursor_position == 0)
                    {
                        //dont do anything.
                    }
                    else
                    {
                        string mynewstring = mystring.Substring(0, relative_cursor_position - 1) +
                                mystring.Substring(relative_cursor_position, mystring.Length - relative_cursor_position);
                        mystring = mynewstring;
                        relative_cursor_position--;
                        //ioStream.WriteByte(b);
                        rewrite_display_line();
                    }

                }
            }
            else if (b == 27)
            {
                byte b1 = (byte)ioStream.ReadByte();
                byte b2 = (byte)ioStream.ReadByte();
                if (b1 == 91)
                {
                    if (b2 == 65) //up
                    {
                        historyidx = (historyidx < (history.Count - 1)) ? (historyidx + 1) : historyidx;
                        if (historyidx >= 0)
                        {
                            backspaces(mystring.Length);
                            mystring = (string)history.ElementAt(historyidx);
                            write_full_str(mystring);
                        }
                        Console.WriteLine("from history:" + mystring);
                        relative_cursor_position = mystring.Length;
                    }
                    else if (b2 == 66) //down
                    {
                        historyidx = (historyidx >= 1) ? (historyidx - 1) : historyidx;
                        if (historyidx >= 0)
                        {
                            backspaces(mystring.Length);
                            mystring = (string)history.ElementAt(historyidx);
                            write_full_str(mystring);
                        }
                        Console.WriteLine("from history 2:" + mystring);
                        relative_cursor_position = mystring.Length;
                    }
                    else if (b2 == 67) //forward
                    {
                        if (relative_cursor_position >= 0 && relative_cursor_position <= 64)
                        {
                            relative_cursor_position++;
                            //cursor_backward();
                            cursor_set_relative_position();
                        }
                        Console.WriteLine("cursor moved: curr = " + relative_cursor_position + " strlen=" + mystring.Length);
                    }
                    else if (b2 == 68) //backward
                    {
                        if (relative_cursor_position > 0)
                        {
                            relative_cursor_position--;
                            //cursor_backward();
                            cursor_set_relative_position();
                        }
                        Console.WriteLine("cursor moved: curr = " + relative_cursor_position + " strlen=" + mystring.Length);
                    }
                    else
                    {
                        Console.WriteLine("byte " + b + " is unknown control char");
                    }
                }
                //Console.WriteLine(b1 + "," + b2);
            }
            else if (valid_byte(b))
            {
                //Console.WriteLine("byte = " + (char)b + " value = " + b);
                Console.WriteLine("key pressed : " + (char)b + " pos=" + relative_cursor_position +
                    " string = " + mystring);

                if (mystring == null)
                {
                    mystring += (char)b;
                    relative_cursor_position = 1;
                    ioStream.WriteByte(b);
                }
                else
                {
                    if (relative_cursor_position == mystring.Length)
                    {
                        mystring += (char)b;
                        relative_cursor_position++;
                        ioStream.WriteByte(b);
                    }
                    else
                    {
                        //Console.WriteLine(mystring.Length);
                        //int sz = mystring.Length;
                        //Console.WriteLine(mystring.Substring(relative_cursor_position, sz -1));
                        //Console.WriteLine(mystring.Substring(0, relative_cursor_position - 1));
                        string substr = (char)b + "";
                        mystring = mystring.Insert(relative_cursor_position, substr);

                        //string mynewstring = mystring.Substring(0, relative_cursor_position -1) + 
                        //    (char)b + mystring.Substring(relative_cursor_position, mystring.Length);
                        //mystring = mynewstring;
                        relative_cursor_position++;
                        cursor_set_relative_position();

                        string toappend = mystring.Substring(relative_cursor_position, mystring.Length - relative_cursor_position);
                        backspaces(1);
                        ioStream.WriteByte(b);
                        write_full_str(toappend);
                        Console.WriteLine("append : " + toappend);
                        cursor_set_relative_position();
                    }
                    //mystring += (char)b;
                }
            }
            else if (b == 9)
            {
                bool autofill_or_optiondisplay = false;

                /* User pressed tab, so parse correctly */
                //new_line_command();
                string[] display = autofill.get_next_strsforuser(mystring, ref autofill_or_optiondisplay);
                Console.WriteLine("autofill_or_optiondisplay = " + autofill_or_optiondisplay);
                if (autofill_or_optiondisplay == false)
                {
                    new_line_command();
                    for (int i = 0; i < display.Length; i++)
                    {
                        write_full_str(display[i]);
                        new_line_command();
                    }
                    new_line_command();
                    rewrite_display_line();
                }
                else
                {
                    mystring = display[0];
                    relative_cursor_position = mystring.Length;
                    Console.WriteLine("--> " + display[0]);
                    rewrite_display_line();
                }

            }
            else
            {
                Console.WriteLine("byte " + b + " cannot be parsed correctly");
            }
            ioStream.Flush();
            return true;
        }

        private bool valid_byte(byte b)
        {
            char ch = (char)b;

            if (ch >= 'a' && ch <= 'z')
                return true;
            if (ch >= '0' && ch <= '9')
                return true;
            if (ch == '-' || ch == ' ' || ch == '.' || ch == '\\' || ch == '/')
                return true;
            if (ch >= 'A' && ch <= 'Z')
                return true;
            return false;
        }
    }

    public class Putty_Client_Session
    {
        public bool thread_done = false;

        public Putty_Client_Session()
        { 
        
        }

        public void start_listener()
        {
            NamedPipeServerStream pipeServer =
                new NamedPipeServerStream("testpipe", PipeDirection.InOut, 1);

            pipeServer.WaitForConnection();

            Console.WriteLine("Client connected..");

            StreamString ss = new StreamString(pipeServer);
            ss.start_service();

            Console.WriteLine("Client disconnected..");
            pipeServer.Close();
            thread_done = true;   
        }
    }

    class ProgramX2
    {
        private static bool shutdown = false;

        public static void StartPuttyListener()
        {
            Thread t = new Thread(Session_Monitor);
            t.Start();
        }

        public static void StopPuttyListener()
        {
            shutdown = true;
        }

        private static void Session_Monitor()
        {
            while (shutdown == false)
            {
                Putty_Client_Session newSession = new Putty_Client_Session();
                Thread t = new Thread(newSession.start_listener);
                t.Start();
                while (newSession.thread_done == false) Thread.Sleep(5000);
            }
        
        }
    }
}
