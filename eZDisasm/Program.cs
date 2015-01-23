using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace eZDisasm
{
    class Program
    {
        enum ArgumentType
        {
            BaseAddress,
            InputFileName,
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

        static int Main(string[] args)
        {
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

            #region Parse Arguments
            Queue<ArgumentType> expectedArgs = new Queue<ArgumentType>();
            
            while ((readInputFile && curArg < args.Length) || (!readInputFile && curArg < args.Length - 1))
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
                        }
                    }
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
                                        case 'e':
                                            z80ClassicMode = false;
                                            break;
                                        case 'E':
                                            /*if (adlMode)
                                                return ShowShortHelp(ErrorCode.ConflictingArgument, "Error: -a is mutually exclusive with -E");*/
                                            adlMode = false;
                                            z80ClassicMode = true;
                                            break;
                                        case 'a':
                                            if (z80ClassicMode)
                                                return ShowShortHelp(ErrorCode.ConflictingArgument, "Error: -a is mutually exclusive with -E");
                                            adlMode = true;
                                            break;
                                        case 'A':
                                            adlMode = false;
                                            break;
                                        case 'l':
                                            addLabels = true;
                                            break;
                                        case 'L':
                                            addLabels = false;
                                            break;
                                        case 'x':
                                            showOpcodes = true;
                                            break;
                                        case 'X':
                                            showOpcodes = false;
                                            break;
                                        case 't':
                                            alignArgs = true;
                                            break;
                                        case 'T':
                                            alignArgs = false;
                                            break;
                                        case 's':
                                            useTabs = false;
                                            break;
                                        case 'S':
                                            useTabs = true;
                                            break;
                                        case 'd':
                                            showAddresses = false;
                                            break;
                                        case 'D':
                                            showAddresses = true;
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
                    Console.WriteLine("Error: Missing implied argument " + expectedArgs.Dequeue().ToString());
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

            foreach (eZ80Disassembler.DisassembledInstruction instr in instrs)
            {
                if (addLabels && knownLabels.Contains(instr.StartPosition + baseAddress))
                {
                    if (showAddresses)
                        if (useTabs)
                            Console.Write("\t");
                        else
                            Console.Write("        ");
                    if (showOpcodes)
                        if (useTabs)
                            Console.Write("\t\t");
                        else
                            Console.Write("            ");
                    Console.Write("label_");
                    if (z80ClassicMode)
                        Console.Write(((instr.StartPosition + baseAddress) & 0xFFFF).ToString("X4"));
                    else
                        Console.Write((instr.StartPosition + baseAddress).ToString("X6"));
                    Console.WriteLine(":");
                }
                if (showAddresses)
                {
                    if (z80ClassicMode)
                        Console.Write((instr.StartPosition + baseAddress).ToString("X4"));
                    else
                        Console.Write((instr.StartPosition + baseAddress).ToString("X6"));
                    Console.Write(":");
                    if (useTabs)
                        Console.Write("\t");
                    else
                        if (z80ClassicMode)
                            Console.Write("   ");
                        else
                            Console.Write(" ");
                }
                if (showOpcodes)
                {
                    for (int i = 0; i < instr.Length; i++)
                        Console.Write(data[instr.StartPosition + i].ToString("X2"));
                    if (useTabs)
                    {
                        if (instr.Length > 3)
                            Console.Write("\t");
                        Console.Write("\t");
                    }
                    else
                        Console.Write(new String(' ', 14 - 2 * instr.Length));
                }
                else
                    if (useTabs)
                        Console.Write("\t");
                    else
                        Console.Write("    ");
                Console.Write(instr.InstructionName);
                Console.Write(instr.InstructionSuffix);
                if (alignArgs)
                    if (useTabs)
                        Console.Write("\t");
                    else
                        Console.Write(new String(' ', instr.InstructionName.Length + instr.InstructionSuffix.Length < 10 ? 10 - instr.InstructionName.Length - instr.InstructionSuffix.Length : 3));
                else
                    Console.Write(" ");
                Console.WriteLine(instr.InstructionArguments);
            }


#if DEBUG
            Console.ReadKey();
#endif
            return (int)ErrorCode.NoError;
        }


        static int ShowShortHelp(ErrorCode e, string msg)
        {
            Console.WriteLine(msg);
            Console.WriteLine("Usage: eZDisasm [options] [hex string]");
            Console.WriteLine("For help: eZDisasm --help");
#if DEBUG
            Console.ReadKey();
#endif
            return (int)e;
        }

        static void ShowHelp()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream("eZDisasm.readme.txt"))
                using (StreamReader reader = new StreamReader(stream))
                    Console.WriteLine(reader.ReadToEnd());
        }
    }
}
