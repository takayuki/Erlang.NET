/*
 * %CopyrightBegin%
 * 
 * Copyright Takayuki Usui 2009. All Rights Reserved.
 * Copyright Ericsson AB 2007-2009. All Rights Reserved.
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
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;

namespace Erlang.NET
{
    /**
     * Provides a Java representation of Erlang bitstrs. An Erlang bitstr is an
     * Erlang binary with a length not an integral number of bytes (8-bit). Anything
     * can be represented as a sequence of bytes can be made into an Erlang bitstr.
     */
    [Serializable]
    public class OtpErlangBitstr : OtpErlangObject
    {
        // don't change this!
        internal static readonly new long serialVersionUID = -3781009633593609217L;

        protected byte[] bin;
        protected int pad_bits;

        /**
         * Create a bitstr from a byte array
         * 
         * @param bin
         *                the array of bytes from which to create the bitstr.
         */
        public OtpErlangBitstr(byte[] bin)
        {
            this.bin = new byte[bin.Length];
            Array.Copy(bin, 0, this.bin, 0, bin.Length);
            pad_bits = 0;
        }

        /**
         * Create a bitstr with pad bits from a byte array.
         * 
         * @param bin
         *                the array of bytes from which to create the bitstr.
         * @param pad_bits
         *                the number of unused bits in the low end of the last byte.
         */
        public OtpErlangBitstr(byte[] bin, int pad_bits)
        {
            this.bin = new byte[bin.Length];
            Array.Copy(bin, 0, this.bin, 0, bin.Length);
            this.pad_bits = pad_bits;

            check_bitstr(this.bin, this.pad_bits);
        }

        private void check_bitstr(byte[] bin, int pad_bits)
        {
            if (pad_bits < 0 || 7 < pad_bits)
            {
                throw new ArgumentException("Padding must be in range 0..7");
            }

            if (pad_bits != 0 && bin.Length == 0)
            {
                throw new ArgumentException("Padding on zero length bitstr");
            }

            if (bin.Length != 0)
            {
                // Make sure padding is zero
                bin[bin.Length - 1] &= (byte)~((1 << pad_bits) - 1);
            }
        }

        /**
         * Create a bitstr from a stream containing a bitstr encoded in Erlang
         * external format.
         * 
         * @param buf
         *                the stream containing the encoded bitstr.
         * 
         * @exception OtpErlangDecodeException
         *                    if the buffer does not contain a valid external
         *                    representation of an Erlang bitstr.
         */
        public OtpErlangBitstr(OtpInputStream buf)
        {
            bin = buf.read_bitstr(out pad_bits);

            check_bitstr(bin, pad_bits);
        }

        /**
         * Create a bitstr from an arbitrary Java Object. The object must implement
         * java.io.Serializable or java.io.Externalizable.
         * 
         * @param o
         *                the object to serialize and create this bitstr from.
         */
        public OtpErlangBitstr(Object o)
        {
            try
            {
                bin = toByteArray(o);
                pad_bits = 0;
            }
            catch (SerializationException)
            {
                throw new ArgumentException("Object must implement Serializable");
            }
        }

        private static byte[] toByteArray(Object o)
        {
            if (o == null)
            {
                return null;
            }

            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream();

            try
            {
                bf.Serialize(ms, o);
                ms.Flush();
                ms.Position = 0;

                return ms.ToArray();
            }
            finally
            {
                ms.Close();
            }
        }

        private static Object fromByteArray(byte[] buf)
        {
            if (buf == null)
            {
                return null;
            }

            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream(buf);

            try
            {
                return bf.Deserialize(ms);
            }
            catch (SerializationException)
            {
            }

            return null;
        }

        /**
         * Get the byte array from a bitstr, padded with zero bits in the little end
         * of the last byte.
         * 
         * @return the byte array containing the bytes for this bitstr.
         */
        public byte[] binaryValue()
        {
            return bin;
        }

        /**
         * Get the size in whole bytes of the bitstr, rest bits in the last byte not
         * counted.
         * 
         * @return the number of bytes contained in the bintstr.
         */
        public int size()
        {
            if (pad_bits == 0)
            {
                return bin.Length;
            }
            if (bin.Length == 0)
            {
                throw new SystemException("Impossible length");
            }
            return bin.Length - 1;
        }

        /**
         * Get the number of pad bits in the last byte of the bitstr. The pad bits
         * are zero and in the little end.
         * 
         * @return the number of pad bits in the bitstr.
         */
        public int PadBits
        {
            get { return pad_bits; }
        }

        /**
         * Get the java Object from the bitstr. If the bitstr contains a serialized
         * Java object, then this method will recreate the object.
         * 
         * 
         * @return the java Object represented by this bitstr, or null if the bitstr
         *         does not represent a Java Object.
         */
        public Object getObject()
        {
            if (pad_bits != 0)
            {
                return null;
            }
            return fromByteArray(bin);
        }

        /**
         * Get the string representation of this bitstr object. A bitstr is printed
         * as #Bin&lt;N&gt;, where N is the number of bytes contained in the object
         * or #bin&lt;N-M&gt; if there are M pad bits.
         * 
         * @return the Erlang string representation of this bitstr.
         */
        public override String ToString()
        {
            if (pad_bits == 0)
            {
                return "#Bin<" + bin.Length + ">";
            }
            if (bin.Length == 0)
            {
                throw new SystemException("Impossible length");
            }
            return "#Bin<" + bin.Length + "-" + pad_bits + ">";
        }

        /**
         * Convert this bitstr to the equivalent Erlang external representation.
         * 
         * @param buf
         *                an output stream to which the encoded bitstr should be
         *                written.
         */
        public override void encode(OtpOutputStream buf)
        {
            buf.write_bitstr(bin, pad_bits);
        }

        /**
         * Determine if two bitstrs are equal. Bitstrs are equal if they have the
         * same byte length and tail length, and the array of bytes is identical.
         * 
         * @param o
         *                the bitstr to compare to.
         * 
         * @return true if the bitstrs contain the same bits, false otherwise.
         */
        public override bool Equals(Object o)
        {
            if (!(o is OtpErlangBitstr))
            {
                return false;
            }

            OtpErlangBitstr that = (OtpErlangBitstr)o;
            if (pad_bits != that.pad_bits)
            {
                return false;
            }

            int len = bin.Length;
            if (len != that.bin.Length)
            {
                return false;
            }

            for (int i = 0; i < len; i++)
            {
                if (bin[i] != that.bin[i])
                {
                    return false; // early exit
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        protected override int doHashCode()
        {
            OtpErlangObject.Hash hash = new OtpErlangObject.Hash(15);
            hash.combine(bin);
            hash.combine(pad_bits);
            return hash.valueOf();
        }

        public override Object Clone()
        {
            OtpErlangBitstr that = (OtpErlangBitstr)base.Clone();
            that.bin = new byte[bin.Length];
            Array.Copy(bin, 0, that.bin, 0, bin.Length);
            that.pad_bits = pad_bits;
            return that;
        }
    }
}
