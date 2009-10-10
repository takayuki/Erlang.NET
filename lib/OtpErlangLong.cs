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

namespace Erlang.NET
{
    /**
     * Provides a Java representation of Erlang integral types. Erlang does not
     * distinguish between different integral types, however this class and its
     * subclasses {@link OtpErlangByte}, {@link OtpErlangChar},
     * {@link OtpErlangInt}, and {@link OtpErlangShort} attempt to map the Erlang
     * types onto the various Java integral types. Two additional classes,
     * {@link OtpErlangUInt} and {@link OtpErlangUShort} are provided for Corba
     * compatibility. See the documentation for IC for more information.
     */
    [Serializable]
    public class OtpErlangLong : OtpErlangObject
    {
        // don't change this!
        static internal readonly new long serialVersionUID = 1610466859236755096L;

        private long val;
        private BigInteger bigVal = null;

        /**
         * Create an Erlang integer from the given value.
         * 
         * @param l
         *                the long value to use.
         */
        public OtpErlangLong(long l)
        {
            val = l;
        }

        /**
         * Create an Erlang integer from the given value.
         * 
         * @param val
         *                the long value to use.
         */
        public OtpErlangLong(BigInteger v)
        {
            if (v == null)
            {
                throw new NullReferenceException();
            }
            if (v.bitCount() < 64)
            {
                val = v.LongValue();
            }
            else
            {
                bigVal = v;
            }
        }

        /**
         * Create an Erlang integer from a stream containing an integer encoded in
         * Erlang external format.
         * 
         * @param buf
         *                the stream containing the encoded value.
         * 
         * @exception OtpErlangDecodeException
         *                    if the buffer does not contain a valid external
         *                    representation of an Erlang integer.
         */
        public OtpErlangLong(OtpInputStream buf)
        {
            byte[] b = buf.read_integer_byte_array();

            try
            {
                val = OtpInputStream.byte_array_to_long(b, false);
            }
            catch (OtpErlangDecodeException)
            {
                bigVal = new BigInteger(b);
            }
        }

        private bool BigIntegerIsNull(BigInteger val)
        {
            return ((Object)val) == null;
        }

        /**
         * Get this number as a BigInteger.
         * 
         * @return the value of this number, as a BigInteger.
         */
        public BigInteger bigIntegerValue()
        {
            if (!BigIntegerIsNull(bigVal))
            {
                return bigVal;
            }
            else
            {
                return new BigInteger(val);
            }
        }

        /**
         * Get this number as a long, or rather truncate all but the least
         * significant 64 bits from the 2's complement representation of this number
         * and return them as a long.
         * 
         * @return the value of this number, as a long.
         */
        public long longValue()
        {
            if (!BigIntegerIsNull(bigVal))
            {
                return bigVal.LongValue();
            }
            else
            {
                return val;
            }
        }

        /**
         * Determine if this value can be represented as a long without truncation.
         * 
         * @return true if this value fits in a long, false otherwise.
         */
        public bool isLong()
        {
            // To just chech this.bigVal is a wee bit to simple, since
            // there just might have be a mean bignum that arrived on
            // a stream, and was a long disguised as more than 8 byte integer.
            if (!BigIntegerIsNull(bigVal))
            {
                return bigVal.bitCount() < 64;
            }
            return true;
        }

        /**
         * Determine if this value can be represented as an unsigned long without
         * truncation, that is if the value is non-negative and its bit pattern
         * completely fits in a long.
         * 
         * @return true if this value is non-negative and fits in a long false
         *         otherwise.
         */
        public bool isULong()
        {
            // Here we have the same problem as for isLong(), plus
            // the whole range 1<<63 .. (1<<64-1) is allowed.
            if (!BigIntegerIsNull(bigVal))
            {
                return bigVal >= ((BigInteger)0) && bigVal.bitCount() <= 64;
            }
            return val >= 0;
        }

        /**
         * Returns the number of bits in the minimal two's-complement representation
         * of this BigInteger, excluding a sign bit.
         * 
         * @return number of bits in the minimal two's-complement representation of
         *         this BigInteger, excluding a sign bit.
         */
        public int bitLength()
        {
            if (!BigIntegerIsNull(bigVal))
            {
                return bigVal.bitCount();
            }
            if (val == 0 || val == -1)
            {
                return 0;
            }
            else
            {
                // Binary search for bit length
                int i = 32; // mask length
                long m = (1L << i) - 1; // AND mask with ones in little end
                if (val < 0)
                {
                    m = ~m; // OR mask with ones in big end
                    for (int j = i >> 1; j > 0; j >>= 1) // mask delta
                    {
                        if ((val | m) == val) // mask >= enough
                        {
                            i -= j;
                            m >>= j; // try less bits
                        }
                        else
                        {
                            i += j;
                            m <<= j; // try more bits
                        }
                    }
                    if ((val | m) != val)
                    {
                        i++; // mask < enough
                    }
                }
                else
                {
                    for (int j = i >> 1; j > 0; j >>= 1) // mask delta
                    {
                        if ((val & m) == val) // mask >= enough
                        {
                            i -= j;
                            m >>= j; // try less bits
                        }
                        else
                        {
                            i += j;
                            m = m << j | m; // try more bits
                        }
                    }
                    if ((val & m) != val)
                    {
                        i++; // mask < enough
                    }
                }
                return i;
            }
        }

        /**
         * Return the signum function of this object.
         * 
         * @return -1, 0 or 1 as the value is negative, zero or positive.
         */
        public int signum()
        {
            if (!BigIntegerIsNull(bigVal))
            {
                return (bigVal > (BigInteger)0) ? 1 : (bigVal < (BigInteger)0) ? -1 : 0;
            }
            else
            {
                return val > 0 ? 1 : val < 0 ? -1 : 0;
            }
        }

        /**
         * Get this number as an int.
         * 
         * @return the value of this number, as an int.
         * 
         * @exception OtpErlangRangeException
         *                    if the value is too large to be represented as an int.
         */
        public int intValue()
        {
            long l = longValue();
            int i = (int)l;

            if (i != l)
            {
                throw new OtpErlangRangeException("Value too large for int: " + val);
            }

            return i;
        }

        /**
         * Get this number as a non-negative int.
         * 
         * @return the value of this number, as an int.
         * 
         * @exception OtpErlangRangeException
         *                    if the value is too large to be represented as an int,
         *                    or if the value is negative.
         */
        public int uIntValue()
        {
            long l = longValue();
            int i = (int)l;

            if (i != l)
            {
                throw new OtpErlangRangeException("Value too large for int: " + val);
            }
            else if (i < 0)
            {
                throw new OtpErlangRangeException("Value not positive: " + val);
            }

            return i;
        }

        /**
         * Get this number as a short.
         * 
         * @return the value of this number, as a short.
         * 
         * @exception OtpErlangRangeException
         *                    if the value is too large to be represented as a
         *                    short.
         */
        public short shortValue()
        {
            long l = longValue();
            short i = (short)l;

            if (i != l)
            {
                throw new OtpErlangRangeException("Value too large for short: " + val);
            }

            return i;
        }

        /**
         * Get this number as a non-negative short.
         * 
         * @return the value of this number, as a short.
         * 
         * @exception OtpErlangRangeException
         *                    if the value is too large to be represented as a
         *                    short, or if the value is negative.
         */
        public short uShortValue()
        {
            long l = longValue();
            short i = (short)l;

            if (i != l)
            {
                throw new OtpErlangRangeException("Value too large for short: " + val);
            }
            else if (i < 0)
            {
                throw new OtpErlangRangeException("Value not positive: " + val);
            }

            return i;
        }

        /**
         * Get this number as a char.
         * 
         * @return the char value of this number.
         * 
         * @exception OtpErlangRangeException
         *                    if the value is too large to be represented as a char.
         */
        public char charValue()
        {
            long l = longValue();
            char i = (char)l;

            if (i != l)
            {
                throw new OtpErlangRangeException("Value too large for char: " + val);
            }

            return i;
        }

        /**
         * Get this number as a byte.
         * 
         * @return the byte value of this number.
         * 
         * @exception OtpErlangRangeException
         *                    if the value is too large to be represented as a byte.
         */
        public byte byteValue()
        {
            long l = longValue();
            byte i = (byte)l;

            if (i != l)
            {
                throw new OtpErlangRangeException("Value too large for byte: " + val);
            }

            return i;
        }

        /**
         * Get the string representation of this number.
         * 
         * @return the string representation of this number.
         */
        public override String ToString()
        {
            if (!BigIntegerIsNull(bigVal))
            {
                return "" + bigVal;
            }
            else
            {
                return "" + val;
            }
        }

        /**
         * Convert this number to the equivalent Erlang external representation.
         * 
         * @param buf
         *                an output stream to which the encoded number should be
         *                written.
         */
        public override void encode(OtpOutputStream buf)
        {
            if (!BigIntegerIsNull(bigVal))
            {
                buf.write_big_integer(bigVal);
            }
            else
            {
                buf.write_long(val);
            }
        }

        /**
         * Determine if two numbers are equal. Numbers are equal if they contain the
         * same value.
         * 
         * @param o
         *                the number to compare to.
         * 
         * @return true if the numbers have the same value.
         */
        public override bool Equals(Object o)
        {
            if (!(o is OtpErlangLong))
            {
                return false;
            }

            OtpErlangLong that = (OtpErlangLong)o;

            if (!BigIntegerIsNull(bigVal) && !BigIntegerIsNull(that.bigVal))
            {
                return bigVal.Equals(that.bigVal);
            }
            else if (BigIntegerIsNull(bigVal) && BigIntegerIsNull(that.bigVal))
            {
                return val == that.val;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        protected override int doHashCode()
        {
            if (!BigIntegerIsNull(bigVal))
            {
                return bigVal.GetHashCode();
            }
            else
            {
                return ((BigInteger)val).GetHashCode();
            }
        }
    }
}
