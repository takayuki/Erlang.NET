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
using System.Threading;
using System.Reflection;
using log4net;
using log4net.Config;

namespace Erlang.NET
{
    public class OtpActorSched : ThreadBase
    {
	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

	private class OtpActorSchedTask
	{
	    private readonly OtpActor actor;
	    private readonly IEnumerator<OtpActor.Continuation> enumerator;
	    private volatile bool cancelled = false;

	    public bool IsCancelled
	    {
		get { return cancelled; }
		set { cancelled = value; }
	    }

	    public OtpActor Actor
	    {
		get { return actor; }
	    }

	    public IEnumerator<OtpActor.Continuation> Enumerator
	    {
		get { return enumerator; }
	    }

	    public OtpActorSchedTask(OtpActor actor)
	    {
		this.actor = actor;
		this.enumerator = actor.GetEnumerator();
	    }
	}

	private Dictionary<OtpAsyncMbox, OtpActorSchedTask> sleeping = new Dictionary<OtpAsyncMbox, OtpActorSchedTask>();
	private List<OtpActorSchedTask> runnable = new List<OtpActorSchedTask>();

	private volatile int tick = 0;

	public OtpActorSched() : base("OtpActorSched", true)
	{
	    base.start();
	}

	public void react(OtpActor actor)
	{
	    OtpActorSchedTask task = new OtpActorSchedTask(actor);

	    Monitor.Enter(runnable);
	    try
	    {
		runnable.Add(task);
		Monitor.Pulse(runnable);
	    }
	    finally
	    {
		Monitor.Exit(runnable);
	    }
	}

	public void canncel(OtpAsyncMbox mbox)
	{
	    Monitor.Enter(runnable);
	    try
	    {
		lock (sleeping)
		{
		    if (sleeping.ContainsKey(mbox))
		    {
			sleeping.Remove(mbox);
		    }
		}
		foreach (OtpActorSchedTask task in runnable)
		{
		    if (task.Actor.Mailbox == mbox)
		    {
			task.IsCancelled = true;
			break;
		    }
		}
	    }
	    finally
	    {
		Monitor.Exit(runnable);
	    }
	}

	public void notify(OtpAsyncMbox mbox)
	{
	    Monitor.Enter(runnable);
	    try
	    {
		lock (sleeping)
		{
		    if (sleeping.ContainsKey(mbox))
		    {
			runnable.Add(sleeping[mbox]);
			sleeping.Remove(mbox);
			Monitor.Pulse(runnable);
		    }
		}
	    }
	    finally
	    {
		Monitor.Exit(runnable);
	    }
	}

	public override void run()
	{
	    while (true)
	    {
		schedule();
	    }
	}

	private void schedule()
	{
	    Monitor.Enter(runnable);
	    try
	    {
		while (runnable.Count == 0)
		{
		    Monitor.Wait(runnable);
		}

		OtpActorSchedTask task = runnable[tick++ % runnable.Count];
		OtpActor actor = task.Actor;
		IEnumerator<OtpActor.Continuation> enumerator = task.Enumerator;
		
		if (task.IsCancelled)
		{
		    runnable.Remove(task);
		}
		else if (!actor.IsStarted)
		{
		    task.Actor.IsStarted = true;
		    if (!enumerator.MoveNext())
		    {
			runnable.Remove(task);
		    }
		}
		else
		{
		    OtpAsyncMbox mbox = actor.Mailbox;
		    GenericQueue queue = mbox.Queue;
		    OtpMsg msg = null;

		    lock (queue)
		    {
			if (queue.getCount() == 0)
			{
			    lock (sleeping)
			    {
				runnable.Remove(task);
				sleeping.Add(mbox, task);
			    }
			}
			else
			{
			    msg = (OtpMsg)queue.get();
			}
		    }

		    if (msg != null)
		    {
			OtpActor.Continuation cont = enumerator.Current;

			try
			{				
			    cont(msg);
			}
			catch (Exception e)
			{
			    log.Info("Exception was thrown from running actor: " + e.Message);
			}
			finally
			{
			    if (!enumerator.MoveNext())
			    {
				runnable.Remove(task);
			    }
			}
		    }
		}
	    }
	    finally
	    {
		Monitor.Exit(runnable);
	    }
	}
    }
}
