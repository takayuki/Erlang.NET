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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using log4net;

namespace Erlang.NET
{
    public partial class OtpEpmd : ThreadBase
    {
        /**
         * Provides an preliminary implementation of epmd in C#
         */
        public class OtpPublishedNode : AbstractNode
        {
            private int port;

            public int Port
            {
                get { return port; }
                set { port = value; }
            }

            public OtpPublishedNode(String node)
                : base(node, String.Empty)
            {
            }
        }

        private readonly TcpListener sock;
        private readonly Dictionary<string, OtpPublishedNode> portmap = new Dictionary<string, OtpPublishedNode>();
        private int creation = 0;
        private volatile bool done = false;

        public Dictionary<string, OtpPublishedNode> Portmap
        {
            get { return portmap; }
        }

        private int Creation
        {
            get
            {
                lock (this)
                {
                    int next = (creation % 3) + 1;
                    creation++;
                    return next;
                }
            }
        }

        public OtpEpmd()
            : base("OtpEpmd", true)
        {
            sock = new TcpListener(new IPEndPoint(IPAddress.Any, EpmdPort.get()));
        }

        public override void start()
        {
            sock.Start();
            base.start();
        }

        private void closeSock(TcpListener s)
        {
            try
            {
                if (s != null)
                {
                    s.Stop();
                }
            }
            catch (Exception)
            {
            }
        }

        public void quit()
        {
            done = true;
            closeSock(sock);
        }

        public override void run()
        {
            log.InfoFormat("[OtpEpmd] start at port {0}", EpmdPort.get());

            while (!done)
            {
                try
                {
                    TcpClient newsock = sock.AcceptTcpClient();
                    OtpEpmdConnection conn = new OtpEpmdConnection(this, newsock);
                    conn.start();
                }
                catch (Exception)
                {
                }
            }
            return;
        }

        private class OtpEpmdConnection : ThreadBase
        {
            private readonly OtpEpmd epmd;
            private readonly Dictionary<string, OtpPublishedNode> portmap;
            private readonly List<string> publishedPort = new List<string>();
            private readonly TcpClient sock;
            private volatile bool done = false;

            public OtpEpmdConnection(OtpEpmd epmd, TcpClient sock)
                : base("OtpEpmd.OtpEpmdConnection", true)
            {
                this.epmd = epmd;
                this.portmap = epmd.Portmap;
                this.sock = sock;
            }

            private void closeSock(TcpClient s)
            {
                try
                {
                    if (s != null)
                    {
                        s.GetStream().Close();
                        s.Close();
                    }
                }
                catch (Exception)
                {
                }
            }

            private int readSock(TcpClient s, byte[] b)
            {
                int got = 0;
                int len = b.Length;
                int i;
                Stream st = s.GetStream();

                while (got < len)
                {
                    try
                    {
                        i = st.Read(b, got, len - got);
                    }
                    catch (IOException e)
                    {
                        throw new IOException(e.Message);
                    }
                    catch (ObjectDisposedException e)
                    {
                        throw new IOException(e.Message);
                    }

                    if (i < 0)
                    {
                        throw new IOException("expected " + len + " bytes, got EOF after " + got + " bytes");
                    }
                    else if (i == 0 && len != 0)
                    {
                        throw new IOException("Remote connection closed");
                    }
                    else
                    {
                        got += i;
                    }
                }
                return got;
            }

            private void quit()
            {
                done = true;
                closeSock(sock);

                foreach (string name in publishedPort)
                {
                    lock (portmap)
                    {
                        if (portmap.ContainsKey(name))
                        {
                            portmap.Remove(name);
                        }
                    }
                }
                publishedPort.Clear();
            }

            private void r4_publish(TcpClient s, OtpInputStream ibuf)
            {
                try
                {
                    int port = ibuf.read2BE();
                    int type = ibuf.read1();
                    int proto = ibuf.read1();
                    int distHigh = ibuf.read2BE();
                    int distLow = ibuf.read2BE();
                    int len = ibuf.read2BE();
                    byte[] alive = new byte[len];
                    ibuf.readN(alive);
                    int elen = ibuf.read2BE();
                    byte[] extra = new byte[elen];
                    ibuf.readN(extra);
                    String name = OtpErlangString.newString(alive);
                    OtpPublishedNode node = new OtpPublishedNode(name);
                    node.Type = type;
                    node.DistHigh = distHigh;
                    node.DistLow = distLow;
                    node.Proto = proto;
                    node.Port = port;

                    if (traceLevel >= traceThreshold)
                    {
                        log.Debug("<- PUBLISH (r4) " + name + " port=" + node.Port);
                    }

                    OtpOutputStream obuf = new OtpOutputStream();
                    obuf.write1(publish4resp);
                    obuf.write1(0);
                    obuf.write2BE(epmd.Creation);
                    obuf.WriteTo(s.GetStream());

                    lock (portmap)
                    {
                        portmap.Add(name, node);
                    }
                    publishedPort.Add(name);
                }
                catch (IOException)
                {
                    if (traceLevel >= traceThreshold)
                    {
                        log.Debug("<- (no response)");
                    }
                    throw new IOException("Request not responding");
                }
                return;
            }

            private void r4_port(TcpClient s, OtpInputStream ibuf)
            {
                try
                {
                    int len = (int)(ibuf.Length - 1);
                    byte[] alive = new byte[len];
                    ibuf.readN(alive);
                    String name = OtpErlangString.newString(alive);
                    OtpPublishedNode node = null;

                    if (traceLevel >= traceThreshold)
                    {
                        log.Debug("<- PORT (r4) " + name);
                    }

                    lock (portmap)
                    {
                        if (portmap.ContainsKey(name))
                        {
                            node = portmap[name];
                        }
                    }

                    OtpOutputStream obuf = new OtpOutputStream();
                    if (node != null)
                    {
                        obuf.write1(port4resp);
                        obuf.write1(0);
                        obuf.write2BE(node.Port);
                        obuf.write1(node.Type);
                        obuf.write1(node.Proto);
                        obuf.write2BE(node.DistHigh);
                        obuf.write2BE(node.DistLow);
                        obuf.write2BE(len);
                        obuf.writeN(alive);
                        obuf.write2BE(0);
                    }
                    else
                    {
                        obuf.write1(port4resp);
                        obuf.write1(1);
                    }
                    obuf.WriteTo(s.GetStream());
                }
                catch (IOException)
                {
                    if (traceLevel >= traceThreshold)
                    {
                        log.Debug("<- (no response)");
                    }
                    throw new IOException("Request not responding");
                }
                return;
            }

            private void r4_names(TcpClient s, OtpInputStream ibuf)
            {
                try
                {
                    if (traceLevel >= traceThreshold)
                    {
                        log.Debug("<- NAMES(r4) ");
                    }

                    OtpOutputStream obuf = new OtpOutputStream();
                    obuf.write4BE(EpmdPort.get());
                    lock (portmap)
                    {
                        foreach (KeyValuePair<string, OtpPublishedNode> pair in portmap)
                        {
                            OtpPublishedNode node = pair.Value;
                            string info = String.Format("name {0} at port {1}\n", node.Alive, node.Port);
                            byte[] bytes = Encoding.GetEncoding("iso-8859-1").GetBytes(info);
                            obuf.writeN(bytes);
                        }
                    }
                    obuf.WriteTo(s.GetStream());
                }
                catch (IOException)
                {
                    if (traceLevel >= traceThreshold)
                    {
                        log.Debug("<- (no response)");
                    }
                    throw new IOException("Request not responding");
                }
                return;
            }

            public override void run()
            {
                byte[] lbuf = new byte[2];
                OtpInputStream ibuf;
                int len;

                try
                {
                    while (!done)
                    {
                        readSock(sock, lbuf);
                        ibuf = new OtpInputStream(lbuf, 0);
                        len = ibuf.read2BE();
                        byte[] tmpbuf = new byte[len];
                        readSock(sock, tmpbuf);
                        ibuf = new OtpInputStream(tmpbuf, 0);

                        int request = ibuf.read1();
                        switch (request)
                        {
                            case publish4req:
                                r4_publish(sock, ibuf);
                                break;

                            case port4req:
                                r4_port(sock, ibuf);
                                done = true;
                                break;

                            case names4req:
                                r4_names(sock, ibuf);
                                done = true;
                                break;

                            case stopReq:
                                break;

                            default:
                                log.InfoFormat("[OtpEpmd] Unknown request (request={0}, length={1}) from {2}",
                                           request, len, sock.Client.RemoteEndPoint);
                                break;
                        }
                    }
                }
                catch (IOException)
                {
                }
                finally
                {
                    quit();
                }
            }
        }
    }
}
