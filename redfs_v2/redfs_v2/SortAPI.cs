using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace redfs_v2
{
    class SortAPI
    {
        Item _item;

        byte[] _internal_cache = null;
        int _internal_cnt = 0;

        byte[] _internal_cache_op = null;
        int _internal_cnt_op = 0;

        Item[] sort_input_array = null;
        int stack_top = 0;

        void PUSH(Item m)
        {
            sort_input_array[stack_top++] = m;
        }

        Item POP()
        {
            return sort_input_array[--stack_top];
        }

        FileStream inputF, outputF;

        public SortAPI(String inputpath, String outputpath, Item _i)
        {
            _item = _i;
            _internal_cache = new byte[_item.get_size() * 1024];

            inputF = new FileStream(inputpath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            outputF = new FileStream(outputpath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        }

        private void insert_item_output(Item i, bool is_last)
        {
            DEFS.ASSERT(_internal_cache_op != null, "Cache memory is empty");
            DEFS.ASSERT(outputF != null, "output file cannot be null in insert-item-output function");

            if (i != null)
            {
                i.get_bytes(_internal_cache_op, _internal_cnt_op * _item.get_size());
                _internal_cnt_op++;
            }

            if (is_last || _internal_cnt_op == 1024)
            {
                outputF.Write(_internal_cache_op, 0, _internal_cnt_op * _item.get_size());
                _internal_cnt_op = 0;
                outputF.Flush();
            }

            if (is_last)
            {
                DEFS.ASSERT(_internal_cnt_op == 0, "_internal_cnt should be zero bcoz we just flushed");
            }
        }


        void insert_item(Item i, bool is_last)
        {
            DEFS.ASSERT(_internal_cache != null, "Cache memory is empty");
            DEFS.ASSERT(inputF != null, "input file cannot be null in insert-item function");

            i.get_bytes(_internal_cache, _internal_cnt * _item.get_size());
            _internal_cnt++;

            if (is_last || _internal_cnt == 1024)
            {
                //print_contents(_internal_cache, _internal_cnt);
                inputF.Write(_internal_cache, 0, _internal_cnt * _item.get_size());
                _internal_cnt = 0;
                inputF.Flush();
            }

            if (is_last)
            {
                DEFS.ASSERT(_internal_cnt == 0, "_internal_cnt should be zero bcoz we just flushed");
                DEFS.DEBUG("SORT", "Finised inserting all test_insert_items");
                inputF.Seek(0, SeekOrigin.Begin);
            }
        }

        private void prepare_next_chunk(int cid)
        {

            int csize = (1024 * 1024) * _item.get_size();
            long offset = (long)cid * csize;
            int bytes = ((inputF.Length - offset) < csize) ? (int)(inputF.Length - offset) : csize;
            int num_items = bytes / _item.get_size();

            inputF.Read(_internal_cache, 0, bytes);

            if (num_items < 1024 * 1024)
            {
                DEFS.DEBUG("SORT", "Processing the last chunk : " + cid);
            }
            else
            {
                DEFS.DEBUG("SORT", "Processing chunk : " + cid);
            }

            for (int i = 0; i < num_items; i++)
            {
                sort_input_array[i].parse_bytes(_internal_cache, _item.get_size() * i);
                //Console.WriteLine(((fingerprint)sort_input_array[i]).dbn);
            }

            Array.Sort(sort_input_array, 0, num_items, _item.get_comparator());

            for (int i = 0; i < num_items; i++)
            {
                sort_input_array[i].get_bytes(_internal_cache, _item.get_size() * i);
                //Console.WriteLine(((fingerprint)sort_input_array[i]).dbn);
            }
            inputF.Seek(cid * (long)csize, SeekOrigin.Begin);
            inputF.Write(_internal_cache, 0, bytes);

        }

        public void do_chunk_sort()
        {

            sort_input_array = new Item[1024 * 1024];
            for (int i = 0; i < 1024 * 1024; i++)
            {
                sort_input_array[i] = _item.create_new_obj();
            }

            int csize = (1024 * 1024) * _item.get_size();
            int num_chunks = (int)((inputF.Length % csize == 0) ? (inputF.Length / csize) : (inputF.Length / csize + 1));

            _internal_cache = new byte[_item.get_size() * 1024 * 1024];
            //sort all chunks
            for (int i = 0; i < num_chunks; i++)
            {
                prepare_next_chunk(i);
            }
            _internal_cache = null;
            sort_input_array = null;
        }

        private void populate_vector(int vecid, List<Item>[] _veclist, long[] _offset)
        {
            DEFS.ASSERT(_veclist[vecid].Count == 0, "List must be empty before you replenish");
            long start_offset = _offset[vecid];
            long end_offset = (long)(vecid + 1) * (1024 * 1024 * (long)_item.get_size()) - 1;

            if (end_offset > inputF.Length) end_offset = inputF.Length;

            if (end_offset - start_offset >= _item.get_size())
            {
                int num_items = 0;
                if (end_offset - start_offset >= _item.get_size() * 1024)
                {
                    num_items = 1024;
                }
                else
                {
                    num_items = (int)((end_offset - start_offset) / _item.get_size());
                }

                inputF.Seek(start_offset, SeekOrigin.Begin);
                inputF.Read(_internal_cache, 0, num_items * _item.get_size());

                for (int i = 0; i < num_items; i++)
                {
                    Item x = POP();
                    x.parse_bytes(_internal_cache, i * _item.get_size());
                    x.set_cookie(vecid);
                    _veclist[vecid].Add(x);
                }
                _offset[vecid] += _item.get_size() * num_items;
            }
            else
            {
                DEFS.DEBUG("SORT", "Finished processing vecid = " + vecid);
            }
        }


        public void do_merge_work()
        {
            int csize = (1024 * 1024) * _item.get_size();
            int num_chunks = (int)((inputF.Length % csize == 0) ? (inputF.Length / csize) : (inputF.Length / csize + 1));
            List<Item>[] _veclist = new List<Item>[num_chunks];
            long[] _offset = new long[num_chunks];
            PriorityQueue pq = new PriorityQueue(num_chunks);

            Console.WriteLine("num chunks in merge " + num_chunks);
            sort_input_array = new Item[1024 * (num_chunks + 1)];
            for (int i = 0; i < sort_input_array.Length; i++)
            {
                PUSH(_item.create_new_obj());
            }
            Console.WriteLine("Pushed " + sort_input_array.Length + " items");

            _internal_cache = new byte[_item.get_size() * 1024];
            _internal_cache_op = new byte[_item.get_size() * 1024];

            for (int i = 0; i < num_chunks; i++)
            {
                _veclist[i] = new List<Item>(1024);
                _offset[i] = (long)i * (1024 * 1024 * (long)_item.get_size());
                populate_vector(i, _veclist, _offset);
                pq.enqueue((Item)_veclist[i][0]);
                _veclist[i].RemoveAt(0);
            }

            long itr = num_chunks;
            while (true)
            {
                Item x = pq.dequeue();

                if (x == null)
                {
                    Console.WriteLine("recieved null value in iteration " + itr);
                    break;
                }
                else
                {
                    //Console.WriteLine("->" + ((fingerprint)x).dbn);
                }
                int idx = x.get_cookie();
                if (_veclist[idx].Count == 0)
                {
                    populate_vector(idx, _veclist, _offset);
                }

                if (_veclist[idx].Count > 0)
                {
                    pq.enqueue((Item)_veclist[idx][0]);
                    _veclist[idx].RemoveAt(0);
                }
                itr++;
                insert_item_output(x, false);
                PUSH(x);
            }
            insert_item_output(null, true);
        }

        public void close_streams()
        {
            inputF.Flush();
            inputF.Close();
            outputF.Flush();
            outputF.Close();
        }
        public void verify_savings()
        {
            DEFS.ASSERT(outputF != null, "Output file cannot be null in verifcation phase");
            long num_items = outputF.Length / _item.get_size();
        }
    }
}
