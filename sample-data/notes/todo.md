# Sample notes

- The backup worker chunks each file (default 4 MB).
- Progress is tracked by bytes copied / total bytes.
- Restore reconstructs files and validates SHA-256 hashes.
