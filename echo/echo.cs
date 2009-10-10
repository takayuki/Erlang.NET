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
using System.Reflection;
using System.Threading;
using Erlang.NET;
using log4net;
using log4net.Config;

namespace Erlang.NET.Test
{
    public class Echo
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        static Echo()
        {
            XmlConfigurator.Configure();
        }

        public class OtpEchoActor : OtpActor
        {
            public OtpEchoActor(OtpActorMbox mbox)
                : base(mbox)
            {
            }

            public override IEnumerator<Continuation> GetEnumerator()
            {
                OtpMbox mbox = base.Mbox;
                OtpMsg msg = null;

                while (true)
                {
                    yield return (delegate(OtpMsg m) { msg = m; });
                    log.Debug("-> ECHO " + msg.getMsg());
                    OtpErlangTuple t = (OtpErlangTuple)msg.getMsg();
                    OtpErlangPid sender = (OtpErlangPid)t.elementAt(0);
                    OtpErlangObject[] v = { mbox.Self, t.elementAt(1) };
                    mbox.send(sender, new OtpErlangTuple(v));
                }
            }
        }

        public static void Main(string[] args)
        {
            OtpNode a = new OtpNode("a");
            OtpNode b = new OtpNode("b");
            OtpActorMbox echo = (OtpActorMbox)b.createMbox("echo", false);
            b.react(new OtpEchoActor(echo));
            OtpMbox echoback = a.createMbox("echoback", true);
            OtpErlangObject[] v = { echoback.Self, new OtpErlangString("Hello, World!") };
            echoback.send(echo.Self, new OtpErlangTuple(v));
            log.Debug("<- ECHO (back) " + echoback.receive());
            b.close();
            a.close();
        }
    }
}
