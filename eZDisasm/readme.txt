eZ80 Disassembler
24 January 2015

Usage: eZDisasm [-options] [-b <baseAddress>] [-o <outfile>] <hexString>
  OR   eZDisasm [-options] [-b <baseAddress>] [-o <outfile>] -i file.txt
  OR   eZDisasm [-options] [-b <baseAddress>] [-o <outfile>] -I file.bin
Output disassembly is dumped to stdout.

Options:
 -a, --short-mode 
    Use short mode, not ADL mode
    Transitions between short and long mode are not handled
 -b <address>, --base-address
    Set base address for disassembly, address is in hexadecimal
 -c, --irc-mode
    Output onto one continuous line
 -d, --show-addresses
    Prefix every instruction with its address
 -E, --eZ80 (default)
    Set eZ80 disassembly mode
 -e, --Z80
    Set classic Z80 disassembly mode, implies -a
 -f, --text-dump
    Convert input into text .db statements instead of disassembly
 -g, --ascii
    Convert input into ASCII .db statements instead of disassembly
 -h, --hex-dump
    Convert input into hex .db statements instead of disassembly
 -H, --word-dump
    Convert input into hex .dw statements instead of disassembly
    NOTE: This will print 24-bit words unless -A or -e is set
 -i <file>, --infile <file>
    Read instructions in hex from <file>
 -I <file>, --binfile <file>
    Read instructions in binary format from <file>
 -k <number>, --line-length <number>
    For -h or -H, print <number> bytes/words per line
 -K <hexNumer>, --line-length-hex <hexNumber>
    Same as -k but input in hexadecimal
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
 -r <address>, --from <address>
    Start disassembly range at <address>
 -R <address>, --to <address>
    Stop disassembly range at <address>
 -s, --pad-tabs
    Use tabs for column alignment instead of spaces
 -t, --no-align
    Do not align instruction arguments
 -x, --hide-opcodes
    Do not show instruction opcodes
 -z, --no-append
    Do not append to outfile; instead overwrite it