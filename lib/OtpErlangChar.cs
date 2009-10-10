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
     * Provides a Java representation of Erlang integral types.
     */
    [Serializable]
    public class OtpErlangChar : OtpErlangLong
    {
        // don't change this!
        internal static readonly new long serialVersionUID = 3225337815669398204L;

        /**
         * Create an Erlang integer from the given value.
         * 
         * @param c
         *                the char value to use.
         */
        public OtpErlangChar(char c)
            : base(c)
        {
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
         * 
         * @exception OtpErlangRangeException
         *                    if the value is too large to be represented as a char.
         */
        public OtpErlangChar(OtpInputStream buf)
            : base(buf)
        {
        }
    }
}
