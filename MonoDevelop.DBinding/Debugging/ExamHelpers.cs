//
// ExamHelpers.cs
//
// Author:
//       Ľudovít Lučenič <llucenic@gmail.com>
//
// Copyright (c) 2013 Copyleft
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Text;

using D_Parser.Parser;
using D_Parser.Resolver;


namespace MonoDevelop.D.Debugging
{
	public class ExamHelpers
	{
		public static byte SizeOf(byte typeToken, bool is64Bit = false)
		{
			switch (typeToken) {
				case DTokens.Bool:
				case DTokens.Byte:
				case DTokens.Ubyte:
				case DTokens.Char:
					return 1;
				case DTokens.Short:
				case DTokens.Ushort:
				case DTokens.Wchar:
					return 2;
				case DTokens.Int:
				case DTokens.Uint:
				case DTokens.Dchar:
				case DTokens.Float:
					return 4;
				case DTokens.Long:
				case DTokens.Ulong:
				case DTokens.Double:
					return 8;
				case DTokens.Real:
					// size is 80 bits = 10 bytes
					if (is64Bit)
						// alignment is to 16 bytes (on 64 bit architecture)
						return 16;
					else
						// and 12 bytes (on 32 bit architecture) 
						return 12;
				default:
					return 1;
			}
		}

		public static decimal GetNumericValue(byte[] array, int i, byte typeToken)
		{
			decimal v = 0.0M;
			switch (typeToken)
			{
				case DTokens.Bool:
					v = BitConverter.ToBoolean(array, i) ? 1M : 0M;
					break;
				case DTokens.Byte:
					v = (decimal)(sbyte)array[i];
					break;
				case DTokens.Ubyte:
					v = (decimal)array[i];
					break;
				case DTokens.Short:
					v = (decimal)BitConverter.ToInt16(array, i);
					break;
				case DTokens.Ushort:
					v = (decimal)BitConverter.ToUInt16(array, i);
					break;
				case DTokens.Int:
					v = (decimal)BitConverter.ToInt32(array, i);
					break;
				case DTokens.Uint:
					v = (decimal)BitConverter.ToUInt32(array, i);
					break;
				case DTokens.Long:
					v = (decimal)BitConverter.ToInt64(array, i);
					break;
				case DTokens.Ulong:
					v = (decimal)BitConverter.ToUInt64(array, i);
					break;

				case DTokens.Float:
					v = (decimal)BitConverter.ToSingle(array, i);
					break;
				case DTokens.Double:
					v = (decimal)BitConverter.ToDouble(array, i);
					break;
				case DTokens.Real:
					v = (decimal)RealToDouble(array, i);
					break;
			}

			return v;
		}

		public delegate string ValueFunction(byte[] array, int i, bool hex);

		static string ToString2(object o, bool hex, int padLength)
		{
			return hex ? String.Format ("0x{0:x"+padLength.ToString()+"}", o) : o.ToString ();
		}

		static string GetBoolValue  (byte[] array, int i, bool hex) { return BitConverter.ToBoolean(array, i) ? Boolean.TrueString : Boolean.FalseString; }

		static string GetByteValue  (byte[] array, int i, bool hex) { return ToString2((sbyte)array[i], hex, 2); }
		static string GetUbyteValue (byte[] array, int i, bool hex) { return ToString2(array[i], hex, 2); }

		static string GetShortValue (byte[] array, int i, bool hex) { return ToString2(BitConverter.ToInt16 (array, i), hex, 4); }
		static string GetIntValue   (byte[] array, int i, bool hex) { return ToString2(BitConverter.ToInt32 (array, i), hex, 8); }
		static string GetLongValue  (byte[] array, int i, bool hex) { return ToString2(BitConverter.ToInt64 (array, i), hex, 16); }

		static string GetUshortValue(byte[] array, int i, bool hex) { return ToString2(BitConverter.ToUInt16 (array, i), hex, 4); }
		static string GetUintValue  (byte[] array, int i, bool hex) { return ToString2(BitConverter.ToUInt32 (array, i), hex, 8); }
		static string GetUlongValue (byte[] array, int i, bool hex) { return ToString2(BitConverter.ToUInt64 (array, i), hex, 16); }

		static string GetFloatValue (byte[] array, int i, bool hex) { return BitConverter.ToSingle(array, i).ToString(); }
		static string GetDoubleValue(byte[] array, int i, bool hex) { return BitConverter.ToDouble(array, i).ToString(); }

		public static double RealToDouble(byte[] array, int i)
		{
			// method converts real precision (80bit) to double precision (64bit)
			// since c# does not natively support real precision variables
			ulong realFraction = BitConverter.ToUInt64 (array, i);		// read in first 8 bytes
			ulong realIntPart = realFraction >> 63;											// extract bit 64 (explicit integer part), this is hidden in double precision
			realFraction &= ~(ulong)(realIntPart << 63); // 0x7FFFFFFFFFFFFFFF				// use only 63 bits for fraction (strip off the integer part bit)
			ushort realExponent = BitConverter.ToUInt16 (array, (i + 8));	// read in the last 2 bytes
			ushort realSign = (ushort)(realExponent >> 15);									// extract sign bit (most significant)
			realExponent &= 0x7FFF;															// strip the sign bit off the exponent

			ulong doubleFraction = realFraction >> 11;				// decrease the fraction precision from real to double
			const ushort realBias = 16383;							// exponents in real as well as double precision are biased (increased by)
			const ushort doubleBias = 1023;
			ushort doubleExponent = (ushort)(realExponent - realBias /* unbias real */ + doubleBias /* bias double */);		// calculate the biased exponent for double precision

			ulong doubleBytes;
			if (realIntPart == 0) {
				// we need to normalize the real fraction if the integer part was not set to 1
				ushort neededShift = 1;						// counter for needed fraction left shift in order to normalize it
				ulong fractionIter = realFraction;			// shift left iterator of real precision number fraction
				const ulong bitTest = 1 << 62;				// test for most significant bit
				while (neededShift < 63 && (fractionIter & bitTest) == 0) {
					++neededShift;
					fractionIter <<= 1;
				}
				if (fractionIter > 0) {
					// we normalize the fraction and adjust the exponent
					// TODO: this code needs to be tested
					doubleExponent += neededShift;
					doubleFraction = (realFraction << neededShift) >> (11 + neededShift);
				}
				else {
					// impossible to normalize
					return double.NaN;
				}
			}
			// we add up all parts to form double precision number
			doubleBytes = doubleFraction;
			doubleBytes |= ((ulong)doubleExponent << 52);
			doubleBytes |= ((ulong)realSign << 63);
			
			return BitConverter.ToDouble(BitConverter.GetBytes(doubleBytes), 0);
		}

		static string GetRealValue (byte[] array, int i, bool hex)
		{
			return RealToDouble(array, i).ToString();
			/*
			 * -var-create - * "*(long double*)(((unsigned long[])REAL)[1]+24)"
			 * ^done,name="var15",numchild="0",value="-2.9999999999999999999130411670751266e-154",type="long double",thread-id="1",has_more="0"
			 */
		}


		static string FormatCharValue(char aChar, bool hex, int aSize)
		{
			return hex ? String.Format("0x{1:x" + aSize*2 + "}", aChar) : aChar.ToString();
		}

		static string GetCharValue (byte[] array, int i, bool hex)
		{
			char[] chars = Encoding.UTF8.GetChars(array, i, 1);
			if ((uint)chars[0] == 0xFFFD) {
				// code point is wider than 1 byte
				chars = Encoding.UTF8.GetChars(array, i, i+2 > array.Length ? 1 : 2);
				if ((uint)chars[0] == 0xFFFD) {
					// code point was already in previous char
					return "(skipped code point)";
				}
				else {
					// code point is resolved correctly
					return FormatCharValue(chars[0], hex, 1) + " (multi-code point)";
				}
			}
			return FormatCharValue(chars[0], hex, 1);
		}
		static string GetWcharValue(byte[] array, int i, bool hex)
		{
			char[] chars = Encoding.Unicode.GetChars(array, i, 2);
			return FormatCharValue(chars[0], hex, 2);
		}
		static string GetDcharValue(byte[] array, int i, bool hex)
		{
			char[] chars = Encoding.UTF32.GetChars(array, i, 4);
			return FormatCharValue(chars[0], hex, 4);
		}

		public static string GetStringValue(byte[] array, byte typeToken = DTokens.Char)
		{
			if (array == null)
				return null;
			switch (typeToken) {
				default:
				case DTokens.Char:		return Encoding.UTF8.GetString(array);
				case DTokens.Wchar:		return Encoding.Unicode.GetString(array);
				case DTokens.Dchar:		return Encoding.UTF32.GetString(array);
			}
		}

		public static ValueFunction GetValueFunction(byte typeToken)
		{
			switch (typeToken) {
				case DTokens.Bool:		return GetBoolValue;
				case DTokens.Byte:		return GetByteValue;
				case DTokens.Ubyte:		return GetUbyteValue;
				case DTokens.Short:		return GetShortValue;
				case DTokens.Ushort:	return GetUshortValue;
				case DTokens.Int:		return GetIntValue;
				case DTokens.Uint:		return GetUintValue;
				case DTokens.Long:		return GetLongValue;
				case DTokens.Ulong:		return GetUlongValue;
				case DTokens.Float:		return GetFloatValue;
				case DTokens.Double:	return GetDoubleValue;
				case DTokens.Real:		return GetRealValue;
				case DTokens.Char:		return GetCharValue;
				case DTokens.Wchar:		return GetWcharValue;
				case DTokens.Dchar:		return GetDcharValue;
				default:				return GetByteValue;
			}
		}

		public static string AliasStringTypes(string type)
		{
			if (type.Contains("immutable(char)[]")) {
				// we support Phobos alias for string
				return type.Replace("immutable(char)[]", "string");
			}
			else if (type.Contains("immutable(wchar)[]")) {
				// we support Phobos alias for wstring
				return type.Replace("immutable(wchar)[]", "wstring");
			}
			else if (type.Contains("immutable(dchar)[]")) {
				// we support Phobos alias for dstring
				return type.Replace("immutable(dchar)[]", "dstring");
			}
			else {
				return type;
			}
		}

	}
}

