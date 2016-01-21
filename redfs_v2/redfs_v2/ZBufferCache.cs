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
