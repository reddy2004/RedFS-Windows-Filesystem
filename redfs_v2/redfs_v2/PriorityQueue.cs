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
using System.Collections;

namespace redfs_v2
{
    public class PriorityQueue
    {
        Item[] _list;
        int counter = 0;

        public PriorityQueue(int size)
        {
            _list = new Item[size];
        }

        Item get_left_child(int loc)
        {
            return (((loc * 2 + 1) > counter) ? null : _list[loc * 2 + 1]);
        }

        Item get_right_child(int loc)
        {
            return (((loc * 2 + 2) > counter) ? null : _list[loc * 2 + 2]);
        }

        private int compare(Item a, Item b)
        {
            IComparer ic = a.get_comparator();
            return ic.Compare(a, b);
        }

        private void process_upwards(int loc)
        {
            if (loc == 0) return;

            Item c = _list[loc];
            Item p = _list[(loc % 2 != 0) ? (loc / 2) : (loc / 2 - 1)];
            if (compare(c, p) < 0)
            {
                _list[loc] = p;
                _list[(loc % 2 != 0) ? (loc / 2) : (loc / 2 - 1)] = c;
                process_upwards((loc % 2 != 0) ? (loc / 2) : (loc / 2 - 1));
            }
        }

        private void process_downwards(int loc)
        {
            if (loc >= counter) return;

            Item p = _list[loc];
            Item c1 = get_left_child(loc);
            Item c2 = get_right_child(loc);

            if ((p == null) || (c1 == null && c2 == null)) return;

            if (c1 != null && c2 == null)
            {
                if (compare(p, c1) <= 0) return;

                _list[loc] = c1;
                _list[loc * 2 + 1] = p;
                process_downwards(loc * 2 + 1);
            }
            else if (c1 != null && c2 != null)
            {
                if (compare(p, c1) <= 0 && compare(p, c2) <= 0) return;

                if (compare(c1, c2) < 0)
                {
                    Item tmp = _list[loc];
                    _list[loc] = c1;
                    _list[loc * 2 + 1] = tmp;
                    process_downwards(loc * 2 + 1);
                }
                else
                {
                    Item tmp = _list[loc];
                    _list[loc] = c2;
                    _list[loc * 2 + 2] = tmp;
                    process_downwards(loc * 2 + 2);
                }
            }
        }

        public bool enqueue(Item i)
        {
            if (counter == _list.Length) return false;

            int pos = counter;
            _list[counter++] = i;
            process_upwards(pos);
            //Console.WriteLine("enqueued + " + ((fingerprint)i).dbn);
            return true;
        }

        public Item dequeue()
        {
            if (counter == 0) return null;

            Item d = _list[0];
            _list[0] = _list[counter - 1];
            counter--;
            process_downwards(0);
            return d;
        }
    }
}
