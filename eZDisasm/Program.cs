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
            InputFileName,
            OutputFileName,
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

            #region Parse Arguments
            Queue<ArgumentType> expectedArgs = new Queue<ArgumentType>();
            
            while (curArg < args.Length)
            {
                if (args[curArg].Length != 0)
                {
                    if (expectedArgs.Count() > 0)
                    {
                        switch (expectedArgs.Dequeue())
                        {
                            case ArgumentType.BaseAddress:
                                try
                                {
                                    baseAddress = Convert.ToInt32(args[curArg], 16);
                                }
                                catch (FormatException)
                                {
                                    return ShowShortHelp(ErrorCode.BadArgument, "Error: Invalid number " + args[curArg]);
                                }
                                catch (OverflowException)
                                {
                                    return ShowShortHelp(ErrorCode.BadArgument, "Error: Invalid number " + args[curArg]);
                                }
                                curArg++;
                                break;
                            case ArgumentType.InputFileName:
                                inputFileName = args[curArg++];
                                break;
                            case ArgumentType.OutputFileName:
                                outputFileName = args[curArg++];
                                break;
                        }
                    }
                    else if (curArg == args.Length - 1 && Regex.IsMatch(args[curArg], "^([0-9A-Fa-f]{1,6}[\\s\\r\\n\\:\\.]*:)?([\\s\\r\\n\\:\\.]*[0-9A-Fa-f][0-9A-Fa-f])+[\\s\\r\\n\\:\\.]*$"))
                        break;
                    else
                    {
                        if (args[curArg][0] == '-')
                        {
                            if (args[curArg].Length == 1)
                            {
                                return ShowShortHelp(ErrorCode.BadArgument, "Error: Bare - without option character");
                            }
                            if (args[curArg][1] == '-')
                            {
                                switch (args[curArg])
                                {
                                    case "--help":
                                        ShowHelp();
                                        return (int)ErrorCode.NoError;
                                    case "--short-mode":
                                        adlMode = false;
                                        break;
                                    case "--base-address":
                                        if (hasBaseAddress)
                                                return ShowShortHelp(ErrorCode.DuplicateArgument, "Error: Duplicate base address specifier");
                                            hasBaseAddress = true;
                                            expectedArgs.Enqueue(ArgumentType.BaseAddress);
                                        break;
                                    case "--eZ80":
                                    case "--ez80":
                                    case "--Ez80": // I hate you if you use these.
                                    case "--EZ80":
                                        z80ClassicMode = false;
                                        break;
                                    case "--Z80":
                                    case "--z80":
                                        adlMode = false;
                                        z80ClassicMode = true;
                                        break;
                                    case "--infile":
                                        if (readInputFile)
                                            return ShowShortHelp(ErrorCode.DuplicateArgument, "Error: Duplicate infile specifier");
                                        if (stdin)
                                            return ShowShortHelp(ErrorCode.ConflictingArgument, "Error: --infile is mutually exclusive with with --stdin");
                                        readInputFile = true;
                                        binaryInputFile = false;
                                        expectedArgs.Enqueue(ArgumentType.InputFileName);
                                        break;
                                    case "--binfile":
                                        if (readInputFile)
                                            return ShowShortHelp(ErrorCode.DuplicateArgument, "Error: Duplicate infile specifier");
                                        if (stdin)
                                            return ShowShortHelp(ErrorCode.ConflictingArgument, "Error: --binfile is mutually exclusive with with --stdin");
                                        readInputFile = true;
                                        binaryInputFile = true;
                                        expectedArgs.Enqueue(ArgumentType.InputFileName);
                                        break;
                                    case "--no-labels":
                                        addLabels = false;
                                        break;
                                    case "--stdout":
                                        writeStdOut = true;
                                        forceWriteStdOut = true;
                                        break;
                                    case "--outfile":
                                        if (writeOutputFile)
                                            return ShowShortHelp(ErrorCode.DuplicateArgument, "Error: Duplicate outfile specifier");
                                        expectedArgs.Enqueue(ArgumentType.OutputFileName);
                                        writeOutputFile = true;
                                        if (!forceWriteStdOut)
                                            writeStdOut = false;
                                        break;
                                    case "--pad-tabs":
                                        useTabs = true;
                                        break;
                                    case "--no-align":
                                        alignArgs = false;
                                        break;
                                    case "--hide-opcodes":
                                        showOpcodes = false;
                                        break;
                                    case "--no-pause":
                                        pause = false;
                                        break;
                                    case "--pause":
                                        pause = true;
                                        break;
                                    case "--stdin":
                                        if (readInputFile)
                                            return ShowShortHelp(ErrorCode.ConflictingArgument, "Error: --stdin is mutually exclusive with with --infile and --binfile");
                                        stdin = true;
                                        break;
                                    case "--append":
                                        append = true;
                                        break;
                                    case "--no-append":
                                        append = false;
                                        break;
                                    case "--irc-mode":
                                        ircMode = true;
                                        break;
                                    default:
                                        return ShowShortHelp(ErrorCode.BadArgument, "Error: Unrecognized option " + args[curArg]);
                                }
                            }
                            else
                            {
                                for (int i = 1; i < args[curArg].Length; i++)
                                    switch (args[curArg][i])
                                    {
                                        case 'b':
                                            if (hasBaseAddress)
                                                return ShowShortHelp(ErrorCode.DuplicateArgument, "Error: Duplicate base address specifier");
                                            hasBaseAddress = true;
                                            expectedArgs.Enqueue(ArgumentType.BaseAddress);
                                            break;
                                        case 'i':
                                            if (readInputFile)
                                                return ShowShortHelp(ErrorCode.DuplicateArgument, "Error: Duplicate infile specifier");
                                            if (stdin)
                                                return ShowShortHelp(ErrorCode.ConflictingArgument, "Error: -i is mutually exclusive with with -n");
                                            readInputFile = true;
                                            binaryInputFile = false;
                                            expectedArgs.Enqueue(ArgumentType.InputFileName);
                                            break;
                                        case 'I':
                                            if (readInputFile)
                                                return ShowShortHelp(ErrorCode.DuplicateArgument, "Error: Duplicate infile specifier");
                                            if (stdin)
                                                return ShowShortHelp(ErrorCode.ConflictingArgument, "Error: -I is mutually exclusive with with -n");
                                            readInputFile = true;
                                            binaryInputFile = true;
                                            expectedArgs.Enqueue(ArgumentType.InputFileName);
                                            break;
                                        case 'E':
                                            z80ClassicMode = false;
                                            break;
                                        case 'e':
                                            /*if (adlMode)
                                                return ShowShortHelp(ErrorCode.ConflictingArgument, "Error: -a is mutually exclusive with -E");*/
                                            adlMode = false;
                                            z80ClassicMode = true;
                                            break;
                                        case 'A':
                                            if (z80ClassicMode)
                                                return ShowShortHelp(ErrorCode.ConflictingArgument, "Error: -A is mutually exclusive with -e");
                                            adlMode = true;
                                            break;
                                        case 'a':
                                            adlMode = false;
                                            break;
                                        case 'L':
                                            addLabels = true;
                                            break;
                                        case 'l':
                                            addLabels = false;
                                            break;
                                        case 'X':
                                            showOpcodes = true;
                                            break;
                                        case 'x':
                                            showOpcodes = false;
                                            break;
                                        case 'T':
                                            alignArgs = true;
                                            break;
                                        case 't':
                                            alignArgs = false;
                                            break;
                                        case 'S':
                                            useTabs = false;
                                            break;
                                        case 's':
                                            useTabs = true;
                                            break;
                                        case 'D':
                                            showAddresses = false;
                                            break;
                                        case 'd':
                                            showAddresses = true;
                                            break;
                                        case 'P':
                                            pause = false;
                                            break;
                                        case 'p':
                                            pause = true;
                                            break;
                                        case 'o':
                                            if (writeOutputFile)
                                                return ShowShortHelp(ErrorCode.DuplicateArgument, "Error: Duplicate outfile specifier");
                                            expectedArgs.Enqueue(ArgumentType.OutputFileName);
                                            writeOutputFile = true;
                                            if (!forceWriteStdOut)
                                                writeStdOut = false;
                                            break;
                                        case 'O':
                                            writeStdOut = true;
                                            forceWriteStdOut = true;
                                            break;
                                        case 'c':
                                            ircMode = true;
                                            break;
                                        case 'C':
                                            ircMode = false;
                                            break;
                                        case 'n':
                                            if (readInputFile)
                                            return ShowShortHelp(ErrorCode.ConflictingArgument, "Error: --stdin is mutually exclusive with with --infile and --binfil");
                                            stdin = true;
                                            break;
                                        case 'z':
                                            append = false;
                                            break;
                                        case 'Z':
                                            append = true;
                                            break;
                                        default:
                                            return ShowShortHelp(ErrorCode.BadArgument, "Error: Unrecognized option -" + args[curArg][i]);
                                    }
                            }
                        }
                        else
                            return ShowShortHelp(ErrorCode.BadArgument, "Error: Bad argument " + args[curArg]);
                        curArg++;
                    }
                }
            }

            if (expectedArgs.Count > 0)
            {
                while (expectedArgs.Count > 1)
                    Console.Error.WriteLine("Error: Missing implied argument " + expectedArgs.Dequeue().ToString());
                return ShowShortHelp(ErrorCode.MissingImpliedArgument, "Error: Missing implied argument " + expectedArgs.Dequeue().ToString());
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
                        return ShowShortHelp(ErrorCode.FileOpenError, "Error opening input file " + inputFileName);
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
                        return ShowShortHelp(ErrorCode.ConflictingArgument, "Error: Input string has base address specifier, which conflicts with -b argument.");
                    hasBaseAddress = true;
                    baseAddress = Convert.ToInt32(Regex.Match(inputText, "[0-9A-Fa-f]{1,6}").Value, 16);
                    inputText = baseAddressPrefixRegex.Replace(inputText, "", 1);
                }
                if (!Regex.IsMatch(inputText, "^([\\s\\r\\n\\:\\.]*[0-9A-Fa-f][0-9A-Fa-f])+[\\s\\r\\n\\:\\.]*$"))
                    return ShowShortHelp(ErrorCode.InvalidHexString, "Error: input is not valid hex string");
                List<byte> bytes = new List<byte>();
                foreach (Match m in Regex.Matches(inputText, "[0-9A-Fa-f][0-9A-Fa-f]"))
                    bytes.Add(Convert.ToByte(m.Value, 16));
                data = bytes.ToArray();
            }

            eZ80Disassembler.DisassembledInstruction[] instrs =
                eZ80Disassembler.Disassemble(data, baseAddress, hasBaseAddress, adlMode, z80ClassicMode, addLabels ? "label_" : "", addLabels ? "loc_" : "");

            HashSet<int> knownLabels = new HashSet<int>();

            if (addLabels)
                foreach (eZ80Disassembler.DisassembledInstruction instr in instrs)
                    if (instr.IsBranch)
                        knownLabels.Add(instr.BranchTarget);

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
                    return ShowShortHelp(ErrorCode.FileOpenError, "Error opening output file " + outputFileName);
                }

            for (int j = 0; j < instrs.Length; j++)
            {
                eZ80Disassembler.DisassembledInstruction instr = instrs[j];
                if (addLabels && knownLabels.Contains(instr.StartPosition + baseAddress))
                {
                    if (showAddresses)
                        if (useTabs)
                            writer.Write("\t");
                        else
                            writer.Write("        ");
                    if (showOpcodes)
                        if (useTabs)
                            writer.Write("\t\t");
                        else
                            writer.Write("            ");
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
                        if (z80ClassicMode)
                            writer.Write("   ");
                        else
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
                        writer.Write(new String(' ', 14 - 2 * instr.Length));
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
                            writer.Write(new String(' ', instr.InstructionName.Length + instr.InstructionSuffix.Length < 10 ? 10 - instr.InstructionName.Length - instr.InstructionSuffix.Length : 3));
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

        static int ShowShortHelp(ErrorCode e, string msg)
        {
#if WIN_32
            if (newConsole && pause)
            {
                ShowDummyHelp();
                Console.WriteLine();
                Console.Write("Message: ");
                Console.Error.WriteLine(msg);
                Console.ReadKey();
                return (int)e;
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
            return (int)e;
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
        }
    }
}
