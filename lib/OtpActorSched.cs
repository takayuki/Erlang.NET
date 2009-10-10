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

        public class OtpActorSchedTask
        {
            private readonly OtpActor actor;
            private readonly IEnumerator<OtpActor.Continuation> enumerator;
            private volatile bool active = false;

            public bool Active
            {
                get { return active; }
                set { active = value; }
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
                actor.Task = this;
                this.actor = actor;
                this.enumerator = actor.GetEnumerator();
            }
        }

        private Queue<OtpActorSchedTask> runnable = new Queue<OtpActorSchedTask>();

        public OtpActorSched()
            : base("OtpActorSched", true)
        {
            base.start();
        }

        public void react(OtpActor actor)
        {
            OtpActorSchedTask task = new OtpActorSchedTask(actor);
            IEnumerator<OtpActor.Continuation> enumerator = task.Enumerator;

            if (!enumerator.MoveNext())
            {
                task.Active = false;
            }
            else
            {
                Monitor.Enter(runnable);
                try
                {
                    task.Active = true;
                    runnable.Enqueue(task);
                    if (runnable.Count == 1)
                    {
                        Monitor.Pulse(runnable);
                    }
                }
                finally
                {
                    Monitor.Exit(runnable);
                }
            }
        }

        public void canncel(OtpActorMbox mbox)
        {
            OtpActorSchedTask task = mbox.Task;

            Monitor.Enter(runnable);
            try
            {
                lock (task)
                {
                    task.Active = false;
                }
            }
            finally
            {
                Monitor.Exit(runnable);
            }
        }

        public void notify(OtpActorMbox mbox)
        {
            OtpActorSchedTask task = mbox.Task;

            Monitor.Enter(runnable);
            try
            {
                Monitor.Enter(task);
                try
                {
                    if (mbox.Task.Active)
                    {
                        runnable.Enqueue(mbox.Task);
                        if (runnable.Count == 1)
                        {
                            Monitor.Pulse(runnable);
                        }
                    }
                }
                finally
                {
                    Monitor.Exit(task);
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

                OtpActorSchedTask task = runnable.Dequeue();
                OtpActor actor = task.Actor;
                IEnumerator<OtpActor.Continuation> enumerator = task.Enumerator;

                Monitor.Enter(task);
                try
                {
                    if (task.Active)
                    {
                        OtpMsg msg = actor.Mbox.receiveMsg();

                        if (msg == null)
                        {
                            return;
                        }

                        ThreadPool.QueueUserWorkItem
                            (delegate(Object state)
                             {
                                 Monitor.Enter(task);
                                 try
                                 {
                                     OtpActor.Continuation cont = enumerator.Current;

                                     cont(msg);

                                     if (!enumerator.MoveNext())
                                     {
                                         task.Active = false;
                                     }
                                 }
                                 catch (Exception e)
                                 {
                                     log.Info("Exception was thrown from running actor: " + e.Message);
                                 }
                                 finally
                                 {
                                     Monitor.Exit(task);
                                 }
                             });
                    }
                }
                finally
                {
                    Monitor.Exit(task);
                }
            }
            finally
            {
                Monitor.Exit(runnable);
            }
        }
    }
}
