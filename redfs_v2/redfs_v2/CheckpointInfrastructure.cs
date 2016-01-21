using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;

namespace redfs_v2
{
    public class CheckpointInfrastructure
    {
        string path = CONFIG.GetBasePath() + "\\checkpoints\\";

        public CheckpointInfrastructure()
        {
    
        }

        public void take_deleted_wip_checkpoint(BlockingCollection<RedFS_Inode> m_wipdelete_queue)
        { 
        
        }

        public void reload_deleted_wip_checkpoint(BlockingCollection<RedFS_Inode> m_wipdelete_queue)
        { 
        
        }
    }
}
