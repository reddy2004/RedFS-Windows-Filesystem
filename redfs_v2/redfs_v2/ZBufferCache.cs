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

namespace redfs_v2
{
    public class ZBufferCache
    {
        private RedBufL0[] iStack = new RedBufL0[1024 * 16 * 4];
        private int iStackTop = 0;

        public ZBufferCache()
        {

        }

        public void init()
        {
            for (int i = 0; i < iStack.Length; i++)
            {
                iStack[i] = new RedBufL0(0);
            }
            iStackTop = iStack.Length - 1;
        }

        public void shutdown()
        {
            if (iStackTop != iStack.Length - 1)
                DEFS.DEBUGYELLOW("Y","Appears that not all bufs are recovered : " + iStackTop);
            //DEFS.ASSERT(iStackTop == iStack.Length - 1, "Appears that not all bufs are recovered : " + iStackTop);
        }

        public RedBufL0 allocate(long sf)
        {
            lock (iStack)
            {
                RedBufL0 wb = iStack[iStackTop];
                iStackTop--;
                wb.reinitbuf(sf);
                return wb;
            }
        }

        public void deallocateList(List<Red_Buffer> wblist)
        {
            int count = wblist.Count;
            for (int i = 0; i < count; i++)
                deallocate4((RedBufL0)wblist.ElementAt(i));
            wblist.Clear();
        }

        public void deallocate4(RedBufL0 wb)
        {
            lock (iStack)
            {
                iStackTop++;
                iStack[iStackTop] = wb;
            }
        }
    }
}
