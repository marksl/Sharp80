﻿/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Sharp80
{
    public static class Extensions
    {
        // TABLES

        private static readonly byte[] BIT = { 0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80 };
        private static readonly byte[] NOT = { 0xFE, 0xFD, 0xFB, 0xF7, 0xEF, 0xDF, 0xBF, 0x7F };
        private static readonly sbyte[] TWOSCOMP =
        {
             0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F,
             0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F,
             0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2E, 0x2F,
             0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F,
             0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4A, 0x4B, 0x4C, 0x4D, 0x4E, 0x4F,
             0x50, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x5F, 
             0x60, 0x61, 0x62, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6A, 0x6B, 0x6C, 0x6D, 0x6E, 0x6F,
             0x70, 0x71, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x7B, 0x7C, 0x7D, 0x7E, 0x7F,
            -0x80,-0x7F,-0x7E,-0x7D,-0x7C,-0x7B,-0x7A,-0x79,-0x78,-0x77,-0x76,-0x75,-0x74,-0x73,-0x72,-0x71,
            -0x70,-0x6F,-0x6E,-0x6D,-0x6C,-0x6B,-0x6A,-0x69,-0x68,-0x67,-0x66,-0x65,-0x64,-0x63,-0x62,-0x61,
            -0x60,-0x5F,-0x5E,-0x5D,-0x5C,-0x5B,-0x5A,-0x59,-0x58,-0x57,-0x56,-0x55,-0x54,-0x53,-0x52,-0x51,
            -0x50,-0x4F,-0x4E,-0x4D,-0x4C,-0x4B,-0x4A,-0x49,-0x48,-0x47,-0x46,-0x45,-0x44,-0x43,-0x42,-0x41,
            -0x40,-0x3F,-0x3E,-0x3D,-0x3C,-0x3B,-0x3A,-0x39,-0x38,-0x37,-0x36,-0x35,-0x34,-0x33,-0x32,-0x31,
            -0x30,-0x2F,-0x2E,-0x2D,-0x2C,-0x2B,-0x2A,-0x29,-0x28,-0x27,-0x26,-0x25,-0x24,-0x23,-0x22,-0x21,
            -0x20,-0x1F,-0x1E,-0x1D,-0x1C,-0x1B,-0x1A,-0x19,-0x18,-0x17,-0x16,-0x15,-0x14,-0x13,-0x12,-0x11,
            -0x10,-0x0F,-0x0E,-0x0D,-0x0C,-0x0B,-0x0A,-0x09,-0x08,-0x07,-0x06,-0x05,-0x04,-0x03,-0x02,-0x01
        };
        private static readonly byte[] TWOSCOMPINVERT_APPLY_0X80_OFFSET =
        {
            0x80,0x81,0x82,0x83,0x84,0x85,0x86,0x87,0x88,0x89,0x8A,0x8B,0x8C,0x8D,0x8E,0x8F,
            0x90,0x91,0x92,0x93,0x94,0x95,0x96,0x97,0x98,0x99,0x9A,0x9B,0x9C,0x9D,0x9E,0x9F,
            0xA0,0xA1,0xA2,0xA3,0xA4,0xA5,0xA6,0xA7,0xA8,0xA9,0xAA,0xAB,0xAC,0xAD,0xAE,0xAF,
            0xB0,0xB1,0xB2,0xB3,0xB4,0xB5,0xB6,0xB7,0xB8,0xB9,0xBA,0xBB,0xBC,0xBD,0xBE,0xBF,
            0xC0,0xC1,0xC2,0xC3,0xC4,0xC5,0xC6,0xC7,0xC8,0xC9,0xCA,0xCB,0xCC,0xCD,0xCE,0xCF,
            0xD0,0xD1,0xD2,0xD3,0xD4,0xD5,0xD6,0xD7,0xD8,0xD9,0xDA,0xDB,0xDC,0xDD,0xDE,0xDF,
            0xE0,0xE1,0xE2,0xE3,0xE4,0xE5,0xE6,0xE7,0xE8,0xE9,0xEA,0xEB,0xEC,0xED,0xEE,0xEF,
            0xF0,0xF1,0xF2,0xF3,0xF4,0xF5,0xF6,0xF7,0xF8,0xF9,0xFA,0xFB,0xFC,0xFD,0xFE,0xFF,
            0x00,0x01,0x02,0x03,0x04,0x05,0x06,0x07,0x08,0x09,0x0A,0x0B,0x0C,0x0D,0x0E,0x0F,
            0x10,0x11,0x12,0x13,0x14,0x15,0x16,0x17,0x18,0x19,0x1A,0x1B,0x1C,0x1D,0x1E,0x1F,
            0x20,0x21,0x22,0x23,0x24,0x25,0x26,0x27,0x28,0x29,0x2A,0x2B,0x2C,0x2D,0x2E,0x2F,
            0x30,0x31,0x32,0x33,0x34,0x35,0x36,0x37,0x38,0x39,0x3A,0x3B,0x3C,0x3D,0x3E,0x3F,
            0x40,0x41,0x42,0x43,0x44,0x45,0x46,0x47,0x48,0x49,0x4A,0x4B,0x4C,0x4D,0x4E,0x4F,
            0x50,0x51,0x52,0x53,0x54,0x55,0x56,0x57,0x58,0x59,0x5A,0x5B,0x5C,0x5D,0x5E,0x5F,
            0x60,0x61,0x62,0x63,0x64,0x65,0x66,0x67,0x68,0x69,0x6A,0x6B,0x6C,0x6D,0x6E,0x6F,
            0x70,0x71,0x72,0x73,0x74,0x75,0x76,0x77,0x78,0x79,0x7A,0x7B,0x7C,0x7D,0x7E,0x7F
        };


        private static string[] BytesAsHex;
        private static string[] WordsAsHex;
        
        // CONSTRUCTOR

        static Extensions()
        {
            BytesAsHex = new string[0x100];
            byte i = 0;
            do
            {
                BytesAsHex[i] = i.ToString("X2");
            } 
            while (++i != 0);

            WordsAsHex = new string[0x10000];
            ushort j = 0;
            do
            {
                WordsAsHex[j] = j.ToString("X4");
            }
            while (++j != 0);


        }

        // ARRAY MANIPULATION

        /// <summary>
        /// Get the subarray, inclusive for start index, exclusive for end index.
        /// Negative End means take the rest of the array
        /// </summary>
        public static T[] Slice<T>(this T[] Source, int Start, int End = -1)
        {
            if (End < 0)
                End = Source.Length;
            else
                End = Math.Min(End, Source.Length);

            if (Start == 0 && End == Source.Length)
                return Source;

            var length = End - Start;

            var ret = new T[length];
            for (int i = 0; i < length; i++)
                ret[i] = Source[i + Start];

            return ret;
        }
        public static T[] Concat<T>(this T[] Array1, T[] Array2)
        {
            var ret = new T[Array1.Length + Array2.Length];
            Array.Copy(Array1, ret, Array1.Length);
            Array.Copy(Array2, 0, ret, Array1.Length, Array2.Length);
            return ret;
        }
        public static ushort[] ToUShortArray(this byte[] Source)
        {
            if (Source.Length % 2 > 0)
                throw new Exception("ToUShortArray: Requires even length source.");

            ushort[] ret = new ushort[Source.Length / 2];
            for (int i = 0, j = 0; i < Source.Length; i += 2, j++)
                ret[j] = Lib.CombineBytes(Source[i], Source[i + 1]);
            
            return ret;
        }
        public static byte[] ToByteArray(this ushort[] Source)
        {
            byte[] ret = new byte[Source.Length * 2];
            for (int i = 0, j = 0; i < Source.Length; i++, j += 2)
                Source[i].Split(out ret[j], out ret[j + 1]);
            return ret;
        }
        public static T[] Double<T>(this T[] Source)
        {
            var ret = new T[Source.Length * 2];
            for (int i = 0; i < Source.Length; i++)
                ret[i * 2] = ret[i * 2 + 1] = Source[i];
            return ret;
        }
        public static T[] Truncate<T>(this T[] Source, int MaxLength) => Source.Length <= MaxLength ? Source : Source.Slice(0, MaxLength);
        public static T[] SetAll<T>(this T[] Array, T Value)
        {
            for (int i = 0; i < Array.Length; i++)
                Array[i] = Value;
            return Array;
        }
        public static T[] SetValues<T>(this T[] Array, ref int Cursor, int Length, T Value)
        {
            int end = Cursor + Length;
            for (; Cursor < end; Cursor++)
                Array[Cursor] = Value;
            return Array;
        }
        public static T[] SetValues<T>(this T[] Array, ref int Cursor, bool Double, params T[] Values)
        {
            foreach (T v in Values)
            {
                Array[Cursor++] = v;
                if (Double)
                    Array[Cursor++] = v;
            }
            return Array;
        }
        /// <summary>
        /// Pads an array to minimum length with given value
        /// NOTE: The origiinal array might be returned!
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="Array"></param>
        /// <param name="Length">The minimum desired length</param>
        /// <param name="Value">The value to pad with</param>
        /// <returns></returns>
        public static T[] Pad<T>(this T[] Array, int Length, T Value)
        {
            return (Array.Length >= Length) ? Array : Array.Concat(new T[Length - Array.Length].SetAll(Value));
        }
        public static bool ArrayEquals<T>(this T[] Source, T[] Other)
        {
            return Source.SequenceEqual(Other);
        }
        public static string ToArrayDeclaration(this byte[] Input) => "{" + String.Join(",", Input.Select(b => "0x" + b.ToHexString())) + "}";
        
        // NUMERIC FUNCTIONS

        public static byte SetBit(this byte Input, byte BitNum) => (byte)(Input | BIT[BitNum]);
        public static byte ResetBit(this byte Input, byte BitNum) => (byte)(Input & NOT[BitNum]);
        public static bool IsBitSet(this byte Input, byte BitNum) =>  (Input & BIT[BitNum]) != 0;
        public static bool IsBitSet(this ushort Input, byte BitNum) => ((Input >> BitNum) & 0x01) == 0x01;
        public static bool RotateAddress(this ushort Input, char c, out ushort Output)
        {
            if (c != '\0')
            {
                string str = Input.ToHexString() + c;
                if (str.Length > 4)
                    str = str.Substring(str.Length - 4, 4);

                return ushort.TryParse(str,
                                    System.Globalization.NumberStyles.AllowHexSpecifier,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    out Output);
            }
            else
            {
                Output = 0;
                return false;
            }
        }
        public static void Split(this ushort NNNN, out byte lowOrderResult, out byte highOrderResult)
        {
            lowOrderResult =  (byte)( NNNN & 0x00FF);
            highOrderResult = (byte)((NNNN & 0xFF00) >> 8);
        }
        public static void Split(this ushort NNNN, out byte? lowOrderResult, out byte? highOrderResult)
        {
            lowOrderResult =  (byte)( NNNN & 0x00FF);
            highOrderResult = (byte)((NNNN & 0xFF00) >> 8);
        }
        public static sbyte TwosComp(this byte input) => TWOSCOMP[input];
        public static byte TwosCompInv(this sbyte Input) => TWOSCOMPINVERT_APPLY_0X80_OFFSET[Input + 0x80];
        public static ushort Offset(this ushort Input, int Offset) => (ushort)(Input + Offset);
        public static bool IsBetween(this ulong Value, ulong Min, ulong Max) => Value >= Min && Value <= Max;
        public static bool IsBetween(this int Value, int Min, int Max) => Value >= Min && Value <= Max;
        public static bool IsBetween(this char Value, char Min, char Max) => Value >= Min && Value <= Max;
        public static bool IsBetween(this byte Value, byte Min, byte Max) => Value >= Min && Value <= Max;

        // STRINGS

        public static string Repeat(this String Input, int Count)
        {
            Count = Math.Max(0, Count);
            switch (Count)
            {
                case 0:
                    return String.Empty;
                case 1:
                    return Input;
                default:
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < Count; i++)
                        sb.Append(Input);
                    return sb.ToString();
            }
        }
        public static string Truncate(this string Input, int Chars)
        {
            if (Input.Length < Chars)
                return Input;
            else
                return Input.Substring(0, Chars);
        }
        public static string FirstText(this string Input)
        {
            if (Input.Contains(" "))
                return Input.Substring(0, Input.IndexOf(' '));
            else
                return Input;
        }

        // HEXADECIMAL

        public static string ToHexString(this byte Input)
        {
            unchecked
            {
                return BytesAsHex[Input];
            }
        }
        public static string ToHexString(this char Input)
        {
            return ToHexString((byte)Input);
        }
        public static string ToHexString(this ushort Input)
        {
            unchecked
            {
                return WordsAsHex[Input];
            }
        }
        public static string ToHexString(this sbyte Input) => (Input < 0 ? "-" : "") + ToHexString((byte)(Math.Abs(Input)));
        public static string ToHexString(this uint input)
        {
            return input.ToString("X8");
        }
        public static string ToHexChar(this ushort Input)
        {
            return ((byte)(Input & 0x0F)).ToHexString().Substring(1);
        }
        public static string ToTwosCompHexString(this byte input)
        {
            if ((input & 0x80) == 0x80)
                return "-" + ((byte)(1 + ((input & 0x7F) ^ 0x7F))).ToHexString();
            else
                return "+" + input.ToHexString();
        }
        public static string ToHexDisplay(this byte[] Input)
        {
            var lines = Input.Select((x, i) => new { Index = i, Value = x })
                             .GroupBy(x => x.Index / 0x10)
                             .Select(x => x.Select(v => v.Value).ToList())
                             .ToList();

            return String.Join(Environment.NewLine, lines.Select(l => String.Join(" ", l.Select(ll => ll.ToHexString()))));
        }
        public static byte ToHexCharByte(this int Input)
        {
            Input &= 0x0F;

            if (Input < 0x0A)
                Input += '0';
            else
                Input += ('A' - 10);

            return (byte)Input;
        }
        public static byte ToHexCharByte(this byte Input)
        {
            Input &= 0x0F;

            if (Input < 0x0A)
                Input += (byte)'0';
            else
                Input += ('A' - 10);

            return Input;
        }

        // FILE PATHS

        public static string MakeUniquePath(this string Path)
        {
            var Dir = System.IO.Path.GetDirectoryName(Path);
            var FileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(Path);
            var Extension = System.IO.Path.GetExtension(Path);

            int i = 0;
            do
            {
                string name = i++ > 0 ? $"{FileNameWithoutExtension} ({i})"
                                      : FileNameWithoutExtension;

                Path = System.IO.Path.Combine(Dir, name + Extension);
            }
            while (File.Exists(Path));

            return Path;
        }
        public static string ReplaceExtension(this string Path, string NewExtension)
        {
            return System.IO.Path.ChangeExtension(Path, NewExtension);
        }
        
        // EXCEPTIONS

        public static string ToReport(this Exception Ex)
        {
            string exMsg;
            if (Ex.Data.Contains("ExtraMessage"))
                exMsg = Ex.Data["ExtraMessage"] + Environment.NewLine;
            else
                exMsg = String.Empty;

            return string.Format("{0} Exception" + Environment.NewLine +
                                 "{1}" +
                                 "{2}" +
                                 "Source: {3}" + Environment.NewLine +
                                 "H_RESULT: 0x{4:X8}" + Environment.NewLine +
                                 "Target Site: {5}" + Environment.NewLine +
                                 "Stack Trace:" + Environment.NewLine +
                                 "{6}",
                                 Ex.GetType(),
                                 Ex.Message,
                                 exMsg,
                                 Ex.Source,
                                 Ex.HResult,
                                 Ex.TargetSite,
                                 string.Join(Environment.NewLine, new System.Diagnostics.StackTrace(Ex).GetFrames().Select(f => f.ToReport())));
        }
        public static string ToReport(this System.Diagnostics.StackFrame Frame)
        {
            var method = Frame.GetMethod();
            if (method.Name.Equals("LogStack"))
                return String.Empty;

            return string.Format("{0}::{1}({2})",
                        method.ReflectedType?.Name ?? String.Empty, method.Name,
                        String.Join(", ", method.GetParameters().Select(p => p.Name)));
        }

        // KEYS

        public static (KeyCode Code, bool Shifted) ToKeyCode(this char c)
        {
            switch (c)
            {
                case '\n': return (KeyCode.Return, false);

                case ' ': return (KeyCode.Space, false);

                case 'a': return (KeyCode.A, false);
                case 'A': return (KeyCode.A, true);
                case 'b': return (KeyCode.B, false);
                case 'B': return (KeyCode.B, true);
                case 'c': return (KeyCode.C, false);
                case 'C': return (KeyCode.C, true);
                case 'd': return (KeyCode.D, false);
                case 'D': return (KeyCode.D, true);
                case 'e': return (KeyCode.E, false);
                case 'E': return (KeyCode.E, true);
                case 'f': return (KeyCode.F, false);
                case 'F': return (KeyCode.F, true);
                case 'g': return (KeyCode.G, false);
                case 'G': return (KeyCode.G, true);
                case 'h': return (KeyCode.H, false);
                case 'H': return (KeyCode.H, true);
                case 'i': return (KeyCode.I, false);
                case 'I': return (KeyCode.I, true);
                case 'j': return (KeyCode.J, false);
                case 'J': return (KeyCode.J, true);
                case 'k': return (KeyCode.K, false);
                case 'K': return (KeyCode.K, true);
                case 'l': return (KeyCode.L, false);
                case 'L': return (KeyCode.L, true);
                case 'm': return (KeyCode.M, false);
                case 'M': return (KeyCode.M, true);
                case 'n': return (KeyCode.N, false);
                case 'N': return (KeyCode.N, true);
                case 'o': return (KeyCode.O, false);
                case 'O': return (KeyCode.O, true);
                case 'p': return (KeyCode.P, false);
                case 'P': return (KeyCode.P, true);
                case 'q': return (KeyCode.Q, false);
                case 'Q': return (KeyCode.Q, true);
                case 'r': return (KeyCode.R, false);
                case 'R': return (KeyCode.R, true);
                case 's': return (KeyCode.S, false);
                case 'S': return (KeyCode.S, true);
                case 't': return (KeyCode.T, false);
                case 'T': return (KeyCode.T, true);
                case 'u': return (KeyCode.U, false);
                case 'U': return (KeyCode.U, true);
                case 'v': return (KeyCode.V, false);
                case 'V': return (KeyCode.V, true);
                case 'w': return (KeyCode.W, false);
                case 'W': return (KeyCode.W, true);
                case 'x': return (KeyCode.X, false);
                case 'X': return (KeyCode.X, true);
                case 'y': return (KeyCode.Y, false);
                case 'Y': return (KeyCode.Y, true);
                case 'z': return (KeyCode.Z, false);
                case 'Z': return (KeyCode.Z, true);

                case '.': return (KeyCode.Period, false);
                case '>': return (KeyCode.Period, true);
                case ',': return (KeyCode.Comma, false);
                case '<': return (KeyCode.Comma, true);
                case '/': return (KeyCode.Slash, false);
                case '?': return (KeyCode.Slash, true);
                case '!': return (KeyCode.D1, true);
                case '@': return (KeyCode.D2, true);
                case '#': return (KeyCode.D3, true);
                case '$': return (KeyCode.D4, true);
                case '%': return (KeyCode.D5, true);
                case '^': return (KeyCode.D6, true);
                case '&': return (KeyCode.D7, true);
                case '*': return (KeyCode.D8, true);
                case '(': return (KeyCode.D9, true);
                case ')': return (KeyCode.D0, true);
                case '-': return (KeyCode.Minus, false);
                case '_': return (KeyCode.Minus, true);
                case '=': return (KeyCode.Equals, false);
                case '+': return (KeyCode.Equals, true);
                case ';': return (KeyCode.Semicolon, false);
                case ':': return (KeyCode.Semicolon, true);
                case '\'': return (KeyCode.Apostrophe, false);

                case '"':
                case (char)0x201C:
                case (char)0x201D:
                    return (KeyCode.Apostrophe, true);

                // these aren't consumable by the trs80, so we change them to keys that are:
                case '[': return (KeyCode.D9, true);
                case '{': return (KeyCode.D9, true);
                case ']': return (KeyCode.D0, true);
                case '}': return (KeyCode.D0, true);
                case '\\': return (KeyCode.Slash, false);
                case '|': return (KeyCode.Slash, true);

                case '0': return (KeyCode.D0, false);
                default:
                    if (c.IsBetween('1', '9'))
                        return (KeyCode.D1 + (c - '1'), false);
                    break;
            }
            return (KeyCode.None, false);
        }

        // COMPRESSION

        public static byte[] Compress(this byte[] data)
        {
            var output = new MemoryStream();
            using (DeflateStream ds = new DeflateStream(output, CompressionLevel.Optimal))
            {
                ds.Write(data, 0, data.Length);
            }
            var o = output.ToArray();
            System.Diagnostics.Debug.Assert(Decompress(o).ArrayEquals(data));
            return o;
        }
        public static byte[] Decompress(this byte[] data)
        {
            var input = new MemoryStream(data);
            var output = new MemoryStream();
            using (DeflateStream ds = new DeflateStream(input, CompressionMode.Decompress))
            {
                ds.CopyTo(output);
            }
            var o = output.ToArray();
            return o;
        }
    }
}
