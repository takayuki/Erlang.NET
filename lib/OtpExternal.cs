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

namespace Erlang.NET
{
    /**
     * Provides a collection of constants used when encoding and decoding Erlang
     * terms.
     */
    public class OtpExternal
    {
        // no constructor
        private OtpExternal()
        {
        }

        /** The tag used for small integers */
        public const int smallIntTag = 97;

        /** The tag used for integers */
        public const int intTag = 98;

        /** The tag used for floating point numbers */
        public const int floatTag = 99;
        public const int newFloatTag = 70;

        /** The tag used for atoms */
        public const int atomTag = 100;

        /** The tag used for old stype references */
        public const int refTag = 101;

        /** The tag used for ports */
        public const int portTag = 102;

        /** The tag used for PIDs */
        public const int pidTag = 103;

        /** The tag used for small tuples */
        public const int smallTupleTag = 104;

        /** The tag used for large tuples */
        public const int largeTupleTag = 105;

        /** The tag used for empty lists */
        public const int nilTag = 106;

        /** The tag used for strings and lists of small integers */
        public const int stringTag = 107;

        /** The tag used for non-empty lists */
        public const int listTag = 108;

        /** The tag used for binaries */
        public const int binTag = 109;

        /** The tag used for bitstrs */
        public const int bitBinTag = 77;

        /** The tag used for small bignums */
        public const int smallBigTag = 110;

        /** The tag used for large bignums */
        public const int largeBigTag = 111;

        /** The tag used for old new Funs */
        public const int newFunTag = 112;

        /** The tag used for external Funs (M:F/A) */
        public const int externalFunTag = 113;

        /** The tag used for new style references */
        public const int newRefTag = 114;

        /** The tag used for old Funs */
        public const int funTag = 117;

        /** The tag used for compressed terms */
        public const int compressedTag = 80;

        /** The version number used to mark serialized Erlang terms */
        public const int versionTag = 131;

        /** The largest value that can be encoded as an integer */
        public const int erlMax = (1 << 27) - 1;

        /** The smallest value that can be encoded as an integer */
        public const int erlMin = -(1 << 27);

        /** The longest allowed Erlang atom */
        public const int maxAtomLength = 255;
    }
}
