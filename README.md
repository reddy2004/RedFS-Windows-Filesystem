# RedFS-Windows-Filesystem
A userspace file system for Windows (using dokan). Supports snapshots, clones and deduplication of files.

The RedFS filesystem is based on heirarchial refcounts to keep track of block usage. Remember that RedFS file system is laid over a regular NTFS file, but I would like to change this in future such that the file system is laid out on a physical disk.

The following are the op complexities in RedFS.

O(1) - Volume snapshot/clone
O(1) - File clone within a volume
O(1) - Cross volume file clone
O(n) - Directory clone withing a volume
O(n) - Directory clone across volumes.

Infinity - The number of supported snapshots. Reads/Writes are not impacted by the number of clones/snapshots.
Infinity - The number of file clones allowed.

RedFS also supports LUNS which can be formatted and mounted as NTFS/FAT drives. LUNS can be cloned/snapshotted!
RedFS supports file delta back ups.

Deduplication works at the block level, hence all data across all volumes become candidates for dedupe pattern matching. 

