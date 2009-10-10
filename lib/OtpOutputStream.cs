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
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Erlang.NET
{
    /**
     * Provides a stream for encoding Erlang terms to external format, for
     * transmission or storage.
     * 
     * <p>
     * Note that this class is not synchronized, if you need synchronization you
     * must provide it yourself.
     * 
     */
    public class OtpOutputStream : MemoryStream
    {
        /** The default initial size of the stream. * */
        public const int defaultInitialSize = 2048;

        /** The default increment used when growing the stream. * */
        public const int defaultIncrement = 2048;

        /**
         * Create a stream with the default initial size (2048 bytes).
         */
        public OtpOutputStream()
            : this(defaultInitialSize)
        {
        }

        /**
         * Create a stream with the specified initial size.
         */
        public OtpOutputStream(int size)
            : base(size)
        {
        }

        /**
         * Create a stream containing the encoded version of the given Erlang term.
         */
        public OtpOutputStream(OtpErlangObject o)
            : base()
        {
            write_any(o);
        }

        // package scope
        /*
         * Get the contents of the output stream as an input stream instead. This is
         * used internally in {@link OtpCconnection} for tracing outgoing packages.
         * 
         * @param offset where in the output stream to read data from when creating
         * the input stream. The offset is necessary because header contents start 5
         * bytes into the header buffer, whereas payload contents start at the
         * beginning
         * 
         * @return an input stream containing the same raw data.
         */
        internal OtpInputStream getOtpInputStream(int offset)
        {
            return new OtpInputStream(base.GetBuffer(), offset, ((int)base.Length) - offset, 0);
        }

        /**
         * Get the current position in the stream.
         * 
         * @return the current position in the stream.
         */
        public int getPos()
        {
            return (int)base.Position;
        }

        /**
         * Write one byte to the stream.
         * 
         * @param b
         *            the byte to write.
         * 
         */
        public void write(byte b)
        {
            base.WriteByte(b);
        }

        /**
         * Write an array of bytes to the stream.
         * 
         * @param buf
         *            the array of bytes to write.
         * 
         */
        public void write(byte[] buf)
        {
            base.Write(buf, 0, (int)buf.Length);
        }

        public override void WriteTo(Stream stream)
        {
            try
            {
                base.WriteTo(stream);
                stream.Flush();
            }
            catch (ObjectDisposedException e)
            {
                throw new IOException(e.Message);
            }
        }

        /**
         * Write the low byte of a value to the stream.
         * 
         * @param n
         *            the value to use.
         * 
         */
        public void write1(long n)
        {
            write((byte)(n & 0xff));
        }

        /**
         * Write an array of bytes to the stream.
         * 
         * @param buf
         *            the array of bytes to write.
         * 
         */
        public void writeN(byte[] bytes)
        {
            write(bytes);
        }

        /**
         * Get the current capacity of the stream. As bytes are added the capacity
         * of the stream is increased automatically, however this method returns the
         * current size.
         * 
         * @return the size of the internal buffer used by the stream.
         */
        public int length()
        {
            return base.Capacity;
        }

        /**
         * Get the number of bytes in the stream.
         * 
         * @return the number of bytes in the stream.
         * 
         * @deprecated As of Jinterface 1.4, replaced by super.size().
         * @see #size()
         */
        [Obsolete]
        public int count()
        {
            return (int)base.Length;
        }

        public int size()
        {
            return (int)base.Length;
        }

        /**
         * Write the low two bytes of a value to the stream in big endian order.
         * 
         * @param n
         *            the value to use.
         */
        public void write2BE(long n)
        {
            write((byte)((n & 0xff00) >> 8));
            write((byte)(n & 0xff));
        }

        /**
         * Write the low four bytes of a value to the stream in big endian order.
         * 
         * @param n
         *            the value to use.
         */
        public void write4BE(long n)
        {
            write((byte)((n & 0xff000000) >> 24));
            write((byte)((n & 0xff0000) >> 16));
            write((byte)((n & 0xff00) >> 8));
            write((byte)(n & 0xff));
        }

        /**
         * Write the low eight (all) bytes of a value to the stream in big endian
         * order.
         * 
         * @param n
         *            the value to use.
         */
        public void write8BE(long n)
        {
            write((byte)(n >> 56 & 0xff));
            write((byte)(n >> 48 & 0xff));
            write((byte)(n >> 40 & 0xff));
            write((byte)(n >> 32 & 0xff));
            write((byte)(n >> 24 & 0xff));
            write((byte)(n >> 16 & 0xff));
            write((byte)(n >> 8 & 0xff));
            write((byte)(n & 0xff));
        }

        /**
         * Write any number of bytes in little endian format.
         * 
         * @param n
         *            the value to use.
         * @param b
         *            the number of bytes to write from the little end.
         */
        public void writeLE(long n, int b)
        {
            for (int i = 0; i < b; i++)
            {
                write((byte)(n & 0xff));
                n >>= 8;
            }
        }

        /**
         * Write the low two bytes of a value to the stream in little endian order.
         * 
         * @param n
         *            the value to use.
         */
        public void write2LE(long n)
        {
            write((byte)(n & 0xff));
            write((byte)((n & 0xff00) >> 8));
        }

        /**
         * Write the low four bytes of a value to the stream in little endian order.
         * 
         * @param n
         *            the value to use.
         */
        public void write4LE(long n)
        {
            write((byte)(n & 0xff));
            write((byte)((n & 0xff00) >> 8));
            write((byte)((n & 0xff0000) >> 16));
            write((byte)((n & 0xff000000) >> 24));
        }

        /**
         * Write the low eight bytes of a value to the stream in little endian
         * order.
         * 
         * @param n
         *            the value to use.
         */
        public void write8LE(long n)
        {
            write((byte)(n & 0xff));
            write((byte)(n >> 8 & 0xff));
            write((byte)(n >> 16 & 0xff));
            write((byte)(n >> 24 & 0xff));
            write((byte)(n >> 32 & 0xff));
            write((byte)(n >> 40 & 0xff));
            write((byte)(n >> 48 & 0xff));
            write((byte)(n >> 56 & 0xff));
        }

        /**
         * Write the low four bytes of a value to the stream in bif endian order, at
         * the specified position. If the position specified is beyond the end of
         * the stream, this method will have no effect.
         * 
         * Normally this method should be used in conjunction with {@link #size()
         * size()}, when is is necessary to insert data into the stream before it is
         * known what the actual value should be. For example:
         * 
         * <pre>
         * int pos = s.size();
         *    s.write4BE(0); // make space for length data,
         *                   // but final value is not yet known
         *     [ ...more write statements...]
         *    // later... when we know the length value
         *    s.poke4BE(pos, length);
         * </pre>
         * 
         * 
         * @param offset
         *            the position in the stream.
         * @param n
         *            the value to use.
         */
        public void poke4BE(int offset, long n)
        {
            long cur = base.Position;

            base.Seek(offset, SeekOrigin.Begin);

            write((byte)((n & 0xff000000) >> 24));
            write((byte)((n & 0xff0000) >> 16));
            write((byte)((n & 0xff00) >> 8));
            write((byte)(n & 0xff));

            base.Seek(cur, SeekOrigin.Begin);
        }

        /**
         * Write a string to the stream as an Erlang atom.
         * 
         * @param atom
         *            the string to write.
         */
        public void write_atom(String atom)
        {
            byte[] bytes = Encoding.GetEncoding("iso-8859-1").GetBytes(atom);
            write1(OtpExternal.atomTag);
            write2BE(bytes.Length);
            writeN(bytes);
        }

        /**
         * Write an array of bytes to the stream as an Erlang binary.
         * 
         * @param bin
         *            the array of bytes to write.
         */
        public void write_binary(byte[] bin)
        {
            write1(OtpExternal.binTag);
            write4BE(bin.Length);
            writeN(bin);
        }

        /**
         * Write an array of bytes to the stream as an Erlang bitstr.
         * 
         * @param bin
         *            the array of bytes to write.
         * @param pad_bits
         *            the number of zero pad bits at the low end of the last byte
         */
        public void write_bitstr(byte[] bin, int pad_bits)
        {
            if (pad_bits == 0)
            {
                write_binary(bin);
                return;
            }
            write1(OtpExternal.bitBinTag);
            write4BE(bin.Length);
            write1(8 - pad_bits);
            writeN(bin);
        }

        /**
         * Write a boolean value to the stream as the Erlang atom 'true' or 'false'.
         * 
         * @param b
         *            the boolean value to write.
         */
        public void write_boolean(bool b)
        {
            write_atom(b.ToString());
        }

        /**
         * Write a single byte to the stream as an Erlang integer. The byte is
         * really an IDL 'octet', that is, unsigned.
         * 
         * @param b
         *            the byte to use.
         */
        public void write_byte(byte b)
        {
            this.write_long(b & 0xffL, true);
        }

        /**
         * Write a character to the stream as an Erlang integer. The character may
         * be a 16 bit character, kind of IDL 'wchar'. It is up to the Erlang side
         * to take care of souch, if they should be used.
         * 
         * @param c
         *            the character to use.
         */
        public void write_char(char c)
        {
            this.write_long(c & 0xffffL, true);
        }

        /**
         * Write a double value to the stream.
         * 
         * @param d
         *            the double to use.
         */
        public void write_double(double d)
        {
            write1(OtpExternal.newFloatTag);
            write8BE(BitConverter.ToInt64(BitConverter.GetBytes(d), 0));
        }

        /**
         * Write a float value to the stream.
         * 
         * @param f
         *            the float to use.
         */
        public void write_float(float f)
        {
            write_double(f);
        }

        public void write_big_integer(BigInteger v)
        {
            if (v.bitCount() < 64)
            {
                this.write_long(v.LongValue(), true);
                return;
            }
            int signum = (v > (BigInteger)0) ? 1 : (v < (BigInteger)0) ? -1 : 0;
            if (signum < 0)
            {
                v = -v;
            }
            byte[] magnitude = v.getBytes();
            int n = magnitude.Length;
            // Reverse the array to make it little endian.
            for (int i = 0, j = n; i < j--; i++)
            {
                // Swap [i] with [j]
                byte b = magnitude[i];
                magnitude[i] = magnitude[j];
                magnitude[j] = b;
            }
            if ((n & 0xFF) == n)
            {
                write1(OtpExternal.smallBigTag);
                write1(n); // length
            }
            else
            {
                write1(OtpExternal.largeBigTag);
                write4BE(n); // length
            }
            write1(signum < 0 ? 1 : 0); // sign
            // Write the array
            writeN(magnitude);
        }

        void write_long(long v, bool unsigned)
        {
            /*
             * If v<0 and unsigned==true the value
             * java.lang.Long.MAX_VALUE-java.lang.Long.MIN_VALUE+1+v is written, i.e
             * v is regarded as unsigned two's complement.
             */
            if ((v & 0xffL) == v)
            {
                // will fit in one byte
                write1(OtpExternal.smallIntTag);
                write1(v);
            }
            else
            {
                // note that v != 0L
                if (v < 0 && unsigned || v < OtpExternal.erlMin || v > OtpExternal.erlMax)
                {
                    // some kind of bignum
                    long abs = unsigned ? v : v < 0 ? -v : v;
                    int sign = unsigned ? 0 : v < 0 ? 1 : 0;
                    int n;
                    long mask;
                    for (mask = 0xFFFFffffL, n = 4; (abs & mask) != abs; n++, mask = mask << 8 | 0xffL)
                    {
                        ; // count nonzero bytes
                    }
                    write1(OtpExternal.smallBigTag);
                    write1(n); // length
                    write1(sign); // sign
                    writeLE(abs, n); // value. obs! little endian
                }
                else
                {
                    write1(OtpExternal.intTag);
                    write4BE(v);
                }
            }
        }

        /**
         * Write a long to the stream.
         * 
         * @param l
         *            the long to use.
         */
        public void write_long(long l)
        {
            this.write_long(l, false);
        }

        /**
         * Write a positive long to the stream. The long is interpreted as a two's
         * complement unsigned long even if it is negative.
         * 
         * @param ul
         *            the long to use.
         */
        public void write_ulong(long ul)
        {
            this.write_long(ul, true);
        }

        /**
         * Write an integer to the stream.
         * 
         * @param i
         *            the integer to use.
         */
        public void write_int(int i)
        {
            this.write_long(i, false);
        }

        /**
         * Write a positive integer to the stream. The integer is interpreted as a
         * two's complement unsigned integer even if it is negative.
         * 
         * @param ui
         *            the integer to use.
         */
        public void write_uint(int ui)
        {
            this.write_long(ui & 0xFFFFffffL, true);
        }

        /**
         * Write a short to the stream.
         * 
         * @param s
         *            the short to use.
         */
        public void write_short(short s)
        {
            this.write_long(s, false);
        }

        /**
         * Write a positive short to the stream. The short is interpreted as a two's
         * complement unsigned short even if it is negative.
         * 
         * @param s
         *            the short to use.
         */
        public void write_ushort(short us)
        {
            this.write_long(us & 0xffffL, true);
        }

        /**
         * Write an Erlang list header to the stream. After calling this method, you
         * must write 'arity' elements to the stream followed by nil, or it will not
         * be possible to decode it later.
         * 
         * @param arity
         *            the number of elements in the list.
         */
        public void write_list_head(int arity)
        {
            if (arity == 0)
            {
                write_nil();
            }
            else
            {
                write1(OtpExternal.listTag);
                write4BE(arity);
            }
        }

        /**
         * Write an empty Erlang list to the stream.
         */
        public void write_nil()
        {
            write1(OtpExternal.nilTag);
        }

        /**
         * Write an Erlang tuple header to the stream. After calling this method,
         * you must write 'arity' elements to the stream or it will not be possible
         * to decode it later.
         * 
         * @param arity
         *            the number of elements in the tuple.
         */
        public void write_tuple_head(int arity)
        {
            if (arity < 0xff)
            {
                write1(OtpExternal.smallTupleTag);
                write1(arity);
            }
            else
            {
                write1(OtpExternal.largeTupleTag);
                write4BE(arity);
            }
        }

        /**
         * Write an Erlang PID to the stream.
         * 
         * @param node
         *            the nodename.
         * 
         * @param id
         *            an arbitrary number. Only the low order 15 bits will be used.
         * 
         * @param serial
         *            another arbitrary number. Only the low order 13 bits will be
         *            used.
         * 
         * @param creation
         *            yet another arbitrary number. Only the low order 2 bits will
         *            be used.
         * 
         */
        public void write_pid(String node, int id, int serial, int creation)
        {
            write1(OtpExternal.pidTag);
            write_atom(node);
            write4BE(id & 0x7fff); // 15 bits
            write4BE(serial & 0x1fff); // 13 bits
            write1(creation & 0x3); // 2 bits
        }

        /**
         * Write an Erlang port to the stream.
         * 
         * @param node
         *            the nodename.
         * 
         * @param id
         *            an arbitrary number. Only the low order 28 bits will be used.
         * 
         * @param creation
         *            another arbitrary number. Only the low order 2 bits will be
         *            used.
         * 
         */
        public void write_port(String node, int id, int creation)
        {
            write1(OtpExternal.portTag);
            write_atom(node);
            write4BE(id & 0xfffffff); // 28 bits
            write1(creation & 0x3); // 2 bits
        }

        /**
         * Write an old style Erlang ref to the stream.
         * 
         * @param node
         *            the nodename.
         * 
         * @param id
         *            an arbitrary number. Only the low order 18 bits will be used.
         * 
         * @param creation
         *            another arbitrary number. Only the low order 2 bits will be
         *            used.
         * 
         */
        public void write_ref(String node, int id, int creation)
        {
            write1(OtpExternal.refTag);
            write_atom(node);
            write4BE(id & 0x3ffff); // 18 bits
            write1(creation & 0x3); // 2 bits
        }

        /**
         * Write a new style (R6 and later) Erlang ref to the stream.
         * 
         * @param node
         *            the nodename.
         * 
         * @param ids
         *            an array of arbitrary numbers. Only the low order 18 bits of
         *            the first number will be used. If the array contains only one
         *            number, an old style ref will be written instead. At most
         *            three numbers will be read from the array.
         * 
         * @param creation
         *            another arbitrary number. Only the low order 2 bits will be
         *            used.
         * 
         */
        public void write_ref(String node, int[] ids, int creation)
        {
            int arity = ids.Length;
            if (arity > 3)
            {
                arity = 3; // max 3 words in ref
            }

            if (arity == 1)
            {
                // use old method
                this.write_ref(node, ids[0], creation);
            }
            else
            {
                // r6 ref
                write1(OtpExternal.newRefTag);

                // how many id values
                write2BE(arity);

                write_atom(node);

                // note: creation BEFORE id in r6 ref
                write1(creation & 0x3); // 2 bits

                // first int gets truncated to 18 bits
                write4BE(ids[0] & 0x3ffff);

                // remaining ones are left as is
                for (int i = 1; i < arity; i++)
                {
                    write4BE(ids[i]);
                }
            }
        }

        /**
         * Write a string to the stream.
         * 
         * @param s
         *            the string to write.
         */
        public void write_string(String s)
        {
            int len = s.Length;

            switch (len)
            {
                case 0:
                    write_nil();
                    break;
                default:
                    if (len <= 65535 && is8bitString(s)) // 8-bit string
                    {
                        try
                        {
                            byte[] bytebuf = Encoding.GetEncoding("iso-8859-1").GetBytes(s);
                            write1(OtpExternal.stringTag);
                            write2BE(len);
                            writeN(bytebuf);
                        }
                        catch (EncoderFallbackException)
                        {
                            write_nil(); // it should never ever get here...
                        }
                    }
                    else // unicode or longer, must code as list
                    {
                        int[] codePoints = OtpErlangString.stringToCodePoints(s);
                        write_list_head(codePoints.Length);
                        foreach (int codePoint in codePoints)
                        {
                            write_int(codePoint);
                        }
                        write_nil();
                    }
                    break;
            }
        }

        private bool is8bitString(String s)
        {
            for (int i = 0; i < s.Length; ++i)
            {
                char c = s[i];
                if (c < 0 || c > 255)
                {
                    return false;
                }
            }
            return true;
        }

        /**
         * Write an arbitrary Erlang term to the stream in compressed format.
         * 
         * @param o
         *            the Erlang tem to write.
         */
        public void write_compressed(OtpErlangObject o)
        {
            OtpOutputStream oos = new OtpOutputStream(o);
            write1(OtpExternal.compressedTag);
            write4BE(oos.Length);
            DeflateStream dos = new DeflateStream(this, CompressionMode.Compress, true);
            try
            {
                oos.WriteTo(dos);
                dos.Close();
            }
            catch (ObjectDisposedException)
            {
                throw new ArgumentException("Intremediate stream failed for Erlang object " + o);
            }
        }

        /**
         * Write an arbitrary Erlang term to the stream.
         * 
         * @param o
         *            the Erlang term to write.
         */
        public void write_any(OtpErlangObject o)
        {
            // calls one of the above functions, depending on o
            o.encode(this);
        }

        public void write_fun(OtpErlangPid pid, String module,
                      long old_index, int arity, byte[] md5,
                      long index, long uniq, OtpErlangObject[] freeVars)
        {
            if (arity == -1)
            {
                write1(OtpExternal.funTag);
                write4BE(freeVars.Length);
                pid.encode(this);
                write_atom(module);
                write_long(index);
                write_long(uniq);
                foreach (OtpErlangObject fv in freeVars)
                {
                    fv.encode(this);
                }
            }
            else
            {
                write1(OtpExternal.newFunTag);
                int saveSizePos = getPos();
                write4BE(0); // this is where we patch in the size
                write1(arity);
                writeN(md5);
                write4BE(index);
                write4BE(freeVars.Length);
                write_atom(module);
                write_long(old_index);
                write_long(uniq);
                pid.encode(this);
                foreach (OtpErlangObject fv in freeVars)
                {
                    fv.encode(this);
                }
                poke4BE(saveSizePos, getPos() - saveSizePos);
            }
        }

        public void write_external_fun(String module, String function, int arity)
        {
            write1(OtpExternal.externalFunTag);
            write_atom(module);
            write_atom(function);
            write_long(arity);
        }
    }
}
