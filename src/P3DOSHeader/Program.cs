using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P3DOSHeader
{
    class Program
    {
        static bool Interactive;
        static string InputFileName;
        static string OutputFileName;
        static FileTypes FileType = FileTypes.Code;
        static FileTypes ExistingFileType = FileTypes.Unknown;
        static ushort Address;
        static ushort Variables;
        static byte[] Header;
        static byte[] Body;
        static ushort Line;
        static bool RemoveHeader;
        static ushort ExistingArg1 = 0;
        static ushort ExistingArg2 = 0;
        static bool AddressSpecified;
        static bool VariablesSpecified;
        static bool LineSpecified;
        static byte Name;
        static bool NameSpecified;

        static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    Console.WriteLine("Usage:");
                    Console.WriteLine();
                    Console.WriteLine("P3DOSHeader [-f <InputFile>]");
                    Console.WriteLine("            [-o <OutputFile>]");
                    Console.WriteLine("            [-b [-l <Line>] [-v <VariablesOffset>]]");
                    Console.WriteLine("            [-na -n <NumericArrayName>]");
                    Console.WriteLine("            [-ca -n <CharacterArrayName>]");
                    Console.WriteLine("            [-c [-a <LoadAddress>]");
                    Console.WriteLine("            [-r]");
                    Console.WriteLine("            [-i]");
                    return 0;
                }

                // Parse args
                Interactive = args.Any(a => a == "-i");
                InputFileName = GetArg(args, "-f");
                OutputFileName = GetArg(args, "-o");
                if (args.Any(a => a == "-b")) FileType = FileTypes.BASIC;
                else if (args.Any(a => a == "-na")) FileType = FileTypes.NumericArray;
                else if (args.Any(a => a == "-ca")) FileType = FileTypes.CharArray;
                else if (args.Any(a => a == "-c")) FileType = FileTypes.Code;
                Address = GetArgWord(args, "-a", out AddressSpecified);
                Variables = GetArgWord(args, "-v", out VariablesSpecified);
                Line = GetArgWord(args, "-l", out LineSpecified);
                RemoveHeader = args.Any(a => a == "-r");
                Name = Convert.ToByte((GetArg(args, "-n") + "\0")[0]);
                NameSpecified = Name != '\0';

                // Validate arguments
                if (string.IsNullOrWhiteSpace(OutputFileName))
                    OutputFileName = InputFileName;
                if (!File.Exists(InputFileName))
                    throw new ArgumentException("File \"" + InputFileName + "\" doesn't exist.", "-f");
                if (FileType == FileTypes.BASIC && Line > 9999)
                    throw new ArgumentException("BASIC start line can't be more than 9999.", "-l");

                // Open input file
                var file = File.ReadAllBytes(InputFileName);
                Header = ParseHeader(file, out Body);

                // Set file type and length
                Header[15] = Convert.ToByte(FileType);
                SetLongWord(Header, 16, Body.Length);

                // Set BASIC options
                if (FileType == FileTypes.BASIC)
                {
                    Console.WriteLine("Adding a BASIC header (type 0).");

                    // Set line (argument 1)
                    if (LineSpecified)
                        SetLongWord(Header, 18, Line);
                    else if (ExistingFileType == FileTypes.BASIC)
                        SetLongWord(Header, 18, ExistingArg1);
                    else
                        SetLongWord(Header, 18, 0x8000);
                    var line = GetWord(Header, 18);
                    if (line != 0x8000)
                        Console.WriteLine("Setting autorun line to " + line + ".");
                    else
                        Console.WriteLine("Clearing autorun line ($" + line.ToString("X4") + ").");

                    // Set variables offset (argument 2)
                    if (VariablesSpecified)
                        SetLongWord(Header, 20, Line);
                    else if (ExistingFileType == FileTypes.BASIC)
                        SetLongWord(Header, 20, ExistingArg2);
                    else
                        SetLongWord(Header, 20, Body.Length);
                    Console.WriteLine("Setting variable offset address to $" + GetWord(Header, 20).ToString("X4") + ".");
                }

                // Set Numeric Array options
                if (FileType == FileTypes.NumericArray)
                {
                    Console.WriteLine("Adding a numeric array header (type 1).");
                    Header[18] = 0x00;
                    if (NameSpecified)
                        Header[19] = Name;
                    else if (ExistingFileType != FileTypes.NumericArray)
                        throw new ArgumentException("Missing numeric array name", "-n");
                    SetLongWord(Header, 20, 0);
                    Console.WriteLine("Setting variable name to " + Convert.ToChar(Header[19]) + ".");
                }

                // Set Character Array options
                if (FileType == FileTypes.CharArray)
                {
                    Console.WriteLine("Adding a character array header (type 2).");
                    Header[18] = 0x00;
                    if (NameSpecified)
                        Header[19] = Name;
                    else if (ExistingFileType != FileTypes.CharArray)
                        throw new ArgumentException("Missing character array name", "-n");
                    SetLongWord(Header, 20, 0);
                    Console.WriteLine("Setting variable name to " + Convert.ToChar(Header[19]) + ".");
                }

                // Set Code/SCREEN$ options
                if (FileType == FileTypes.Code)
                {
                    Console.WriteLine("Adding a code header (type 3).");
                    if (AddressSpecified)
                        SetLongWord(Header, 18, Address);
                    else if (ExistingFileType == FileTypes.Code)
                        SetLongWord(Header, 18, ExistingArg1);
                    else
                        SetLongWord(Header, 18, 0);
                    Console.WriteLine("Setting execute address to $" + GetWord(Header, 18).ToString("X4") + ".");
                    SetLongWord(Header, 20, 0);
                }

                // Calculate Checksum
                CalculateChecksum(Header);
                Console.WriteLine("Calculated checksum as $" + Header[127].ToString("X2") + ".");

                // Construct final file
                var final = new byte[RemoveHeader ? Body.Length : Header.Length + Body.Length];
                if (RemoveHeader)
                {
                    Console.WriteLine("Saving file without +3DOS header.");
                    Array.Copy(Body, final, Body.Length);
                }
                else
                {
                    Console.WriteLine("Saving file with +3DOS header.");
                    Array.Copy(Header, final, Header.Length);
                    Array.Copy(Body, 0, final, Header.Length, Body.Length);
                }

                // Write output file
                try
                {
                    File.WriteAllBytes(OutputFileName, final);
                    Console.WriteLine("Saved file \"" + OutputFileName + "\".");
                }
                catch
                {
                    throw new IOException("Could not save file \"" + OutputFileName + "\".");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return 1;
            }
            finally
            {
                if (Interactive)
                {
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey();
                }
            }
            return 0;
        }

        static string GetArg(string[] Args, string Flag, bool RemoveQuotes = true)
        {
            Args = Args ?? new string[0];
            Flag = Flag ?? "";
            for (int i = 0; i < (Args).Length; i++)
            {
                if (Args[i] == Flag && Args.Length >= i)
                {
                    string val = (Args[i + 1] ?? "");
                    if (val.Length >= 2 && val.StartsWith("\"") && val.EndsWith("\""))
                        val = val.Substring(1, val.Length - 2);
                    return val;
                }
            }
            return "";
        }

        static ushort GetArgWord(string[] Args, string Flag, out bool WasFound)
        {
            WasFound = false;
            string val = "";
            try
            {
                ushort rv = 0;
                val = GetArg(Args, Flag, false);
                if (string.IsNullOrWhiteSpace(val))
                    return 0;
                WasFound = true;
                if (val.StartsWith("$") || val.StartsWith("#"))
                    rv = Convert.ToUInt16(val.Substring(1), 16);
                else if (val.StartsWith("0x"))
                    rv = Convert.ToUInt16(val.Substring(2), 16);
                else
                    rv = Convert.ToUInt16(val);
                return rv;
            }
            catch
            {
                throw new ArgumentException("\"" + val + "\" is not a number.", Flag ?? "Unknown");
            }
        }

        static T GetArg<T>(string[] Args, string Flag, T DefaultValue)
        {
            try
            {
                var val = GetArg(Args, Flag, false);
                var converter = TypeDescriptor.GetConverter(typeof(T));
                if (converter != null)
                    return (T)converter.ConvertFromString(val);
                return DefaultValue;
            }
            catch
            {
                return DefaultValue;
            }
        }

        static byte[] ParseHeader(byte[] Contents, out byte[] Body)
        {
            Contents = Contents ?? new byte[0];
            bool hasHeader = Contents.Length >= 128;
            hasHeader = hasHeader
                && Contents[0] == 'P' && Contents[1] == 'L' && Contents[2] == 'U' && Contents[3] == 'S'
                && Contents[4] == '3' && Contents[5] == 'D' && Contents[6] == 'O' && Contents[7] == 'S';
            byte cs = hasHeader ? Convert.ToByte(Contents.Take(127).Sum(b => b) % 256) : (byte)0;
            hasHeader = hasHeader && (cs == Contents[127]);
            if (hasHeader)
            {
                var header = new byte[128];
                Body = new byte[Contents.Length - 128];
                Array.Copy(Contents, header, 128);
                Array.Copy(Contents, 128, Body, 0, Body.Length);
                SetLongWord(header, 11, header.Length + Body.Length);
                ExistingFileType = (FileTypes)header[15];
                ExistingArg1 = GetWord(header, 18);
                ExistingArg2 = GetWord(header, 20);
                return header;
            }
            else
            {
                Body = Contents;
                var header = CreateHeader();
                SetLongWord(header, 11, header.Length + Body.Length);
                return header;
            }
        }

        static byte[] CreateHeader()
        {
            var header = new byte[128];
            header[00] = Convert.ToByte('P'); // +3DOS signature
            header[01] = Convert.ToByte('L');
            header[02] = Convert.ToByte('U');
            header[03] = Convert.ToByte('S');
            header[04] = Convert.ToByte('3');
            header[05] = Convert.ToByte('D');
            header[06] = Convert.ToByte('O');
            header[07] = Convert.ToByte('S');
            header[08] = 0x1A; // Soft-EOF (end of file)
            header[09] = 0x01; // Issue number
            header[10] = 0x00; // Version number
            return header;
        }

        static void SetLongWord(byte[] Array, int Index, Int32 Value)
        {
            Array[Index + 0] = (byte)Value;
            Array[Index + 1] = (byte)(((uint)Value >> 8) & 0xFF);
            Array[Index + 2] = (byte)(((uint)Value >> 16) & 0xFF);
            Array[Index + 3] = (byte)(((uint)Value >> 24) & 0xFF);
        }

        static void SetWord(byte[] Array, int Index, Int32 Value)
        {
            Array[Index + 0] = (byte)Value;
            Array[Index + 1] = (byte)(((uint)Value >> 8) & 0xFF);
        }

        static ushort GetWord(byte[] Array, int Index)
        {
            return Convert.ToUInt16(Array[Index + 0] + Array[Index + 1] * 256);
        }

        static void CalculateChecksum(byte[] Header)
        {
            if (Header == null || Header.Length != 128)
                throw new InvalidOperationException("Unexpected header size: " +(Header ?? new byte[0]).Length);
            byte cs = Convert.ToByte(Header.Take(127).Sum(b => b) % 256);
            Header[127] = cs;
        }
    }

    enum FileTypes
    {
        BASIC = 0,
        NumericArray = 1,
        CharArray = 2,
        Code = 3,
        Unknown = 255
    }
}
