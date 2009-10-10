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
    public class OtpErlangExternalFun : OtpErlangObject
    {
        // don't change this!
        internal static readonly new long serialVersionUID = 6443965570641913886L;

        private String module;
        private String function;
        private int arity;

        public OtpErlangExternalFun(String module, String function, int arity)
            : base()
        {
            this.module = module;
            this.function = function;
            this.arity = arity;
        }

        public OtpErlangExternalFun(OtpInputStream buf)
        {
            OtpErlangExternalFun f = buf.read_external_fun();
            module = f.module;
            function = f.function;
            arity = f.arity;
        }

        public override void encode(OtpOutputStream buf)
        {
            buf.write_external_fun(module, function, arity);
        }

        public override bool Equals(Object o)
        {
            if (!(o is OtpErlangExternalFun))
            {
                return false;
            }
            OtpErlangExternalFun f = (OtpErlangExternalFun)o;
            return module.Equals(f.module) && function.Equals(f.function) && arity == f.arity;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        protected override int doHashCode()
        {
            OtpErlangObject.Hash hash = new OtpErlangObject.Hash(14);
            hash.combine(module.GetHashCode(), function.GetHashCode());
            hash.combine(arity);
            return hash.valueOf();
        }

        public override String ToString()
        {
            return "#Fun<" + module + "." + function + "." + arity + ">";
        }
    }
}