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


namespace eZDisasm
{
    class Program
    {
#if WIN_32
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int AttachConsole(int dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int AllocConsole();
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern int GetConsoleProcessList(int ptr, int processCount);
        public const int ATTACH_PARENT_PROCESS = -1;
        public const int ERROR_ACCESS_DENIED = 5;
        public const int ERROR_INVALID_HANDLE = 6;
        public const int ERROR_GEN_FAILURE = 31;
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

        static int Main(string[] args)
        {
#if WIN_32
            /*switch (AttachConsole(ATTACH_PARENT_PROCESS))
            {
                case ERROR_ACCESS_DENIED:
                    // Already attached to a console, so do nothing
                    break;
                case ERROR_INVALID_HANDLE:
                case ERROR_GEN_FAILURE:
                default:

                    break;
            }
            
            Console.WriteLine("P: " + pause.ToString());
            Console.ReadKey();*/
            AllocConsole();
            //tPtr[] blah = new IntPtr[] { 0 };
            Console.WriteLine(GetConsoleProcessList(0, 0));
            Console.ReadKey();
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
            string outputFileName = "";
            bool ircMode = false;

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
                    else if (curArg == args.Length - 1 && (new Regex("([0-9A-Fa-f]{1,6}:)?([0-9A-Fa-f][0-9A-Fa-f])+")).IsMatch(args[curArg]))
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
                                                return ShowShortHelp(ErrorCode.DuplicateArgument, "Error: Duplicate -b argument");
                                            hasBaseAddress = true;
                                            expectedArgs.Enqueue(ArgumentType.BaseAddress);
                                            break;
                                        case 'i':
                                            if (readInputFile)
                                                return ShowShortHelp(ErrorCode.DuplicateArgument, "Error: Duplicate -i/-I argument");
                                            readInputFile = true;
                                            binaryInputFile = false;
                                            expectedArgs.Enqueue(ArgumentType.InputFileName);
                                            break;
                                        case 'I':
                                            if (readInputFile)
                                                return ShowShortHelp(ErrorCode.DuplicateArgument, "Error: Duplicate -i/-I argument");
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
                                                return ShowShortHelp(ErrorCode.ConflictingArgument, "Error: -a is mutually exclusive with -E");
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
                                                return ShowShortHelp(ErrorCode.DuplicateArgument, "Error: Duplicate -o argument");
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
                    //Console.WriteLine("Error: Missing implied argument " + expectedArgs.Dequeue().ToString());
                    Console.Error.WriteLine("Error: Missing implied argument " + expectedArgs.Dequeue().ToString());
                return ShowShortHelp(ErrorCode.MissingImpliedArgument, "Error: Missing implied argument " + expectedArgs.Dequeue().ToString());
            }
            /*
            if (hasBaseAddress)
                Console.WriteLine("Base address: " + baseAddress.ToString("X"));
            else
                Console.WriteLine("No base address.");
            if (readInputFile)
                Console.WriteLine("Input file: " + inputFileName);
            else
                Console.WriteLine("No input file.");
            if (binaryInputFile)
                Console.WriteLine("Binary format file.");
            if (z80ClassicMode)
                Console.WriteLine("Z80 classic mode.");
            else
                if (adlMode)
                    Console.WriteLine("eZ80 ADL mode.");
                else
                    Console.WriteLine("eZ80 short mode.");
            if (addLabels)
                Console.WriteLine("Show labels.");
            else
                Console.WriteLine("Do not show labels.");
            if (showOpcodes)
                Console.WriteLine("Show opcodes.");
            else
                Console.WriteLine("Do not show opcodes.");
            if (showAddresses)
                Console.WriteLine("Show addresses.");
            else
                Console.WriteLine("Do not show addresses.");
            if (alignArgs)
                if (useTabs)
                    Console.WriteLine("Align args using tabs.");
                else
                    Console.WriteLine("Align args using spaces.");
            else
                Console.WriteLine("Do not align args.");
            */
            #endregion

            byte[] data = new byte[] {0};
            string inputText = "";

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
                Regex baseAddressPrefixRegex = new Regex("^\\s*[0-9A-Fa-f]{1,6}\\s*\\:");
                Regex addressRegex = new Regex("[0-9A-Fa-f]{1,6}");
                if (baseAddressPrefixRegex.IsMatch(inputText))
                {
                    if (hasBaseAddress)
                        return ShowShortHelp(ErrorCode.ConflictingArgument, "Input string has base address specifier, which conflicts with -b argument.");
                    hasBaseAddress = true;
                    baseAddress = Convert.ToInt32(addressRegex.Match(inputText).Value, 16);
                    inputText = baseAddressPrefixRegex.Replace(inputText, "", 1);
                }
                List<byte> bytes = new List<byte>();
                Regex byteRegex = new Regex("[0-9A-Fa-f][0-9A-Fa-f]");
                foreach (Match m in byteRegex.Matches(inputText))
                    bytes.Add(Convert.ToByte(m.Value, 16));
                data = bytes.ToArray();
            }

            /*Console.Write(baseAddress.ToString("X"));
            foreach (byte b in data)
            {
                Console.Write(":");
                Console.Write(b.ToString("X2"));
            }*/

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
                    writer.FileWriter = new StreamWriter(outputFileName);
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
                        writer.Write(" \\ ");
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
                        if (instr.Length > 3)
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

        static int ShowShortHelp(ErrorCode e, string msg)
        {
            //Console.WriteLine(msg);
            Console.Error.WriteLine(msg);
            Console.WriteLine("Usage: eZDisasm [-acdelstxOp] [-b <base>] [-o <outfile>] {-i/I <infile> | <hex>}");
            Console.WriteLine("For help: eZDisasm --help");
#if DEBUG
            Console.ReadKey();
#else
#if WIN_32
            if (pause)
                Console.ReadKey();
#else
#endif
#endif
            return (int)e;
        }

        static void ShowHelp()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream("eZDisasm.readme.txt"))
                using (StreamReader reader = new StreamReader(stream))
                    Console.WriteLine(reader.ReadToEnd());
#if DEBUG
            Console.ReadKey();
#else
#if WIN_32
            if (pause)
                Console.ReadKey();
#else
#endif
#endif
        }
    }
}
