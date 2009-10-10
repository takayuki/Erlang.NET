/*
 * %CopyrightBegin%
 * 
 * Copyright Takayuki Usui 2009. All Rights Reserved.
 * Copyright Ericsson AB 2009. All Rights Reserved.
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
    [Serializable]
    public class OtpErlangFun : OtpErlangObject
    {
        // don't change this!
        internal static new long serialVersionUID = -3423031125356706472L;

        private readonly OtpErlangPid pid;
        private readonly String module;
        private readonly long index;
        private readonly long old_index;
        private readonly long uniq;
        private readonly OtpErlangObject[] freeVars;
        private readonly int arity;
        private readonly byte[] md5;

        public OtpErlangFun(OtpInputStream buf)
        {
            OtpErlangFun f = buf.read_fun();
            pid = f.pid;
            module = f.module;
            arity = f.arity;
            md5 = f.md5;
            index = f.index;
            old_index = f.old_index;
            uniq = f.uniq;
            freeVars = f.freeVars;
        }

        public OtpErlangFun(OtpErlangPid pid, String module,
                    long index, long uniq, OtpErlangObject[] freeVars)
        {
            this.pid = pid;
            this.module = module;
            arity = -1;
            md5 = null;
            this.index = index;
            old_index = 0;
            this.uniq = uniq;
            this.freeVars = freeVars;
        }

        public OtpErlangFun(OtpErlangPid pid, String module,
                    int arity, byte[] md5, int index,
                    long old_index, long uniq,
                    OtpErlangObject[] freeVars)
        {
            this.pid = pid;
            this.module = module;
            this.arity = arity;
            this.md5 = md5;
            this.index = index;
            this.old_index = old_index;
            this.uniq = uniq;
            this.freeVars = freeVars;
        }

        public override void encode(OtpOutputStream buf)
        {
            buf.write_fun(pid, module, old_index, arity, md5, index, uniq,
                  freeVars);
        }

        public override bool Equals(Object o)
        {
            if (!(o is OtpErlangFun))
            {
                return false;
            }
            OtpErlangFun f = (OtpErlangFun)o;
            if (!pid.Equals(f.pid) || !module.Equals(f.module) || arity != f.arity)
            {
                return false;
            }
            if (md5 == null)
            {
                if (f.md5 != null)
                {
                    return false;
                }
            }
            else
            {
                if (!md5.Equals(f.md5))
                {
                    return false;
                }
            }
            if (index != f.index || uniq != f.uniq)
            {
                return false;
            }
            if (freeVars == null)
            {
                return f.freeVars == null;
            }
            return freeVars.Equals(f.freeVars);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        protected override int doHashCode()
        {
            OtpErlangObject.Hash hash = new OtpErlangObject.Hash(1);
            hash.combine(pid.GetHashCode(), module.GetHashCode());
            hash.combine(arity);
            if (md5 != null) hash.combine(md5);
            hash.combine(index);
            hash.combine(uniq);
            if (freeVars != null)
            {
                foreach (OtpErlangObject o in freeVars)
                {
                    hash.combine(o.GetHashCode(), 1);
                }
            }
            return hash.valueOf();
        }

        public override String ToString()
        {
            return "#Fun<" + module + "." + old_index + "." + uniq + ">";
        }
    }
}
