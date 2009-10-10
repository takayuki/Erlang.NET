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
    // package scope
    public class Link
    {
        private OtpErlangPid local;
        private OtpErlangPid remote;
        private int hashCodeValue = 0;

        public Link(OtpErlangPid local, OtpErlangPid remote)
        {
            this.local = local;
            this.remote = remote;
        }

        public OtpErlangPid Local
        {
            get { return local; }
        }

        public OtpErlangPid Remote
        {
            get { return remote; }
        }

        public bool contains(OtpErlangPid pid)
        {
            return local.Equals(pid) || remote.Equals(pid);
        }

        public bool equals(OtpErlangPid local, OtpErlangPid remote)
        {
            return this.local.Equals(local) && this.remote.Equals(remote)
            || this.local.Equals(remote) && this.remote.Equals(local);
        }

        public override int GetHashCode()
        {
            if (hashCodeValue == 0)
            {
                OtpErlangObject.Hash hash = new OtpErlangObject.Hash(5);
                hash.combine(local.GetHashCode() + remote.GetHashCode());
                hashCodeValue = hash.valueOf();
            }
            return hashCodeValue;
        }
    }
}
