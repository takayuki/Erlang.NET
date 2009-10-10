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
     * Exception raised when a communication channel is broken. This can be caused
     * for a number of reasons, for example:
     * 
     * <ul>
     * <li> an error in communication has occurred
     * <li> a remote process has sent an exit signal
     * <li> a linked process has exited
     * </ul>
     * 
     * @see OtpConnection
     */
    public class OtpErlangExit : OtpErlangException
    {
        internal static readonly new long serialVersionUID = 1L;

        OtpErlangObject reason = null;
        OtpErlangPid pid = null;

        /**
         * Create an OtpErlangExit exception with the given reason.
         * 
         * @param reason
         *                the reason this exit signal has been sent.
         */
        public OtpErlangExit(OtpErlangObject reason)
            : base(reason.ToString())
        {
            this.reason = reason;
        }

        /**
         * <p>
         * Equivalent to <code>OtpErlangExit(new
         * OtpErlangAtom(reason)</code>.
         * </p>
         * 
         * @param reason
         *                the reason this exit signal has been sent.
         * 
         * @see #OtpErlangExit(OtpErlangObject)
         */
        public OtpErlangExit(String reason)
            : this(new OtpErlangAtom(reason))
        {
        }

        /**
         * Create an OtpErlangExit exception with the given reason and sender pid.
         * 
         * @param reason
         *                the reason this exit signal has been sent.
         * 
         * @param pid
         *                the pid that sent this exit.
         */
        public OtpErlangExit(OtpErlangObject reason, OtpErlangPid pid)
            : base(reason.ToString())
        {
            this.reason = reason;
            this.pid = pid;
        }

        /**
         * <p>
         * Equivalent to <code>OtpErlangExit(new OtpErlangAtom(reason),
         * pid)</code>.
         * </p>
         * 
         * @param reason
         *                the reason this exit signal has been sent.
         * 
         * @param pid
         *                the pid that sent this exit.
         * 
         * @see #OtpErlangExit(OtpErlangObject, OtpErlangPid)
         */
        public OtpErlangExit(String reason, OtpErlangPid pid)
            : this(new OtpErlangAtom(reason), pid)
        {
        }

        /**
         * Get the reason associated with this exit signal.
         */
        public OtpErlangObject Reason
        {
            get { return reason; }
        }

        /**
         * Get the pid that sent this exit.
         */
        public OtpErlangPid Pid
        {
            get { return pid; }
        }
    }
}
