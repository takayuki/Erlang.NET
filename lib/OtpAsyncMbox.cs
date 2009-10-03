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
    public class OtpAsyncMbox : OtpMbox
    {
	protected readonly OtpActorSched sched;

	public new GenericQueue Queue
	{
	    get { return base.Queue; }
	}

	internal OtpAsyncMbox(OtpActorSched sched, OtpNode home, OtpErlangPid self, String name)
	    : base(home, self, name)
	{
	    this.sched = sched;
	}

	internal OtpAsyncMbox(OtpActorSched sched, OtpNode home, OtpErlangPid self)
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
	    throw new NotSupportedException();
	}

	public override OtpMsg receiveMsg(long timeout)
	{
	    throw new NotSupportedException();
	}

	public override void deliver(OtpMsg m)
	{
	    base.deliver(m);
	    sched.notify(this);
	}
    }
}
