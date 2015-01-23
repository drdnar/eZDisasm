eZ80 Disassembler
22 January 2015

Usage: eZDisasm [-eEaAlLxXtTsS] [-b <baseAddress>] <hex string>
 OR    eZDisasm [-eEaAlLxXtTsS] [-b <baseAddress>] -i file.txt
 OR    eZDisasm [-eEaAlLxXtTsS] [-b <baseAddress>] -I file.bin
Output disassembly is dumped to stdout.

Options:
 -a (default): Set ADL mode.  Transitions between short and long mode are not
    handled.
 -A: Unset ADL mode.  Transitions between short and long mode are not handled.
 -b <address>: Set base address for disassembly, address is in hexadecimal
 -d (default): Do not display address of every instruction
 -D: Show address of every instruction
 -e (default): Set eZ80 disassembly mode, implies -a unless -A is specified
 -E: Set classic Z80 disassembly mode, implies -A
 -i <file>: Read instructions in hex from <file>
 -I <file>: Read instructions in binary format from <file>
 -l (default): Add labels when possible for branches
 -L: Do not add labels for branches
 -s (default): Use spaces for column alignment
 -S: Use tabs for column alignment
 -t (default): Align instruction arguments with whitespace
 -T: Do not alight instruction arguments
 -x (default): Prefix every instruction with its opcode
 -X: Do not show instruction opcodes
 -p (default): Do not pause and wait for a key when done
 -P: Pause when done