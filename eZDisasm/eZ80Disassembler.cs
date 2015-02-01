using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eZDisasm
{
    public class eZ80Disassembler
    {
        // This is based on http://www.z80.info/decoding.htm
        public struct DisassembledInstruction
        {
            public string InstructionName;
            public string InstructionArguments;
            public string InstructionSuffix;
            public int StartPosition;
            public byte Length;
            public bool IsBranch;
            public int BranchTarget;
            
            public string ToString(bool indentArguments = false)
            {
                if (!indentArguments)
                    return InstructionName + InstructionSuffix
                    + (String.IsNullOrEmpty(InstructionArguments)
                        ? ""
                        : " " + InstructionArguments);
                else
                    return InstructionName + InstructionSuffix
                    + (String.IsNullOrEmpty(InstructionArguments)
                        ? ""
                        : new String(' ', 10 - InstructionName.Length - InstructionSuffix.Length) + InstructionArguments);
            }
        }
        
        public static DisassembledInstruction[] Disassemble(byte[] data, int start, int end, int baseAddress = 0, bool hasBaseAddress = false, bool adlMode = true, bool plainZ80Mode = false, string labelPrefixString = "", string locPrefixString = "")
        {
            List<DisassembledInstruction> disasm = new List<DisassembledInstruction>();
            eZ80Disassembler dis = new eZ80Disassembler();
            dis.Data = data;
            dis.BaseAddress = baseAddress;
            if (baseAddress != 0)
                dis.HasBaseAddress = true;
            else
                dis.HasBaseAddress = hasBaseAddress;
            dis.AdlMode = adlMode;
            dis.Z80PlainMode = plainZ80Mode;
            dis.CurrentByte = start;
            dis.LabelPrefixString = labelPrefixString;
            dis.LocationPrefixString = locPrefixString;
            while (dis.CurrentByte <= end)
                disasm.Add(dis.DoDisassembleInstruction());

            return disasm.ToArray();
        }


        protected eZ80Disassembler()
        {

        }


        protected byte[] Data;
        protected int BaseAddress;
        protected bool HasBaseAddress;
        protected bool AdlMode;
        protected bool Z80PlainMode;
        protected string LabelPrefixString;
        protected string LocationPrefixString;
        protected int CurrentByte;
        protected DisassembledInstruction CurrentInstruction;
        protected AddressingModePrefix CurrentPrefix;
        protected bool LongData;
        protected string WordDataFormatString;
        protected static readonly string Format1Byte = "X2";
        protected static readonly string Format2Byte = "X4";
        protected static readonly string Format3Byte = "X6";

        protected enum AddressingModePrefix
        {
            None,
            Sis,
            Sil,
            Lis,
            Lil,
        }


        protected DisassembledInstruction DoDisassembleInstruction()
        {
            CurrentPrefix = AddressingModePrefix.None;
            LongData = AdlMode;
            if (LongData)
                WordDataFormatString = Format3Byte;
            else
                WordDataFormatString = Format2Byte;
            CurrentInstruction = default(DisassembledInstruction);
            CurrentInstruction.InstructionName = "ERROR";
            CurrentInstruction.InstructionArguments = "";
            CurrentInstruction.InstructionSuffix = "";
            CurrentInstruction.Length = 0;
            //CurrentInstruction.Disassembly = "ERROR";
            CurrentInstruction.StartPosition = CurrentByte;
            try
            {
                DisassembleInstruction();
                CurrentInstruction.Length = (byte)(CurrentByte - CurrentInstruction.StartPosition);
            }
            catch (IndexOutOfRangeException)
            {
                CurrentInstruction.InstructionName = "<Incomplete instruction>";
                CurrentInstruction.InstructionArguments = "";
                CurrentInstruction.InstructionSuffix = "";
                CurrentInstruction.Length = (byte)(Data.Length - CurrentInstruction.StartPosition);
            }
            /*CurrentInstruction.Disassembly = CurrentInstruction.InstructionName + CurrentInstruction.InstructionSuffix
                + (String.IsNullOrEmpty(CurrentInstruction.InstructionArguments)
                    ? ""
                    : " " + CurrentInstruction.InstructionArguments);*/
            return CurrentInstruction;
        }

        protected int ReadImmWord()
        {
            int r = Data[CurrentByte++] | (Data[CurrentByte++] << 8);
            if (LongData)
                r |= (Data[CurrentByte++] << 16);
            return r;
        }

        static string SignedByte(int x)
        {
            if (x >= 0)
                return x.ToString("X2");
            else
                return "-" + (-x).ToString("X2");
        }

        protected void DisassembleInstruction()
        {
            unchecked
            {
                int branchTarget;
                int b = Data[CurrentByte++];
                // Do you like switches? 'Cause that's kind of kinky.
                switch (GetField(Field.x, b))
                {
                    case 0:
                        switch (GetField(Field.z, b))
                        {
                            case 0:
                                switch (GetField(Field.y, b))
                                {
                                    case 0:
                                        CurrentInstruction.InstructionName = "nop";
                                        break;
                                    case 1:
                                        CurrentInstruction.InstructionName = "ex";
                                        CurrentInstruction.InstructionArguments = "af, af'";
                                        break;
                                    case 2:
                                        branchTarget = (sbyte)Data[CurrentByte++];
                                        if (HasBaseAddress)
                                            branchTarget += CurrentByte + BaseAddress;
                                        if (Z80PlainMode)
                                            branchTarget &= 0xFFFF;
                                        CurrentInstruction.IsBranch = true;
                                        CurrentInstruction.BranchTarget = branchTarget;
                                        CurrentInstruction.InstructionName = "djnz";
                                        if (HasBaseAddress)
                                            CurrentInstruction.InstructionArguments = LabelPrefixString + branchTarget.ToString(Z80PlainMode ? Format2Byte : Format3Byte);
                                        else
                                            CurrentInstruction.InstructionArguments = SignedByte(branchTarget);
                                        break;
                                    case 3:
                                        branchTarget = (sbyte)Data[CurrentByte++];
                                        if (HasBaseAddress)
                                            branchTarget += CurrentByte + BaseAddress;
                                        if (Z80PlainMode)
                                            branchTarget &= 0xFFFF;
                                        CurrentInstruction.IsBranch = true;
                                        CurrentInstruction.BranchTarget = branchTarget;
                                        CurrentInstruction.InstructionName = "jr";
                                        if (HasBaseAddress)
                                            CurrentInstruction.InstructionArguments = LabelPrefixString + branchTarget.ToString(Z80PlainMode ? Format2Byte : Format3Byte);
                                        else
                                            CurrentInstruction.InstructionArguments = SignedByte(branchTarget);
                                        break;
                                    case 4:
                                    case 5:
                                    case 6:
                                    case 7:
                                        branchTarget = (sbyte)Data[CurrentByte++];
                                        if (HasBaseAddress)
                                            branchTarget += CurrentByte + BaseAddress;
                                        if (Z80PlainMode)
                                            branchTarget &= 0xFFFF;
                                        CurrentInstruction.IsBranch = true;
                                        CurrentInstruction.BranchTarget = branchTarget;
                                        CurrentInstruction.InstructionName = "jr";
                                        if (HasBaseAddress)
                                            CurrentInstruction.InstructionArguments = TableCC[GetField(Field.qq, b)] + ", " + LabelPrefixString + branchTarget.ToString(Z80PlainMode ? Format2Byte : Format3Byte);
                                        else
                                            CurrentInstruction.InstructionArguments = TableCC[GetField(Field.qq, b)] + ", " + SignedByte(branchTarget);
                                        break;
                                }
                                break;
                            case 1:
                                switch (GetField(Field.q, b))
                                {
                                    case 0:
                                        CurrentInstruction.InstructionName = "ld";
                                        CurrentInstruction.InstructionArguments = TableRP[GetField(Field.p, b)] + ", " + ReadImmWord().ToString(WordDataFormatString);
                                        break;
                                    case 1:
                                        CurrentInstruction.InstructionName = "add";
                                        CurrentInstruction.InstructionArguments = "hl, " + TableRP[GetField(Field.p, b)];
                                        break;
                                }
                                break;
                            case 2:
                                switch (GetField(Field.y, b))
                                {
                                    case 0:
                                        CurrentInstruction.InstructionName = "ld";
                                        CurrentInstruction.InstructionArguments = "(bc), a";
                                        break;
                                    case 1:
                                        CurrentInstruction.InstructionName = "ld";
                                        CurrentInstruction.InstructionArguments = "a, (bc)";
                                        break;
                                    case 2:
                                        CurrentInstruction.InstructionName = "ld";
                                        CurrentInstruction.InstructionArguments = "(de), a";
                                        break;
                                    case 3:
                                        CurrentInstruction.InstructionName = "ld";
                                        CurrentInstruction.InstructionArguments = "a, (de)";
                                        break;
                                    case 4:
                                        CurrentInstruction.InstructionName = "ld";
                                        CurrentInstruction.InstructionArguments = "(" + LocationPrefixString + ReadImmWord().ToString(WordDataFormatString) + "), hl";
                                        break;
                                    case 5:
                                        CurrentInstruction.InstructionName = "ld";
                                        CurrentInstruction.InstructionArguments = "hl, (" + LocationPrefixString + ReadImmWord().ToString(WordDataFormatString) + ")";
                                        break;
                                    case 6:
                                        CurrentInstruction.InstructionName = "ld";
                                        CurrentInstruction.InstructionArguments = "(" + LocationPrefixString + ReadImmWord().ToString(WordDataFormatString) + "), a";
                                        break;
                                    case 7:
                                        CurrentInstruction.InstructionName = "ld";
                                        CurrentInstruction.InstructionArguments = "a, (" + LocationPrefixString + ReadImmWord().ToString(WordDataFormatString) + ")";
                                        break;
                                }
                                break;
                            case 3:
                                switch (GetField(Field.q, b))
                                {
                                    case 0:
                                        CurrentInstruction.InstructionName = "inc";
                                        CurrentInstruction.InstructionArguments = TableRP[GetField(Field.p, b)];
                                        break;
                                    case 1:
                                        CurrentInstruction.InstructionName = "dec";
                                        CurrentInstruction.InstructionArguments = TableRP[GetField(Field.p, b)];
                                        break;
                                }
                                break;
                            case 4:
                                CurrentInstruction.InstructionName = "inc";
                                CurrentInstruction.InstructionArguments = TableR[GetField(Field.y, b)];
                                break;
                            case 5:
                                CurrentInstruction.InstructionName = "dec";
                                CurrentInstruction.InstructionArguments = TableR[GetField(Field.y, b)];
                                break;
                            case 6:
                                CurrentInstruction.InstructionName = "ld";
                                CurrentInstruction.InstructionArguments = TableR[GetField(Field.y, b)] + ", " + Data[CurrentByte++].ToString("X2");
                                break;
                            case 7:
                                switch (GetField(Field.y, b))
                                {
                                    case 0:
                                        CurrentInstruction.InstructionName = "rlca";
                                        break;
                                    case 1:
                                        CurrentInstruction.InstructionName = "rrca";
                                        break;
                                    case 2:
                                        CurrentInstruction.InstructionName = "rla";
                                        break;
                                    case 3:
                                        CurrentInstruction.InstructionName = "rra";
                                        break;
                                    case 4:
                                        CurrentInstruction.InstructionName = "daa";
                                        break;
                                    case 5:
                                        CurrentInstruction.InstructionName = "cpl";
                                        break;
                                    case 6:
                                        CurrentInstruction.InstructionName = "scf";
                                        break;
                                    case 7:
                                        CurrentInstruction.InstructionName = "ccf";
                                        break;
                                }
                                break;
                        }
                        break;
                    case 1:
                        if (!Z80PlainMode)
                        {
                            if (CurrentPrefix != AddressingModePrefix.None)
                            {
                                CurrentInstruction.InstructionName = "NONI";
                                // Last prefix is being ignored, but this one might matter.
                                // So unwind CurrentByte so we can process it again.
                                CurrentByte--;
                                return;
                            }
                            if (b == 0x40)
                            {
                                CurrentPrefix = AddressingModePrefix.Sis;
                                CurrentInstruction.InstructionSuffix = ".sis";
                                LongData = false;
                                WordDataFormatString = Format2Byte;
                                DisassembleInstruction();
                                return;
                            }
                            else if (b == 0x49)
                            {
                                CurrentPrefix = AddressingModePrefix.Sil;
                                CurrentInstruction.InstructionSuffix = ".lis";
                                LongData = true;
                                WordDataFormatString = Format2Byte;
                                DisassembleInstruction();
                                return;
                            }
                            else if (b == 0x52)
                            {
                                CurrentPrefix = AddressingModePrefix.Lis;
                                CurrentInstruction.InstructionSuffix = ".sil";
                                LongData = false;
                                WordDataFormatString = Format3Byte;
                                DisassembleInstruction();
                                return;
                            }
                            else if (b == 0x5B)
                            {
                                CurrentPrefix = AddressingModePrefix.Lil;
                                CurrentInstruction.InstructionSuffix = ".lil";
                                LongData = true;
                                WordDataFormatString = Format3Byte;
                                DisassembleInstruction();
                                return;
                            }
                        }
                        if (b == 0x76)
                            CurrentInstruction.InstructionName = "halt";
                        else
                        {
                            CurrentInstruction.InstructionName = "ld";
                            CurrentInstruction.InstructionArguments = TableR[GetField(Field.y, b)] + ", " + TableR[GetField(Field.z, b)];
                        }
                        break;
                    case 2:
                        CurrentInstruction.InstructionName = TableAlu[GetField(Field.y, b)];
                        CurrentInstruction.InstructionArguments = TableAluArg[GetField(Field.y, b)] + TableR[GetField(Field.z, b)];
                        break;
                    case 3:
                        switch (GetField(Field.z, b))
                        {
                            case 0:
                                CurrentInstruction.InstructionName = "ret";
                                CurrentInstruction.InstructionArguments = TableCC[GetField(Field.y, b)];
                                break;
                            case 1:
                                switch (GetField(Field.q, b))
                                {
                                    case 0:
                                        CurrentInstruction.InstructionName = "pop";
                                        CurrentInstruction.InstructionArguments = TableRP2[GetField(Field.p, b)];
                                        break;
                                    case 1:
                                        switch (GetField(Field.p, b))
                                        {
                                            case 0:
                                                CurrentInstruction.InstructionName = "ret";
                                                break;
                                            case 1:
                                                CurrentInstruction.InstructionName = "exx";
                                                break;
                                            case 2:
                                                CurrentInstruction.InstructionName = "jp";
                                                CurrentInstruction.InstructionArguments = "(hl)";
                                                break;
                                            case 3:
                                                CurrentInstruction.InstructionName = "ld";
                                                CurrentInstruction.InstructionArguments = "sp, hl";
                                                break;
                                        }
                                        break;
                                }
                                break;
                            case 2:
                                CurrentInstruction.InstructionName = "jp";
                                branchTarget = ReadImmWord();
                                CurrentInstruction.InstructionArguments = TableCC[GetField(Field.y, b)] + ", "
                                    + (branchTarget - BaseAddress >= 0 && branchTarget - BaseAddress < Data.Length ? LabelPrefixString : LabelPrefixString)
                                    + branchTarget.ToString(WordDataFormatString);
                                CurrentInstruction.IsBranch = true;
                                CurrentInstruction.BranchTarget = branchTarget;
                                break;
                            case 3:
                                switch (GetField(Field.y, b))
                                {
                                    case 0:
                                        CurrentInstruction.InstructionName = "jp";
                                        branchTarget = ReadImmWord();
                                        CurrentInstruction.InstructionArguments = (branchTarget - BaseAddress >= 0 && branchTarget - BaseAddress < Data.Length ? LabelPrefixString : LabelPrefixString) + branchTarget.ToString(WordDataFormatString);
                                        CurrentInstruction.IsBranch = true;
                                        CurrentInstruction.BranchTarget = branchTarget;
                                        break;
                                    case 1: // CB Prefix
                                        DoCBPrefix();
                                        break;
                                    case 2:
                                        CurrentInstruction.InstructionName = "out";
                                        CurrentInstruction.InstructionArguments = "(" + Data[CurrentByte++].ToString("X2") + "), a";
                                        break;
                                    case 3:
                                        CurrentInstruction.InstructionName = "in";
                                        CurrentInstruction.InstructionArguments = "a, (" + Data[CurrentByte++].ToString("X2") + ")";
                                        break;
                                    case 4:
                                        CurrentInstruction.InstructionName = "ex";
                                        CurrentInstruction.InstructionArguments = "(sp), hl";
                                        break;
                                    case 5:
                                        CurrentInstruction.InstructionName = "ex";
                                        CurrentInstruction.InstructionArguments = "de, hl";
                                        break;
                                    case 6:
                                        CurrentInstruction.InstructionName = "di";
                                        break;
                                    case 7:
                                        CurrentInstruction.InstructionName = "ei";
                                        break;
                                }
                                break;
                            case 4:
                                branchTarget = ReadImmWord();
                                CurrentInstruction.IsBranch = true;
                                CurrentInstruction.BranchTarget = branchTarget;
                                CurrentInstruction.InstructionName = "call";
                                CurrentInstruction.InstructionArguments = TableCC[GetField(Field.y, b)] + ", "
                                    + (branchTarget - BaseAddress >= 0 && branchTarget - BaseAddress < Data.Length ? LabelPrefixString : LabelPrefixString)
                                    + branchTarget.ToString(WordDataFormatString);
                                break;
                            case 5:
                                switch (GetField(Field.q, b))
                                {
                                    case 0:
                                        CurrentInstruction.InstructionName = "push";
                                        CurrentInstruction.InstructionArguments = TableRP2[GetField(Field.p, b)];
                                        break;
                                    case 1:
                                        switch (b)
                                        {
                                            case 0xCD:
                                                branchTarget = ReadImmWord();
                                                CurrentInstruction.IsBranch = true;
                                                CurrentInstruction.BranchTarget = branchTarget;
                                                CurrentInstruction.InstructionName = "call";
                                                CurrentInstruction.InstructionArguments = (branchTarget - BaseAddress >= 0 && branchTarget - BaseAddress < Data.Length ? LabelPrefixString : LabelPrefixString)
                                                    + branchTarget.ToString(WordDataFormatString);
                                                break;
                                            case 0xDD:
                                                DoIndexPrefix("ix", 0);
                                                break;
                                            case 0xED:
                                                DoEDPrefix();
                                                break;
                                            case 0xFD:
                                                DoIndexPrefix("iy", 1);
                                                break;
                                        }
                                        break;
                                }
                                break;
                            case 6:
                                CurrentInstruction.InstructionName = TableAlu[GetField(Field.y, b)];
                                CurrentInstruction.InstructionArguments = TableAluArg[GetField(Field.y, b)] + Data[CurrentByte++].ToString("X2");
                                break;
                            case 7:
                                CurrentInstruction.InstructionName = "rst";
                                CurrentInstruction.InstructionArguments = (GetField(Field.y, b) * 8).ToString("X2") + "h";
                                break;
                        }
                        break;
                }
                return;
            }
        }

        private void DoCBPrefix()
        {
            byte b = Data[CurrentByte++];
            switch (GetField(Field.x, b))
            {
                case 0:
                    CurrentInstruction.InstructionName = TableRot[GetField(Field.y, b)];
                    CurrentInstruction.InstructionArguments = TableR[GetField(Field.z, b)];
                    break;
                case 1:
                    CurrentInstruction.InstructionName = "bit";
                    CurrentInstruction.InstructionArguments = GetField(Field.y, b).ToString() + ", " + TableR[GetField(Field.z, b)];
                    break;
                case 2:
                    CurrentInstruction.InstructionName = "res";
                    CurrentInstruction.InstructionArguments = GetField(Field.y, b).ToString() + ", " + TableR[GetField(Field.z, b)];
                    break;
                case 3:
                    CurrentInstruction.InstructionName = "set";
                    CurrentInstruction.InstructionArguments = GetField(Field.y, b).ToString() + ", " + TableR[GetField(Field.z, b)];
                    break;
            }
        }


        private void DoEDPrefix()
        {
            string indexreg;
            string tempstr = "";
            byte b = Data[CurrentByte++];
            switch (GetField(Field.x, b))
            {
                case 0:
                    if (Z80PlainMode)
                    {
                        CurrentInstruction.InstructionName = "NONI \\ NOP";
                        return;
                    }
                    switch (GetField(Field.z, b))
                    {
                        case 0:
                            if (b == 0x30)
                            {
                                CurrentInstruction.InstructionName = "OPCODETRAP";
                                return;
                            }
                            CurrentInstruction.InstructionName = "in0";
                            CurrentInstruction.InstructionArguments = TableR[GetField(Field.y, b)] + ", (" + Data[CurrentByte++].ToString("X2") + ")";
                            break;
                        case 1:
                            if (b == 0x31)
                            {
                                CurrentInstruction.InstructionName = "ld";
                                CurrentInstruction.InstructionArguments = "iy, (hl)";
                            }
                            else
                            {
                                CurrentInstruction.InstructionName = "out0";
                                CurrentInstruction.InstructionArguments = "(" + Data[CurrentByte++].ToString("X2") + "), " + TableR[GetField(Field.y, b)];
                            }
                            break;
                        case 2:
                        case 3:
                            if ((b & 1) != 0)
                                indexreg = "iy";
                            else
                                indexreg = "ix";
                            CurrentInstruction.InstructionName = "lea";
                            if (GetField(Field.q, b) == 0)
                            {
                                switch (GetField(Field.p, b))
                                {
                                    case 0:
                                        tempstr = "bc";
                                        break;
                                    case 1:
                                        tempstr = "de";
                                        break;
                                    case 2:
                                        tempstr = "hl";
                                        break;
                                    case 3:
                                        tempstr = indexreg;
                                        break;
                                }
                                CurrentInstruction.InstructionArguments = tempstr + ", " + indexreg + " + " + ((sbyte)Data[CurrentByte++]).ToString("X2");
                            }
                            else
                                CurrentInstruction.InstructionName = "OPCODETRAP";
                            break;
                        case 4:
                            CurrentInstruction.InstructionName = "tst";
                            CurrentInstruction.InstructionArguments = "a, " + TableR[GetField(Field.y, b)];
                            break;
                        case 5:
                            CurrentInstruction.InstructionName = "OPCODETRAP";
                            break;
                        case 6:
                            if (b == 0x3E)
                            {
                                CurrentInstruction.InstructionName = "ld";
                                CurrentInstruction.InstructionArguments = "(hl), iy";
                            }
                            else
                                CurrentInstruction.InstructionName = "OPCODETRAP";
                            break;
                        case 7:
                            CurrentInstruction.InstructionName = "ld";
                            switch (GetField(Field.p, b))
                            {
                                case 0:
                                    tempstr = "bc";
                                    break;
                                case 1:
                                    tempstr = "de";
                                    break;
                                case 2:
                                    tempstr = "hl";
                                    break;
                                case 3:
                                    tempstr = "ix";
                                    break;
                            }
                            switch (GetField(Field.q, b))
                            {
                                case 0:
                                    CurrentInstruction.InstructionArguments = tempstr + ", (hl)";
                                    break;
                                case 1:
                                    CurrentInstruction.InstructionArguments = "(hl), " + tempstr;
                                    break;
                            }
                            break;
                    }
                    break;
                case 1:
                    switch (GetField(Field.z, b))
                    {
                        case 0:
                            CurrentInstruction.InstructionName = "in";
                            if (b == 0x70)
                                if (Z80PlainMode)
                                    CurrentInstruction.InstructionArguments = "(c)";
                                else
                                    CurrentInstruction.InstructionName = "OPCODETRAP";
                            else
                                CurrentInstruction.InstructionArguments = TableR[GetField(Field.y, b)] + (Z80PlainMode ? ", (c)" : ", (bc)");
                            break;
                        case 1:
                            CurrentInstruction.InstructionName = "out";
                            if (b == 0x71)
                                if (Z80PlainMode)
                                    CurrentInstruction.InstructionArguments = "(c), 0";
                                else
                                    CurrentInstruction.InstructionName = "OPCODETRAP";
                            else
                                CurrentInstruction.InstructionArguments = (Z80PlainMode ? "(c), " : "(bc), ") + TableR[GetField(Field.y, b)];
                            break;
                        case 2:
                            if (GetField(Field.q, b) == 0)
                                CurrentInstruction.InstructionName = "sbc";
                            else
                                CurrentInstruction.InstructionName = "adc";
                            CurrentInstruction.InstructionArguments = "hl, " + TableRP[GetField(Field.p, b)];
                            break;
                        case 3:
                            CurrentInstruction.InstructionName = "ld";
                            if (GetField(Field.q, b) == 0)
                                CurrentInstruction.InstructionArguments = "(" + LocationPrefixString + ReadImmWord().ToString(WordDataFormatString) + "), " + TableRP[GetField(Field.p, b)];
                            else
                                CurrentInstruction.InstructionArguments = TableRP[GetField(Field.p, b)] + ", (" + LocationPrefixString + ReadImmWord().ToString(WordDataFormatString) + ")";
                            break;
                        case 4:
                            if (Z80PlainMode)
                            {
                                CurrentInstruction.InstructionName = "neg";
                            }
                            else
                            {
                                if (GetField(Field.q, b) == 0)
                                    switch (GetField(Field.p, b))
                                    {
                                        case 0:
                                            CurrentInstruction.InstructionName = "neg";
                                            break;
                                        case 1:
                                            CurrentInstruction.InstructionName = "lea";
                                            CurrentInstruction.InstructionArguments = "ix, iy + " + SignedByte((sbyte)Data[CurrentByte++]);
                                            break;
                                        case 2:
                                            CurrentInstruction.InstructionName = "tst";
                                            CurrentInstruction.InstructionArguments = "a, " + Data[CurrentByte++].ToString("X2");
                                            break;
                                        case 3:
                                            CurrentInstruction.InstructionName = "tstio";
                                            CurrentInstruction.InstructionArguments = Data[CurrentByte++].ToString("X2");
                                            break;
                                    }
                                else
                                {
                                    CurrentInstruction.InstructionName = "mlt";
                                    CurrentInstruction.InstructionArguments = TableRP[GetField(Field.p, b)];
                                }
                            }
                            break;
                        case 5:
                            if (Z80PlainMode)
                            {
                                if (GetField(Field.y, b) != 1)
                                    CurrentInstruction.InstructionName = "retn";
                                else
                                    CurrentInstruction.InstructionName = "reti";
                            }
                            else
                            {
                                switch (GetField(Field.y, b))
                                {
                                    case 0:
                                        CurrentInstruction.InstructionName = "retn";
                                        break;
                                    case 1:
                                        CurrentInstruction.InstructionName = "reti";
                                        break;
                                    case 2:
                                        CurrentInstruction.InstructionName = "lea";
                                        CurrentInstruction.InstructionArguments = "iy, ix + " + SignedByte((sbyte)Data[CurrentByte++]);
                                        break;
                                    case 3:
                                    case 6:
                                        CurrentInstruction.InstructionName = "OPCODETRAP";
                                        break;
                                    case 4:
                                        CurrentInstruction.InstructionName = "pea";
                                        CurrentInstruction.InstructionArguments = "ix + " + SignedByte((sbyte)Data[CurrentByte++]);
                                        break;
                                    case 5:
                                        CurrentInstruction.InstructionName = "ld";
                                        CurrentInstruction.InstructionArguments = "mb, a";
                                        break;
                                    case 7:
                                        CurrentInstruction.InstructionName = "stmix";
                                        break;
                                }
                            }
                            break;
                        case 6:
                            if (Z80PlainMode)
                            {
                                CurrentInstruction.InstructionName = "im";
                                CurrentInstruction.InstructionArguments = TableIM[GetField(Field.y, b)];
                            }
                            else
                            {
                                switch (GetField(Field.y, b))
                                {
                                    case 0:
                                    case 2:
                                    case 3:
                                        CurrentInstruction.InstructionName = "im";
                                        CurrentInstruction.InstructionArguments = TableIM[GetField(Field.y, b)];
                                        break;
                                    case 1:
                                        CurrentInstruction.InstructionName = "OPCODETRAP";
                                        break;
                                    case 4:
                                        CurrentInstruction.InstructionName = "pea";
                                        CurrentInstruction.InstructionArguments = "iy + " + SignedByte((sbyte)Data[CurrentByte++]);
                                        break;
                                    case 5:
                                        CurrentInstruction.InstructionName = "ld";
                                        CurrentInstruction.InstructionArguments = "a, mb";
                                        break;
                                    case 6:
                                        CurrentInstruction.InstructionName = "slp";
                                        break;
                                    case 7:
                                        CurrentInstruction.InstructionName = "rsmix";
                                        break;
                                }
                            }
                            break;
                        case 7:
                            switch (GetField(Field.y, b))
                            {
                                case 0:
                                    CurrentInstruction.InstructionName = "ld";
                                    CurrentInstruction.InstructionArguments = "i, a";
                                    break;
                                case 1:
                                    CurrentInstruction.InstructionName = "ld";
                                    CurrentInstruction.InstructionArguments = "r, a";
                                    break;
                                case 2:
                                    CurrentInstruction.InstructionName = "ld";
                                    CurrentInstruction.InstructionArguments = " a, i";
                                    break;
                                case 3:
                                    CurrentInstruction.InstructionName = "ld";
                                    CurrentInstruction.InstructionArguments = " a, r";
                                    break;
                                case 4:
                                    CurrentInstruction.InstructionName = "rrd";
                                    break;
                                case 5:
                                    CurrentInstruction.InstructionName = "rld";
                                    break;
                                case 6:
                                case 7:
                                    if (Z80PlainMode)
                                        CurrentInstruction.InstructionName = "NONI \\ NOP";
                                    else
                                        CurrentInstruction.InstructionName = "OPCODETRAP";
                                    break;
                            }
                            break;
                    }
                    break;
                case 2:
                case 3:
                    switch (b)
                    {
                        case 0xA0:
                            CurrentInstruction.InstructionName = "ldi";
                            break;
                        case 0xA1:
                            CurrentInstruction.InstructionName = "cpi";
                            break;
                        case 0xA2:
                            CurrentInstruction.InstructionName = "ini";
                            break;
                        case 0xA3:
                            CurrentInstruction.InstructionName = "outi";
                            break;
                        case 0xA4:
                            if (Z80PlainMode)
                                goto default;
                            CurrentInstruction.InstructionName = "outi2";
                            break;
                        case 0xA8:
                            CurrentInstruction.InstructionName = "ldd";
                            break;
                        case 0xA9:
                            CurrentInstruction.InstructionName = "cpd";
                            break;
                        case 0xAA:
                            CurrentInstruction.InstructionName = "ind";
                            break;
                        case 0xAB:
                            CurrentInstruction.InstructionName = "outd";
                            break;
                        case 0xAC:
                            if (Z80PlainMode)
                                goto default;
                            CurrentInstruction.InstructionName = "outd2";
                            break;
                        case 0xB0:
                            CurrentInstruction.InstructionName = "ldir";
                            break;
                        case 0xB1:
                            CurrentInstruction.InstructionName = "cpir";
                            break;
                        case 0xB2:
                            CurrentInstruction.InstructionName = "inir";
                            break;
                        case 0xB3:
                            CurrentInstruction.InstructionName = "otir";
                            break;
                        case 0xB4:
                            if (Z80PlainMode)
                                goto default;
                            CurrentInstruction.InstructionName = "oti2r";
                            break;
                        case 0xB8:
                            CurrentInstruction.InstructionName = "lddr";
                            break;
                        case 0xB9:
                            CurrentInstruction.InstructionName = "cpdr";
                            break;
                        case 0xBA:
                            CurrentInstruction.InstructionName = "indr";
                            break;
                        case 0xBB:
                            CurrentInstruction.InstructionName = "otdr";
                            break;
                        case 0xBC:
                            if (Z80PlainMode)
                                goto default;
                            CurrentInstruction.InstructionName = "otd2r";
                            break;
                        case 0x82:
                            if (Z80PlainMode)
                                goto default;
                            CurrentInstruction.InstructionName = "inim";
                            break;
                        case 0x83:
                            if (Z80PlainMode)
                                goto default;
                            CurrentInstruction.InstructionName = "otim";
                            break;
                        case 0x84:
                            if (Z80PlainMode)
                                goto default;
                            CurrentInstruction.InstructionName = "ini2";
                            break;
                        case 0x8A:
                            if (Z80PlainMode)
                                goto default;
                            CurrentInstruction.InstructionName = "indm";
                            break;
                        case 0x8B:
                            if (Z80PlainMode)
                                goto default;
                            CurrentInstruction.InstructionName = "otdm";
                            break;
                        case 0x8C:
                            if (Z80PlainMode)
                                goto default;
                            CurrentInstruction.InstructionName = "ind2";
                            break;
                        case 0x92:
                            if (Z80PlainMode)
                                goto default;
                            CurrentInstruction.InstructionName = "inimr";
                            break;
                        case 0x93:
                            if (Z80PlainMode)
                                goto default;
                            CurrentInstruction.InstructionName = "otimr";
                            break;
                        case 0x94:
                            if (Z80PlainMode)
                                goto default;
                            CurrentInstruction.InstructionName = "ini2r";
                            break;
                        case 0x9A:
                            if (Z80PlainMode)
                                goto default;
                            CurrentInstruction.InstructionName = "indmr";
                            break;
                        case 0x9B:
                            if (Z80PlainMode)
                                goto default;
                            CurrentInstruction.InstructionName = "otdmr";
                            break;
                        case 0x9C:
                            if (Z80PlainMode)
                                goto default;
                            CurrentInstruction.InstructionName = "ind2r";
                            break;
                        case 0xC2:
                            if (Z80PlainMode)
                                goto default;
                            CurrentInstruction.InstructionName = "inirx";
                            break;
                        case 0xC3:
                            if (Z80PlainMode)
                                goto default;
                            CurrentInstruction.InstructionName = "otirx";
                            break;
                        case 0xC7:
                            if (Z80PlainMode)
                                goto default;
                            CurrentInstruction.InstructionName = "ld";
                            CurrentInstruction.InstructionArguments = "i, hl";
                            break;
                        case 0xD7:
                            if (Z80PlainMode)
                                goto default;
                            CurrentInstruction.InstructionName = "ld";
                            CurrentInstruction.InstructionArguments = "hl, i";
                            break;
                        case 0xCA:
                            if (Z80PlainMode)
                                goto default;
                            CurrentInstruction.InstructionName = "indrx";
                            break;
                        case 0xCB:
                            if (Z80PlainMode)
                                goto default;
                            CurrentInstruction.InstructionName = "otdrx";
                            break;
                        default:
                            if (Z80PlainMode)
                                CurrentInstruction.InstructionName = "NONI \\ NOP";
                            else
                                CurrentInstruction.InstructionName = "OPCODETRAP";
                            break;
                    }
                    break;
            }
        }
        private void DoIndexPrefix(string indexRegister, int indexRegNumber)
        {
            unchecked
            {
                byte b = Data[CurrentByte++];
                switch (b)
                {
                    case 0x21:
                        CurrentInstruction.InstructionName = "ld";
                        CurrentInstruction.InstructionArguments = indexRegister + ", " + ReadImmWord().ToString(WordDataFormatString);
                        break;
                    case 0x22:
                        CurrentInstruction.InstructionName = "ld";
                        CurrentInstruction.InstructionArguments = "(" + ReadImmWord().ToString(WordDataFormatString) + "), " + indexRegister;
                        break;
                    case 0x2A:
                        CurrentInstruction.InstructionName = "ld";
                        CurrentInstruction.InstructionArguments = indexRegister + ", (" + ReadImmWord().ToString(WordDataFormatString) + ")";
                        break;
                    case 0x23:
                        CurrentInstruction.InstructionName = "inc";
                        CurrentInstruction.InstructionArguments = indexRegister;
                        break;
                    case 0x2B:
                        CurrentInstruction.InstructionName = "dec";
                        CurrentInstruction.InstructionArguments = indexRegister;
                        break;
                    case 0x24:
                        CurrentInstruction.InstructionName = "inc";
                        CurrentInstruction.InstructionArguments = indexRegister + "h";
                        break;
                    case 0x2C:
                        CurrentInstruction.InstructionName = "inc";
                        CurrentInstruction.InstructionArguments = indexRegister + "l";
                        break;
                    case 0x34:
                        CurrentInstruction.InstructionName = "inc";
                        CurrentInstruction.InstructionArguments = "(" + indexRegister + " + " + SignedByte((sbyte)Data[CurrentByte++]) + ")";
                        break;
                    case 0x25:
                        CurrentInstruction.InstructionName = "dec";
                        CurrentInstruction.InstructionArguments = indexRegister + "h";
                        break;
                    case 0x2D:
                        CurrentInstruction.InstructionName = "dec";
                        CurrentInstruction.InstructionArguments = indexRegister + "l";
                        break;
                    case 0x35:
                        CurrentInstruction.InstructionName = "dec";
                        CurrentInstruction.InstructionArguments = "(" + indexRegister + " + " + SignedByte((sbyte)Data[CurrentByte++]) + ")";
                        break;
                    case 0x26:
                        CurrentInstruction.InstructionName = "ld";
                        CurrentInstruction.InstructionArguments = indexRegister + "h, " + SignedByte((sbyte)Data[CurrentByte++]);
                        break;
                    case 0x2E:
                        CurrentInstruction.InstructionName = "ld";
                        CurrentInstruction.InstructionArguments = indexRegister + "l, " + SignedByte((sbyte)Data[CurrentByte++]);
                        break;
                    case 0x36:
                        CurrentInstruction.InstructionName = "ld";
                        CurrentInstruction.InstructionArguments = "(" + indexRegister + " + " + SignedByte((sbyte)Data[CurrentByte++]) + "), " + Data[CurrentByte++].ToString("X2");
                        break;
                    case 0x09:
                        CurrentInstruction.InstructionName = "add";
                        CurrentInstruction.InstructionArguments = indexRegister + ", bc";
                        break;
                    case 0x19:
                        CurrentInstruction.InstructionName = "add";
                        CurrentInstruction.InstructionArguments = indexRegister + ", de";
                        break;
                    case 0x29:
                        CurrentInstruction.InstructionName = "add";
                        CurrentInstruction.InstructionArguments = indexRegister + ", " + indexRegister;
                        break;
                    case 0x39:
                        CurrentInstruction.InstructionName = "add";
                        CurrentInstruction.InstructionArguments = indexRegister + ", sp";
                        break;
                    case 0x60:
                    case 0x61:
                    case 0x62:
                    case 0x63:
                    case 0x67:
                    case 0x68:
                    case 0x69:
                    case 0x6A:
                    case 0x6B:
                    case 0x6F:
                        CurrentInstruction.InstructionName = "ld";
                        CurrentInstruction.InstructionArguments = indexRegister + TableR[GetField(Field.y, b)] + ", " + TableR[GetField(Field.z, b)];
                        break;
                    case 0x64:
                    case 0x65:
                    case 0x6C:
                    case 0x6D:
                        CurrentInstruction.InstructionName = "ld";
                        CurrentInstruction.InstructionArguments = indexRegister + TableR[GetField(Field.y, b)] + ", " + indexRegister + TableR[GetField(Field.z, b)];
                        break;
                    case 0x70:
                    case 0x71:
                    case 0x72:
                    case 0x73:
                    case 0x74:
                    case 0x75:
                    case 0x77:
                        CurrentInstruction.InstructionName = "ld";
                        CurrentInstruction.InstructionArguments = "(" + indexRegister + " + " + SignedByte((sbyte)Data[CurrentByte++]) + "), " + TableR[GetField(Field.z, b)];
                        break;
                    case 0x44:
                    case 0x45:
                    case 0x4C:
                    case 0x4D:
                    case 0x54:
                    case 0x55:
                    case 0x5C:
                    case 0x5D:
                    case 0x7C:
                    case 0x7D:
                        CurrentInstruction.InstructionName = "ld";
                        CurrentInstruction.InstructionArguments = TableR[GetField(Field.y, b)] + ", " + indexRegister + TableR[GetField(Field.z, b)];
                        break;
                    case 0x46:
                    case 0x4E:
                    case 0x56:
                    case 0x5E:
                    case 0x66:
                    case 0x6E:
                    case 0x7E:
                        CurrentInstruction.InstructionName = "ld";
                        CurrentInstruction.InstructionArguments = TableR[GetField(Field.y, b)] + ", (" + indexRegister + " + " + SignedByte((sbyte)Data[CurrentByte++]) + ")";
                        break;
                    case 0x84:
                    case 0x85:
                    case 0x8C:
                    case 0x8D:
                    case 0x94:
                    case 0x95:
                    case 0x9C:
                    case 0x9D:
                    case 0xA4:
                    case 0xA5:
                    case 0xAC:
                    case 0xAD:
                    case 0xB4:
                    case 0xB5:
                    case 0xBC:
                    case 0xBD:
                        CurrentInstruction.InstructionName = TableAlu[GetField(Field.y, b)];
                        CurrentInstruction.InstructionArguments = TableAluArg[GetField(Field.y, b)] + indexRegister + TableR[GetField(Field.z, b)];
                        break;
                    case 0x86:
                    case 0x8E:
                    case 0x96:
                    case 0x9E:
                    case 0xA6:
                    case 0xAE:
                    case 0xB6:
                    case 0xBE:
                        CurrentInstruction.InstructionName = TableAlu[GetField(Field.y, b)];
                        CurrentInstruction.InstructionArguments = TableAluArg[GetField(Field.y, b)] + "(" + indexRegister + " + " + SignedByte((sbyte)Data[CurrentByte++]) + ")";
                        break;
                    case 0xE1:
                        CurrentInstruction.InstructionName = "pop";
                        CurrentInstruction.InstructionArguments = indexRegister;
                        break;
                    case 0xE9:
                        CurrentInstruction.InstructionName = "jp";
                        CurrentInstruction.InstructionArguments = "(" + indexRegister + ")";
                        break;
                    case 0xE3:
                        CurrentInstruction.InstructionName = "ex";
                        CurrentInstruction.InstructionArguments = "(sp), " + indexRegister;
                        break;
                    case 0xE5:
                        CurrentInstruction.InstructionName = "push";
                        CurrentInstruction.InstructionArguments = indexRegister;
                        break;
                    case 0xCB:
                        b = Data[++CurrentByte];
                        switch (GetField(Field.x, b))
                        {
                            case 0:
                                CurrentInstruction.InstructionName = TableRot[GetField(Field.y, b)];
                                if (GetField(Field.z, b) != 6)
                                    if (Z80PlainMode)
                                        CurrentInstruction.InstructionArguments = TableR[GetField(Field.z, b)] + ", (" + indexRegister + " + " + SignedByte((sbyte)Data[CurrentByte - 1]) + ")";
                                    else
                                        CurrentInstruction.InstructionName = "OPCODETRAP";
                                else
                                    CurrentInstruction.InstructionArguments = TableRot[GetField(Field.y, b)] + "(" + indexRegister + " + " + SignedByte((sbyte)Data[CurrentByte - 1]) + ")";
                                break;
                            case 1:
                                CurrentInstruction.InstructionName = "bit";
                                if (GetField(Field.z, b) != 6 && !Z80PlainMode)
                                    CurrentInstruction.InstructionName = "OPCODETRAP";
                                else
                                    CurrentInstruction.InstructionArguments = GetField(Field.y, b).ToString() + ", (" + indexRegister + " + " + SignedByte((sbyte)Data[CurrentByte - 1]) + ")";
                                break;
                            case 2:
                                CurrentInstruction.InstructionName = "res";
                                if (GetField(Field.z, b) != 6)
                                    if (Z80PlainMode)
                                        CurrentInstruction.InstructionArguments = TableR[GetField(Field.z, b)] + ", " + GetField(Field.y, b).ToString() + ", (" + indexRegister + " + " + SignedByte((sbyte)Data[CurrentByte - 1]) + ")";
                                    else
                                        CurrentInstruction.InstructionName = "OPCODETRAP";
                                else
                                    CurrentInstruction.InstructionArguments = GetField(Field.y, b).ToString() + ", (" + indexRegister + " + " + SignedByte((sbyte)Data[CurrentByte - 1]) + ")";
                                break;
                            case 3:
                                CurrentInstruction.InstructionName = "set";
                                if (GetField(Field.z, b) != 6)
                                    if (Z80PlainMode)
                                        CurrentInstruction.InstructionArguments = TableR[GetField(Field.z, b)] + ", " + GetField(Field.y, b).ToString() + ", (" + indexRegister + " + " + SignedByte((sbyte)Data[CurrentByte - 1]) + ")";
                                    else
                                        CurrentInstruction.InstructionName = "OPCODETRAP";
                                else
                                    CurrentInstruction.InstructionArguments = GetField(Field.y, b).ToString() + ", (" + indexRegister + " + " + SignedByte((sbyte)Data[CurrentByte - 1]) + ")";
                                break;
                        }
                        CurrentByte++;
                        break;
                    case 0xF9:
                        CurrentInstruction.InstructionName = "ld";
                        CurrentInstruction.InstructionArguments = "sp, " + indexRegister;
                        break;
                    case 0x07:
                    case 0x17:
                    case 0x27:
                        if (Z80PlainMode)
                            goto default;
                        CurrentInstruction.InstructionName = "ld";
                        CurrentInstruction.InstructionArguments = TableRP[GetField(Field.p, b)] + ", (" + indexRegister + " + " + SignedByte((sbyte)Data[CurrentByte++]) + ")";
                        break;
                    case 0x37:
                        if (Z80PlainMode)
                            goto default;
                        CurrentInstruction.InstructionName = "ld";
                        CurrentInstruction.InstructionArguments = indexRegister + ", (" + indexRegister + " + " + SignedByte((sbyte)Data[CurrentByte++]) + ")";
                        break;
                    case 0x0F:
                    case 0x1F:
                    case 0x2F:
                        if (Z80PlainMode)
                            goto default;
                        CurrentInstruction.InstructionName = "ld";
                        CurrentInstruction.InstructionArguments = "(" + indexRegister + " + " + SignedByte((sbyte)Data[CurrentByte++]) + "), " + TableRP[GetField(Field.p, b)];
                        break;
                    case 0x3F:
                        if (Z80PlainMode)
                            goto default;
                        CurrentInstruction.InstructionName = "ld";
                        CurrentInstruction.InstructionArguments = "(" + indexRegister + " + " + SignedByte((sbyte)Data[CurrentByte++]) + "), " + indexRegister;
                        break;
                    case 0x31:
                        if (Z80PlainMode)
                            goto default;
                        CurrentInstruction.InstructionName = "ld";
                        CurrentInstruction.InstructionArguments = TableIndex[indexRegNumber ^ 1] + ", (" + indexRegister + " + " + SignedByte((sbyte)Data[CurrentByte++]) + ")";
                        break;
                    case 0x3E:
                        if (Z80PlainMode)
                            goto default;
                        CurrentInstruction.InstructionName = "ld";
                        CurrentInstruction.InstructionArguments = "(" + indexRegister + " + " + SignedByte((sbyte)Data[CurrentByte++]) + "), " + TableIndex[indexRegNumber ^ 1];
                        break;
                    // No case ED; index registers forbidden on ED block.
                    // No case DD or FD: Only last prefix matters.
                    default:
                        CurrentInstruction.InstructionName = "NONI";
                        // This is an invalid prefix sequence, but invalid prefix sequences are just ignored.
                        // We previously incremented CurrentByte to point after this opcode,
                        // so we have to decrement it so we can process it again later.
                        CurrentByte--;
                        break;
                }
            }
        }



        private static readonly string[] TableR = new string[]
        {
            "b",
            "c",
            "d",
            "e",
            "h",
            "l",
            "(hl)",
            "a",
        };

        private static readonly string[] TableIndex = new string[]
        {
            "ix",
            "iy",
        };

        private static readonly string[] TableRP = new string[]
        {
            "bc",
            "de",
            "hl",
            "sp",
        };

        private static readonly string[] TableRP2 = new string[]
        {
            "bc",
            "de",
            "hl",
            "af",
        };

        private static readonly string[] TableCC = new string[]
        {
            "nz",
            "z",
            "nc",
            "c",
            "po",
            "pe",
            "p",
            "m",
        };

        private static readonly string[] TableAlu = new string[]
        {
            "add ",
            "adc ",
            "sub ",
            "sbc ",
            "and ",
            "xor ",
            "or ",
            "cp ",
        };

        private static readonly string[] TableAluArg = new string[]
        {
            "a, ",
            "a, ",
            "",
            "a, ",
            "",
            "",
            "",
            "",
        };

        private static readonly string[] TableRot = new string[]
        {
            "rlc ",
            "rrc ",
            "rl ",
            "rr ",
            "sla ",
            "sra ",
            "sll ",
            "srl ",
        };

        private static readonly string[] TableIM = new string[]
        {
            "0",
            "?",
            "1",
            "2",
            "0",
            "?",
            "1",
            "2",
        };



        private enum Field
        {
            x,
            y,
            z,
            p,
            q,
            pp,
            qq,
        };

        private static int GetField(Field f, int b)
        {
            switch (f)
            {
                case Field.x:
                    return b >> 6;
                case Field.y:
                    return (b >> 3) & 7;
                case Field.z:
                    return b & 7;
                case Field.p:
                    return (b >> 4) & 3;
                case Field.q:
                    return (b >> 3) & 1;
                case Field.pp:
                    return (b >> 5) & 1;
                case Field.qq:
                    return (b >> 3) & 3;
            }
            throw new ArgumentOutOfRangeException();
        }


    }
}
