using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
#if WIN_32
using System.Runtime.InteropServices;
#endif

// Could be useful to add an option for reading from stdin.
// Also, a way to only read from part of a .bin.
// And a way to specify append, not overwrite, for outfile.
// Also support --argname in addition to single-char switches.

namespace eZDisasm
{
    class Program
    {
#if WIN_32

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint GetConsoleProcessList(uint[] ProcessList, uint ProcessCount);
#endif
        enum ArgumentType
        {
            BaseAddress,
            InputFile,
            BinaryInputFile,
            OutputFile,

        }
        
        enum StringArgumentType
        {
            BaseAddress,
            InputFileName,
            OutputFileName,
            StartAddress,
            EndAddress,
        }

        enum ErrorCode
        {
            NoError,
            NoArguments,
            BadArgument,
            ConflictingArgument,
            DuplicateArgument,
            MissingImpliedArgument,
            InvalidHexString,
            FileOpenError,
        }

        static bool pause = false;

#if WIN_32
        static bool newConsole = false;
#endif

        static int Main(string[] args)
        {
#if WIN_32
            // Don't call Win32 functions from Mono
            if (Type.GetType("Mono.Runtime") == null)
                // Are we the only process using this console?
                switch (GetConsoleProcessList(new uint[] { 0 }, 1))
                {
                    case 0:
                        Console.WriteLine("Internal error: Could not get console process list.");
                        break;
                    case 1:
                        // Yes, we are the only process using this console, so
                        // hold it open when done unless overridden with --no-pause
                        newConsole = pause = true;
                        break;
                    default:
                        pause = false;
                        break;
                }
#endif
            // Parse arguments
            if (args.Length == 0)
            {
                ShowHelp();
                return (int)ErrorCode.NoArguments;
            }

            int curArg = 0;

            int baseAddress = 0;
            bool hasBaseAddress = false;
            bool readInputFile = false;
            bool binaryInputFile = false;
            string inputFileName = "";
            bool z80ClassicMode = false;
            bool adlMode = true;
            bool addLabels = true;
            bool showOpcodes = true;
            bool alignArgs = true;
            bool useTabs = false;
            bool showAddresses = false;
            bool forceWriteStdOut = false;
            bool writeStdOut = true;
            bool writeOutputFile = false;
            bool stdin = false;
            string outputFileName = "";
            bool ircMode = false;
            bool append = true;
            int start = 0;
            int end = 0;
            bool dumpMode = false;
            bool hexMode = true;
            bool wordMode = false;
            
            #region Parse Arguments
            Queue<StringArgumentType> expectedArgs = new Queue<StringArgumentType>();

            Action seteZ80Mode = () => z80ClassicMode = false;
            Action setZ80Mode = () => { z80ClassicMode = true; adlMode = false; };
            Action setShortMode = () => adlMode = false;
            Action setShowLabels = () => addLabels = true;
            Action unsetShowLabels = () => addLabels = false;
            Action setAlignArgs = () => alignArgs = true;
            Action unsetAlignArgs = () => alignArgs = false;
            Action setUseTabs = () => useTabs = true;
            Action unsetUseTabs = () => useTabs = false;
            Action setIrcMode = () => ircMode = true;
            Action unsetIrcMode = () => ircMode = false;
            Action setStdIn = () => stdin = true;
            Action setWriteStdOut = () => writeStdOut = forceWriteStdOut = true;
            Action setShowAddresses = () => showAddresses = true;
            Action unsetShowAddresses = () => showAddresses = false;
            Action setShowOpcodes = () => showOpcodes = true;
            Action unsetShowOpcodes = () => showOpcodes = false;
            Action setPause = () => pause = true;
            Action unsetPause = () => pause = false;
            Action setAppend = () => append = true;
            Action unsetAppend = () => append = false;
            Action setLongMode = () =>
                {
                    if (z80ClassicMode)
                        ShowShortHelp(ErrorCode.ConflictingArgument, "Error: -A is mutually exclusive with -e");
                    adlMode = true;
                };
            Action setWriteOutFile = () =>
                {
                    if (writeOutputFile)
                        ShowShortHelp(ErrorCode.DuplicateArgument, "Error: Duplicate outfile specifier");
                    if (!forceWriteStdOut)
                        writeStdOut = false;
                    writeOutputFile = true;
                    expectedArgs.Enqueue(StringArgumentType.OutputFileName);
                };
            Action setBaseAddress = () =>
                {
                    if (hasBaseAddress)
                        ShowShortHelp(ErrorCode.DuplicateArgument, "Error: Duplicate base address specifier");
                    hasBaseAddress = true;
                    expectedArgs.Enqueue(StringArgumentType.BaseAddress);
                };
            Action setReadFile = () =>
                {
                    if (readInputFile)
                        ShowShortHelp(ErrorCode.DuplicateArgument, "Error: Duplicate infile specifier");
                    if (stdin)
                        ShowShortHelp(ErrorCode.ConflictingArgument, "Error: -i is mutually exclusive with with -n");
                    readInputFile = true;
                    binaryInputFile = false;
                    expectedArgs.Enqueue(StringArgumentType.InputFileName);
                };
            Action setBinFile = () =>
                {
                    if (readInputFile)
                        ShowShortHelp(ErrorCode.DuplicateArgument, "Error: Duplicate infile specifier");
                    if (stdin)
                        ShowShortHelp(ErrorCode.ConflictingArgument, "Error: -I is mutually exclusive with with -n");
                    readInputFile = true;
                    binaryInputFile = true;
                    expectedArgs.Enqueue(StringArgumentType.InputFileName);
                };
            Action setStartAddr = () =>
                {
                    if (start != 0)
                        ShowShortHelp(ErrorCode.DuplicateArgument, "Error: Duplicate start address");
                    expectedArgs.Enqueue(StringArgumentType.StartAddress);
                };
            Action setEndAddr = () =>
                {
                    if (end != 0)
                        ShowShortHelp(ErrorCode.DuplicateArgument, "Error: Duplicate end address");
                    expectedArgs.Enqueue(StringArgumentType.EndAddress);
                };
            Action setHexMode = () =>
                {
                    if (wordMode)
                        ShowShortHelp(ErrorCode.ConflictingArgument, "Error: Byte hex mode is mutually exclusive with word hex mode");
                    if (dumpMode)
                        ShowShortHelp(ErrorCode.ConflictingArgument, "Error: Hex mode is mutually exclusive with ASCII mode");
                    dumpMode = true;
                    wordMode = false;
                    hexMode = true;
                };
            Action setWordMode = () =>
                {
                    if (dumpMode)
                        if (hexMode && !wordMode)
                            ShowShortHelp(ErrorCode.ConflictingArgument, "Error: Byte hex mode is mutually exclusive with word hex mode");
                        else if (!hexMode)
                            ShowShortHelp(ErrorCode.ConflictingArgument, "Error: Word hex mode is mutually exclusive with ASCII mode");
                    dumpMode = true;
                    wordMode = true;
                    hexMode = true;
                };
            Action setAsciiMode = () =>
                {
                    if (dumpMode)
                        if (hexMode)
                            ShowShortHelp(ErrorCode.ConflictingArgument, "Error: ASCII mode is mutually exclusive with hex mode");
                    dumpMode = true;
                    hexMode = false;
                    wordMode = false;
                };

            while (curArg < args.Length)
            {
                if (args[curArg].Length != 0)
                {
                    if (expectedArgs.Count() > 0)
                    {
                        try
                        {
                            switch (expectedArgs.Dequeue())
                            {
                                case StringArgumentType.BaseAddress:
                                    baseAddress = Convert.ToInt32(args[curArg], 16);
                                    break;
                                case StringArgumentType.InputFileName:
                                    inputFileName = args[curArg];
                                    break;
                                case StringArgumentType.OutputFileName:
                                    outputFileName = args[curArg];
                                    break;
                                case StringArgumentType.StartAddress:
                                    start = Convert.ToInt32(args[curArg], 16);
                                    break;
                                case StringArgumentType.EndAddress:
                                    end = Convert.ToInt32(args[curArg], 16);
                                    break;
                            }
                        }
                        catch (FormatException)
                        {
                            ShowShortHelp(ErrorCode.BadArgument, "Error: Invalid number " + args[curArg]);
                        }
                        catch (OverflowException)
                        {
                            ShowShortHelp(ErrorCode.BadArgument, "Error: Invalid number " + args[curArg]);
                        }
                        curArg++;
                    }
                    else if (curArg == args.Length - 1 && Regex.IsMatch(args[curArg], "^([0-9A-Fa-f]{1,6}[\\s\\r\\n\\:\\.]*:)?([\\s\\r\\n\\:\\.]*[0-9A-Fa-f][0-9A-Fa-f])+[\\s\\r\\n\\:\\.]*$"))
                        break;
                    else
                    {
                        if (args[curArg][0] == '-')
                        {
                            if (args[curArg].Length == 1)
                            {
                                ShowShortHelp(ErrorCode.BadArgument, "Error: Bare - without option character");
                            }
                            if (args[curArg][1] == '-')
                            {
                                switch (args[curArg])
                                {
                                    case "--help":
                                        ShowHelp();
                                        break;
                                    case "--short-mode":
                                        setShortMode();
                                        break;
                                    case "--base-address":
                                        setBaseAddress();
                                        break;
                                    case "--eZ80":
                                    case "--ez80":
                                    case "--Ez80": // I hate you if you use these.
                                    case "--EZ80":
                                        seteZ80Mode();
                                        break;
                                    case "--Z80":
                                    case "--z80":
                                        setZ80Mode();
                                        break;
                                    case "--infile":
                                        setReadFile();
                                        break;
                                    case "--binfile":
                                        setBinFile();
                                        break;
                                    case "--no-labels":
                                        unsetShowLabels();
                                        break;
                                    case "--stdout":
                                        setWriteStdOut();
                                        break;
                                    case "--outfile":
                                        setWriteOutFile();
                                        break;
                                    case "--pad-tabs":
                                        setUseTabs();
                                        break;
                                    case "--no-align":
                                        unsetAlignArgs();
                                        break;
                                    case "--hide-opcodes":
                                        setShowOpcodes();
                                        break;
                                    case "--no-pause":
                                        unsetPause();
                                        break;
                                    case "--pause":
                                        setPause();
                                        break;
                                    case "--stdin":
                                        setStdIn();
                                        break;
                                    case "--append":
                                        setAppend();
                                        break;
                                    case "--no-append":
                                        unsetAppend();
                                        break;
                                    case "--irc-mode":
                                        setIrcMode();
                                        break;
                                    case "--from":
                                        setStartAddr();
                                        break;
                                    case "--to":
                                        setEndAddr();
                                        break;
                                    default:
                                        ShowShortHelp(ErrorCode.BadArgument, "Error: Unrecognized option " + args[curArg]);
                                        return (int)ErrorCode.BadArgument;
                                }
                            }
                            else
                            {
                                for (int i = 1; i < args[curArg].Length; i++)
                                    switch (args[curArg][i])
                                    {
                                        case 'b':
                                            setBaseAddress();
                                            break;
                                        case 'i':
                                            setReadFile();
                                            break;
                                        case 'I':
                                            setBinFile();
                                            break;
                                        case 'E':
                                            seteZ80Mode();
                                            break;
                                        case 'e':
                                            setZ80Mode();    
                                            break;
                                        case 'A':
                                            setLongMode();
                                            break;
                                        case 'a':
                                            setShortMode();
                                            break;
                                        case 'L':
                                            setShowLabels();
                                            break;
                                        case 'l':
                                            unsetShowLabels();
                                            break;
                                        case 'X':
                                            setShowOpcodes();
                                            break;
                                        case 'x':
                                            unsetShowLabels();
                                            break;
                                        case 'T':
                                            setAlignArgs();
                                            break;
                                        case 't':
                                            unsetAlignArgs();
                                            break;
                                        case 'S':
                                            unsetUseTabs();
                                            break;
                                        case 's':
                                            setUseTabs();
                                            break;
                                        case 'D':
                                            unsetShowAddresses();
                                            break;
                                        case 'd':
                                            setShowAddresses();
                                            break;
                                        case 'P':
                                            unsetPause();
                                            break;
                                        case 'p':
                                            setPause();
                                            break;
                                        case 'o':
                                            setWriteOutFile();
                                            break;
                                        case 'O':
                                            setWriteStdOut();
                                            break;
                                        case 'c':
                                            setIrcMode();
                                            break;
                                        case 'C':
                                            unsetIrcMode();
                                            break;
                                        case 'n':
                                            setStdIn();
                                            break;
                                        case 'Z':
                                            setAppend();
                                            break;
                                        case 'z':
                                            unsetAppend();
                                            break;
                                        case 'r':
                                            setStartAddr();
                                            break;
                                        case 'R':
                                            setEndAddr();
                                            break;
                                        default:
                                            ShowShortHelp(ErrorCode.BadArgument, "Error: Unrecognized option -" + args[curArg][i]);
                                            return (int)ErrorCode.BadArgument;
                                    }
                            }
                        }
                        else
                            ShowShortHelp(ErrorCode.BadArgument, "Error: Bad argument " + args[curArg]);
                        curArg++;
                    }
                }
            }

            if (expectedArgs.Count > 0)
            {
                while (expectedArgs.Count > 1)
                    Console.Error.WriteLine("Error: Missing implied argument " + expectedArgs.Dequeue().ToString());
                ShowShortHelp(ErrorCode.MissingImpliedArgument, "Error: Missing implied argument " + expectedArgs.Dequeue().ToString());
            }
            #endregion

            byte[] data = new byte[] {0};
            string inputText = "";

            if (stdin)
            {
                StringBuilder b = new StringBuilder();
                string s;
                while ((s = Console.ReadLine()) != null)
                    b.Append(s);
                inputText = b.ToString();
            }
            else
                if (readInputFile)
                {
                    try
                    {
                        if (binaryInputFile)
                            data = System.IO.File.ReadAllBytes(inputFileName);
                        else
                            inputText = System.IO.File.ReadAllText(inputFileName);
                    }
                    catch
                    {
                        ShowShortHelp(ErrorCode.FileOpenError, "Error opening input file " + inputFileName);
                    }
                }
                else
                    inputText = args[args.Length - 1];

            if (!binaryInputFile)
            {
                Regex baseAddressPrefixRegex = new Regex("^\\s*[0-9A-Fa-f]{1,6}[\\s\\r\\n\\:\\.]*\\:");
                if (baseAddressPrefixRegex.IsMatch(inputText))
                {
                    if (hasBaseAddress)
                        ShowShortHelp(ErrorCode.ConflictingArgument, "Error: Input string has base address specifier, which conflicts with -b argument.");
                    hasBaseAddress = true;
                    baseAddress = Convert.ToInt32(Regex.Match(inputText, "[0-9A-Fa-f]{1,6}").Value, 16);
                    inputText = baseAddressPrefixRegex.Replace(inputText, "", 1);
                }
                if (!Regex.IsMatch(inputText, "^([\\s\\r\\n\\:\\.]*[0-9A-Fa-f][0-9A-Fa-f])+[\\s\\r\\n\\:\\.]*$"))
                    ShowShortHelp(ErrorCode.InvalidHexString, "Error: input is not valid hex string");
                List<byte> bytes = new List<byte>();
                foreach (Match m in Regex.Matches(inputText, "[0-9A-Fa-f][0-9A-Fa-f]"))
                    bytes.Add(Convert.ToByte(m.Value, 16));
                data = bytes.ToArray();
            }

            if (end == 0)
                end = data.Length;
            else
                end -= baseAddress;
            if (end >= data.Length)
                end = data.Length - 1;
            start -= baseAddress;
            if (start > end)
                ShowShortHelp(ErrorCode.BadArgument, "Error: End address cannot be before start address");
            if (start < 0)
                ShowShortHelp(ErrorCode.BadArgument, "Error: Start address is before start of input data");

            StreamsWriter writer = new StreamsWriter();
            if (writeStdOut)
                writer.StdOutWriter = Console.Out;
            if (writeOutputFile)
            try
            {
                writer.FileWriter = new StreamWriter(outputFileName, append);
            }
            catch
            {
                ShowShortHelp(ErrorCode.FileOpenError, "Error opening output file " + outputFileName);
            }
            
            int addrColWidth = 8;
            int opcodeColWidth = 14;
            int instrColWidth = 8;
                            
            if (!dumpMode)
            {
                eZ80Disassembler.DisassembledInstruction[] instrs =
                    eZ80Disassembler.Disassemble(data, start, end, baseAddress, hasBaseAddress, adlMode, z80ClassicMode, addLabels ? "label_" : "", addLabels ? "loc_" : "");

                HashSet<int> knownLabels = new HashSet<int>();

                if (addLabels)
                    foreach (eZ80Disassembler.DisassembledInstruction instr in instrs)
                        if (instr.IsBranch)
                            knownLabels.Add(instr.BranchTarget);

                for (int j = 0; j < instrs.Length; j++)
                {
                    eZ80Disassembler.DisassembledInstruction instr = instrs[j];
                    if (addLabels && knownLabels.Contains(instr.StartPosition + baseAddress))
                    {
                        if (showAddresses)
                            if (useTabs)
                                writer.Write("\t");
                            else
                                for (int c = 0; c < addrColWidth; c++)
                                    writer.Write(" ");
                        if (showOpcodes)
                            if (useTabs)
                                writer.Write("\t\t");
                            else
                                for (int c = 0; c < opcodeColWidth; c++)
                                    writer.Write(" ");
                        writer.Write("label_");
                        if (z80ClassicMode)
                            writer.Write(((instr.StartPosition + baseAddress) & 0xFFFF).ToString("X4"));
                        else
                            writer.Write((instr.StartPosition + baseAddress).ToString("X6"));
                        writer.Write(":");
                        if (!ircMode)
                            writer.WriteLine();
                        else
                            writer.Write(" ");
                    }
                    if (showAddresses)
                    {
                        if (z80ClassicMode)
                            writer.Write((instr.StartPosition + baseAddress).ToString("X4"));
                        else
                            writer.Write((instr.StartPosition + baseAddress).ToString("X6"));
                        writer.Write(":");
                        if (useTabs)
                            writer.Write("\t");
                        else
                            for (int c = z80ClassicMode ? 5 : 7; c < addrColWidth; c++)
                                writer.Write(" ");
                    }
                    if (showOpcodes)
                    {
                        for (int i = 0; i < instr.Length; i++)
                            writer.Write(data[instr.StartPosition + i].ToString("X2"));
                        if (useTabs)
                        {
                            if (instr.Length <= 3)
                                writer.Write("\t");
                            writer.Write("\t");
                        }
                        else
                            for (int c = 2 * instr.Length; c < opcodeColWidth; c++)
                                writer.Write(" ");
                    }
                    else
                        if (!ircMode)
                            if (useTabs)
                                writer.Write("\t");
                            else
                                writer.Write("    ");
                    writer.Write(instr.InstructionName);
                    writer.Write(instr.InstructionSuffix);
                    if (!String.IsNullOrEmpty(instr.InstructionArguments))
                    {
                        if (alignArgs)
                            if (useTabs)
                                writer.Write("\t");
                            else
                            {
                                for (int c = instr.InstructionName.Length + instr.InstructionSuffix.Length - 1; c < instrColWidth; c++)
                                    writer.Write(" ");
                                writer.Write(" ");
                            }
                        else
                            writer.Write(" ");
                        writer.Write(instr.InstructionArguments);
                    }
                    if (j != instrs.Length - 1)
                        if (!ircMode)
                            writer.WriteLine();
                        else
                            writer.Write(" \\ ");
                }
            }
            else
            {
                int addr = start;
                while (addr++ < end)
                {
                    if (showAddresses)
                    {
                        if (z80ClassicMode)
                            writer.Write((instr.StartPosition + baseAddress).ToString("X4"));
                        else
                            writer.Write((instr.StartPosition + baseAddress).ToString("X6"));
                        writer.Write(":");
                        if (useTabs)
                            writer.Write("\t");
                        else
                            for (int c = z80ClassicMode ? 5 : 7; c < addrColWidth; c++)
                                writer.Write(" ");
                    }
                    if (showOpcodes)
                    {
                        for (int c = ; c < opcodeColWidth; c++)
                            writer.Write(" ");
                    }
                    else
                        if (!ircMode)
                            if (useTabs)
                                writer.Write("\t");
                            else
                                writer.Write("    ");
                }    
            }
            
            if (pause)
                Console.ReadKey();

            writer.Close();
            return (int)ErrorCode.NoError;
        }

        class StreamsWriter
        {
            public StreamWriter FileWriter;
            public TextWriter StdOutWriter;

            public void Write(string str)
            {
                if (FileWriter != null)
                    FileWriter.Write(str);
                if (StdOutWriter != null)
                    StdOutWriter.Write(str);
            }

            public void WriteLine(string str)
            {
                if (FileWriter != null)
                    FileWriter.WriteLine(str);
                if (StdOutWriter != null)
                    StdOutWriter.WriteLine(str);
            }

            public void WriteLine()
            {
                if (FileWriter != null)
                    FileWriter.WriteLine();
                if (StdOutWriter != null)
                    StdOutWriter.WriteLine();
            }

            public void Close()
            {
                if (FileWriter != null)
                    FileWriter.Dispose();
            }
        }

#if WIN_32
        static void ShowDummyHelp()
        {
            Console.WriteLine("eZDisasm, an eZ80 and classic Z80 disassembler");
            Console.WriteLine();
            Console.WriteLine("This is a command-line application.  You must run it from the command line.");
            Console.WriteLine("It is not an interactive application.");
            Console.WriteLine("For syntaxic help:");
            Console.WriteLine("    eZDisasm --help");
        }
#endif

        static void ShowShortHelp(ErrorCode e, string msg)
        {
#if WIN_32
            if (newConsole && pause)
            {
                ShowDummyHelp();
                Console.WriteLine();
                Console.Write("Message: ");
                Console.Error.WriteLine(msg);
                Console.ReadKey();
                Environment.Exit((int)e);
            }
#endif
            //Console.WriteLine(msg);
            Console.Error.WriteLine(msg);
            Console.WriteLine("Usage: eZDisasm [-acdelstxOp] [-b <base>] [-o <outfile>] {-i/I <infile> | <hex>}");
            Console.WriteLine("For help: eZDisasm --help");
#if DEBUG
            Console.ReadKey();
#else
            if (pause)
                Console.ReadKey();
#endif
            Environment.Exit((int)e);
        }

        static void ShowHelp()
        {
#if WIN_32
            if (newConsole && pause)
            {
                ShowDummyHelp();
                Console.ReadKey();
                return;
            }
#endif            
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream("eZDisasm.readme.txt"))
                using (StreamReader reader = new StreamReader(stream))
                    Console.WriteLine(reader.ReadToEnd());
            if (pause)
                Console.ReadKey();
            Environment.Exit((int)ErrorCode.NoError);
        }
    }
}
