eZ80 Disassembler
23 January 2015

Usage: eZDisasm [-acdelstxOp] [-b <baseAddress>] [-o <outfile>] <hex string>
  OR   eZDisasm [-acdelstxOp] [-b <baseAddress>] [-o <outfile>] -i file.txt
  OR   eZDisasm [-acdelstxOp] [-b <baseAddress>] [-o <outfile>] -I file.bin
Output disassembly is dumped to stdout.

Options:
 -a, --short-mode 
    Use short mode, not ADL mode.  Transitions between short and long mode are
	not handled.
 -c, --irc-mode
    Output onto one continuous line
 -b <address>, --base-address
    Set base address for disassembly, address is in hexadecimal
 -d, --show-addresses
    Prefix every instruction with its address
 -E, --eZ80 (default)
    Set eZ80 disassembly mode
 -e, --Z80
    Set classic Z80 disassembly mode, implies -a
 -i <file>, --infile <file>
    Read instructions in hex from <file>
 -I <file>, --binfile <file>
    Read instructions in binary format from <file>
 -l, --no-labels
    Do not add labels for branches
 -n, --stdin
    Read input from stdin
 -O (default), --stdout
    Write output to stdout, not mutually exclusive with -o
 -o <file>, --outfile <file>
    Write output to <file> instead of stdout.
 -P, --no-pause (default)
    Do not pause to wait for a key when done
 -p, --pause
    Pause when done
 -s, --pad-tabs
    Use tabs for column alignment instead of spaces
 -t, --no-align
    Do not align instruction arguments
 -x, --hide-opcodes
    Do not show instruction opcodes
 -z, --no-append
    Do not append to outfile; instead overwrite it