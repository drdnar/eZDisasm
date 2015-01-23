eZ80 Disassembler
23 January 2015

Usage: eZDisasm [-acdelstxOp] [-b <baseAddress>] [-o <outfile>] <hex string>
 OR    eZDisasm [-acdelstxOp] [-b <baseAddress>] [-o <outfile>] -i file.txt
 OR    eZDisasm [-acdelstxOp] [-b <baseAddress>] [-o <outfile>] -I file.bin
Output disassembly is dumped to stdout.

Options:
 -A (default): Set ADL mode.  Transitions between short and long mode are not
    handled.
 -a: Unset ADL mode.  Transitions between short and long mode are not handled.
 -C (default): Do not output onto one continuous line
 -c: Output onto one continuous line
 -b <address>: Set base address for disassembly, address is in hexadecimal
 -D (default): Do not display address of every instruction
 -d: Show address of every instruction
 -E (default): Set eZ80 disassembly mode, implies -a unless -A is specified
 -e: Set classic Z80 disassembly mode, implies -A
 -i <file>: Read instructions in hex from <file>
 -I <file>: Read instructions in binary format from <file>
 -L (default): Add labels when possible for branches
 -l: Do not add labels for branches
 -O (default): Write output to stdout, not mutually exclusive with -o
 -o <file>: Write output to <file> instead of stdout.
 -S (default): Use spaces for column alignment
 -s: Use tabs for column alignment
 -T (default): Align instruction arguments with whitespace
 -t: Do not alight instruction arguments
 -X (default): Prefix every instruction with its opcode
 -x: Do not show instruction opcodes
 -P (default): Do not pause and wait for a key when done
 -p: Pause when done