# RedFS-Windows-Filesystem
A userspace file system for Windows (using dokan). Supports snapshots, clones and deduplication of files.

The RedFS filesystem is based on heirarchial refcounts to keep track of block usage. Remember that RedFS file system is laid over a regular NTFS file, but I would like to change this in future such that the file system is laid out on a physical disk.

