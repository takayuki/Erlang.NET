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

namespace Erlang.NET
{
    public class OtpActorMbox : OtpMbox
    {
        protected readonly OtpActorSched sched;
        protected OtpActorSched.OtpActorSchedTask task;

        public OtpActorSched.OtpActorSchedTask Task
        {
            get { return task; }
            set { task = value; }
        }

        internal OtpActorMbox(OtpActorSched sched, OtpNode home, OtpErlangPid self, String name)
            : base(home, self, name)
        {
            this.sched = sched;
        }

        internal OtpActorMbox(OtpActorSched sched, OtpNode home, OtpErlangPid self)
            : base(home, self, null)
        {
            this.sched = sched;
        }

        public override void close()
        {
            base.close();
            sched.canncel(this);
        }

        public override OtpMsg receiveMsg()
        {
            Object m = Queue.tryGet();

            if (m == null)
            {
                return null;
            }
            else
            {
                return (OtpMsg)m;
            }
        }

        public override OtpMsg receiveMsg(long timeout)
        {
            Object m = Queue.get(timeout);

            if (m == null)
            {
                return null;
            }
            else
            {
                return (OtpMsg)m;
            }
        }

        public override void deliver(OtpMsg m)
        {
            base.deliver(m);
            sched.notify(this);
        }
    }
}
