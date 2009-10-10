/*
 * %CopyrightBegin%
 * 
 * Copyright Takayuki Usui 2009. All Rights Reserved.
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
using System.Collections.Generic;

namespace Erlang.NET
{
    public abstract class OtpActor
    {
        public delegate void Continuation(OtpMsg msg);

        private readonly OtpActorMbox mbox;

        public OtpActorMbox Mbox
        {
            get { return mbox; }
        }

        public OtpActorSched.OtpActorSchedTask Task
        {
            get { return mbox.Task; }
            set { mbox.Task = value; }
        }

        public OtpActor(OtpActorMbox mbox)
        {
            this.mbox = mbox;
        }

        public abstract IEnumerator<Continuation> GetEnumerator();
    }
}
