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
     * Base class of the Erlang data type classes. This class is used to represent
     * an arbitrary Erlang term.
     */
    [Serializable]
    public abstract class OtpErlangObject : ICloneable
    {
        protected int hashCodeValue = 0;

        // don't change this!
        internal static readonly long serialVersionUID = -8435938572339430044L;

        /**
         * @return the printable representation of the object. This is usually
         *         similar to the representation used by Erlang for the same type of
         *         object.
         */
        public abstract override String ToString();

        /**
         * Convert the object according to the rules of the Erlang external format.
         * This is mainly used for sending Erlang terms in messages, however it can
         * also be used for storing terms to disk.
         * 
         * @param buf
         *                an output stream to which the encoded term should be
         *                written.
         */
        public abstract void encode(OtpOutputStream buf);

        /**
         * Read binary data in the Erlang external format, and produce a
         * corresponding Erlang data type object. This method is normally used when
         * Erlang terms are received in messages, however it can also be used for
         * reading terms from disk.
         * 
         * @param buf
         *                an input stream containing one or more encoded Erlang
         *                terms.
         * 
         * @return an object representing one of the Erlang data types.
         * 
         * @exception OtpErlangDecodeException
         *                    if the stream does not contain a valid representation
         *                    of an Erlang term.
         */
        public static OtpErlangObject decode(OtpInputStream buf)
        {
            return buf.read_any();
        }

        /**
         * Determine if two Erlang objects are equal. In general, Erlang objects are
         * equal if the components they consist of are equal.
         * 
         * @param o
         *                the object to compare to.
         * 
         * @return true if the objects are identical.
         */

        public abstract override bool Equals(Object o);

        public override int GetHashCode()
        {
            if (hashCodeValue == 0)
            {
                hashCodeValue = doHashCode();
            }
            return hashCodeValue;
        }

        protected virtual int doHashCode()
        {
            return base.GetHashCode();
        }

        public virtual Object Clone()
        {
            return base.MemberwiseClone();
        }

        internal class Hash
        {
            uint[] abc = { 0, 0, 0 };

            /* Hash function suggested by Bob Jenkins.
             * The same as in the Erlang VM (beam); utils.c.
             */

            private readonly static uint[] HASH_CONST = {
		0, // not used
		0x9e3779b9, // the golden ratio; an arbitrary value
		0x3c6ef372, // (hashHConst[1] * 2) % (1<<32)
		0xdaa66d2b, //             1    3
		0x78dde6e4, //             1    4
		0x1715609d, //             1    5
		0xb54cda56, //             1    6
		0x5384540f, //             1    7
		0xf1bbcdc8, //             1    8
		0x8ff34781, //             1    9
		0x2e2ac13a, //             1    10
		0xcc623af3, //             1    11
		0x6a99b4ac, //             1    12
		0x08d12e65, //             1    13
		0xa708a81e, //             1    14
		0x454021d7, //             1    15
	    };

            public Hash(int i)
            {
                abc[0] = abc[1] = HASH_CONST[i];
                abc[2] = 0;
            }

            //protected Hash() {
            //    Hash(1);
            //}

            private void mix()
            {
                abc[0] -= abc[1]; abc[0] -= abc[2]; abc[0] ^= (abc[2] >> 13);
                abc[1] -= abc[2]; abc[1] -= abc[0]; abc[1] ^= (abc[0] << 8);
                abc[2] -= abc[0]; abc[2] -= abc[1]; abc[2] ^= (abc[1] >> 13);
                abc[0] -= abc[1]; abc[0] -= abc[2]; abc[0] ^= (abc[2] >> 12);
                abc[1] -= abc[2]; abc[1] -= abc[0]; abc[1] ^= (abc[0] << 16);
                abc[2] -= abc[0]; abc[2] -= abc[1]; abc[2] ^= (abc[1] >> 5);
                abc[0] -= abc[1]; abc[0] -= abc[2]; abc[0] ^= (abc[2] >> 3);
                abc[1] -= abc[2]; abc[1] -= abc[0]; abc[1] ^= (abc[0] << 10);
                abc[2] -= abc[0]; abc[2] -= abc[1]; abc[2] ^= (abc[1] >> 15);
            }

            public void combine(int a)
            {
                abc[0] += (uint)a;
                mix();
            }

            public void combine(long a)
            {
                combine((int)(((uint)a) >> 32), (int)a);
            }

            public void combine(int a, int b)
            {
                abc[0] += (uint)a;
                abc[1] += (uint)b;
                mix();
            }

            public void combine(byte[] b)
            {
                int j, k;
                for (j = 0, k = 0;
                     j + 4 < b.Length;
                     j += 4, k += 1, k %= 3)
                {
                    abc[k] += ((uint)b[j + 0] & 0xFF) + ((uint)b[j + 1] << 8 & 0xFF00)
                    + ((uint)b[j + 2] << 16 & 0xFF0000) + ((uint)b[j + 3] << 24);
                    mix();
                }
                for (int n = 0, m = 0xFF;
                     j < b.Length;
                     j++, n += 8, m <<= 8)
                {
                    abc[k] += (uint)(b[j] << n & m);
                }
                mix();
            }

            public int valueOf()
            {
                return (int)abc[2];
            }
        }
    }
}
