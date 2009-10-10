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
using Erlang.NET;
using log4net;
using log4net.Config;

namespace Erlang.NET.Test
{
    public class Ping
    {
        static Ping()
        {
            XmlConfigurator.Configure();
        }

        public static void Main(string[] args)
        {
            OtpNode pingNode = new OtpNode("ping");
            OtpNode pongNode = new OtpNode("pong");
            bool ok = pingNode.ping("pong", 10000);
            pingNode.close();
            pongNode.close();
            Environment.Exit(ok ? 0 : 1);
        }
    }
}
