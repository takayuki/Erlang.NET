/*
 * %CopyrightBegin%
 * 
 * Copyright Takayuki Usui 2009. All Rights Reserved.
 * Copyright Ericsson AB 2000-2009. All Rights Reserved.
 * 
 * The contents of this file are subject to the Erlang Public License,
 * Version 1.1, (the "License"); you may not use this file except in
 * compliance with the License. You should have received a copy of the
 * Erlang Public License along with this software. If not, it can be
 * retrieved online at http://www.erlang.org/.
 * 
 * Software distributed under the License is distributed on an "AS IS"
 * basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
 * the License for the specific language governing rights and limitations
 * under the License.
 * 
 * %CopyrightEnd%
 */
using System;
using System.Text;

namespace Erlang.NET
{
    /**
     * Provides a Java representation of Erlang strings.
     */
    [Serializable]
    public class OtpErlangString : OtpErlangObject
    {
        // don't change this!
        internal static readonly new long serialVersionUID = -7053595217604929233L;

        private readonly String str;

        /**
         * Create an Erlang string from the given string.
         */
        public OtpErlangString(String str)
        {
            this.str = str;
        }

        /**
         * Create an Erlang string from a list of integers.
         * 
         * @return an Erlang string with Unicode code units.
         *
         * @throws OtpErlangException
         *                for non-proper and non-integer lists.
         * @throws OtpErlangRangeException
         *                if an integer in the list is not
         *                a valid Unicode code point according to Erlang.
         */
        public OtpErlangString(OtpErlangList list)
        {
            String s = list.stringValue();

            int[] offset = System.Globalization.StringInfo.ParseCombiningCharacters(s);
            int n = offset.Length;
            for (int i = 0; i < n; i++)
            {
                int cp = (int)codePointAt(s, offset[i]);
                if (!isValidCodePoint(cp))
                {
                    throw new OtpErlangRangeException("Invalid CodePoint: " + cp);
                }
            }
            str = s;
        }

        /**
         * Create an Erlang string from a stream containing a string encoded in
         * Erlang external format.
         * 
         * @param buf
         *            the stream containing the encoded string.
         * 
         * @exception OtpErlangDecodeException
         *                if the buffer does not contain a valid external
         *                representation of an Erlang string.
         */
        public OtpErlangString(OtpInputStream buf)
        {
            str = buf.read_string();
        }

        /**
         * Get the actual string contained in this object.
         * 
         * @return the raw string contained in this object, without regard to Erlang
         *         quoting rules.
         * 
         * @see #toString
         */
        public String stringValue()
        {
            return str;
        }

        /**
         * Get the printable version of the string contained in this object.
         * 
         * @return the string contained in this object, quoted.
         * 
         * @see #stringValue
         */
        public override String ToString()
        {
            return "\"" + str + "\"";
        }

        /**
         * Convert this string to the equivalent Erlang external representation.
         * 
         * @param buf
         *            an output stream to which the encoded string should be
         *            written.
         */
        public override void encode(OtpOutputStream buf)
        {
            buf.write_string(str);
        }

        /**
         * Determine if two strings are equal. They are equal if they represent the
         * same sequence of characters. This method can be used to compare
         * OtpErlangStrings with each other and with Strings.
         * 
         * @param o
         *            the OtpErlangString or String to compare to.
         * 
         * @return true if the strings consist of the same sequence of characters,
         *         false otherwise.
         */
        public override bool Equals(Object o)
        {
            if (o is String)
            {
                return str.CompareTo((String)o) == 0;
            }
            else if (o is OtpErlangString)
            {
                return str.CompareTo(((OtpErlangString)o).str) == 0;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        protected override int doHashCode()
        {
            return str.GetHashCode();
        }

        /**
         * Create Unicode code points from a String.
         * 
         * @param  s
         *             a String to convert to an Unicode code point array
         *
         * @return the corresponding array of integers representing
         *         Unicode code points
         */
        public static int[] stringToCodePoints(String s)
        {
            int[] offset = System.Globalization.StringInfo.ParseCombiningCharacters(s);
            int m = offset.Length;
            int[] codePoints = new int[m];
            for (int i = 0; i < m; i++)
            {
                codePoints[i] = codePointAt(s, offset[i]);
            }
            return codePoints;
        }

        public static char codePointAt(String s, int index)
        {
            if (Char.IsHighSurrogate(s[index]))
            {
                char high = s[index], low = s[index + 1];

                if (Char.IsLowSurrogate(low))
                {
                    return (char)((high - 0xD800) * 0x400 + (low - 0xDC00) + 0x10000);
                }
            }
            return s[index];
        }

        /**
         * Validate a code point according to Erlang definition; Unicode 3.0.
         * That is; valid in the range U+0..U+10FFFF, but not in the range
         * U+D800..U+DFFF (surrogat pairs), nor U+FFFE..U+FFFF (non-characters).
         *
         * @param  cp
         *             the code point value to validate
         *
         * @return true if the code point is valid,
         *         false otherwise.
         */
        public static bool isValidCodePoint(int cp)
        {
            // Erlang definition of valid Unicode code points; 
            // Unicode 3.0, XML, et.al.
            return (((uint)cp) >> 16) <= 0x10 // in 0..10FFFF; Unicode range
            && (cp & ~0x7FF) != 0xD800 // not in D800..DFFF; surrogate range
            && (cp & ~1) != 0xFFFE; // not in FFFE..FFFF; non-characters
        }

        /**
         * Construct a String from a Latin-1 (ISO-8859-1) encoded byte array,
         * if Latin-1 is available, otherwise use the default encoding. 
         *
         */
        public static String newString(byte[] bytes)
        {
            try
            {
                return Encoding.GetEncoding("iso-8859-1").GetString(bytes);
            }
            catch (ArgumentException)
            {
            }
            return Encoding.ASCII.GetString(bytes);
        }
    }
}
