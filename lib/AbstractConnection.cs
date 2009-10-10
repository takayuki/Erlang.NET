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
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using log4net;
using log4net.Config;

namespace Erlang.NET
{
    /**
     * Maintains a connection between a Java process and a remote Erlang, Java or C
     * node. The object maintains connection state and allows data to be sent to and
     * received from the peer.
     * 
     * <p>
     * This abstract class provides the neccesary methods to maintain the actual
     * connection and encode the messages and headers in the proper format according
     * to the Erlang distribution protocol. Subclasses can use these methods to
     * provide a more or less transparent communication channel as desired.
     * </p>
     * 
     * <p>
     * Note that no receive methods are provided. Subclasses must provide methods
     * for message delivery, and may implement their own receive methods.
     * <p>
     * 
     * <p>
     * If an exception occurs in any of the methods in this class, the connection
     * will be closed and must be reopened in order to resume communication with the
     * peer. This will be indicated to the subclass by passing the exception to its
     * delivery() method.
     * </p>
     * 
     * <p>
     * The System property OtpConnection.trace can be used to change the initial
     * trace level setting for all connections. Normally the initial trace level is
     * 0 and connections are not traced unless {@link #setTraceLevel
     * setTraceLevel()} is used to change the setting for a particular connection.
     * OtpConnection.trace can be used to turn on tracing by default for all
     * connections.
     * </p>
     */
    public abstract class AbstractConnection : ThreadBase
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected const int headerLen = 2048; // more than enough

        protected const byte passThrough = (byte)0x70;
        protected const byte version = (byte)0x83;

        // Erlang message header tags
        protected const int linkTag = 1;
        protected const int sendTag = 2;
        protected const int exitTag = 3;
        protected const int unlinkTag = 4;
        protected const int nodeLinkTag = 5;
        protected const int regSendTag = 6;
        protected const int groupLeaderTag = 7;
        protected const int exit2Tag = 8;

        protected const int sendTTTag = 12;
        protected const int exitTTTag = 13;
        protected const int regSendTTTag = 16;
        protected const int exit2TTTag = 18;

        // MD5 challenge messsage tags
        protected const int ChallengeReply = 'r';
        protected const int ChallengeAck = 'a';
        protected const int ChallengeStatus = 's';

        private volatile bool done = false;

        protected bool connected = false; // connection status
        protected BufferedTcpClient socket; // communication channel
        protected OtpPeer peer; // who are we connected to
        protected OtpLocalNode self; // this nodes id
        String name; // local name of this connection

        public String Name
        {
            get { return name; }
            set { name = value; }
        }

        protected bool cookieOk = false; // already checked the cookie for this
        // connection
        protected bool sendCookie = true; // Send cookies in messages?

        // tracelevel constants
        protected int traceLevel = 0;

        protected static int defaultLevel = 0;
        protected static int sendThreshold = 1;
        protected static int ctrlThreshold = 2;
        protected static int handshakeThreshold = 3;

        protected static readonly Random random;

        private int flags = 0;

        static AbstractConnection()
        {
            XmlConfigurator.Configure();

            // trace this connection?
            String trace = ConfigurationManager.AppSettings["OtpConnection.trace"];
            try
            {
                if (trace != null)
                {
                    defaultLevel = Int32.Parse(trace);
                }
            }
            catch (FormatException)
            {
                defaultLevel = 0;
            }
            random = new Random();
        }

        /**
         * Accept an incoming connection from a remote node. Used by {@link
         * OtpSelf#accept() OtpSelf.accept()} to create a connection based on data
         * received when handshaking with the peer node, when the remote node is the
         * connection intitiator.
         * 
         * @exception java.io.IOException if it was not possible to connect to the
         * peer.
         * 
         * @exception OtpAuthException if handshake resulted in an authentication
         * error
         */
        protected AbstractConnection(OtpLocalNode self, BufferedTcpClient s)
            : base("receive", true)
        {
            this.self = self;
            peer = new OtpPeer();
            socket = s;

            traceLevel = defaultLevel;

            if (traceLevel >= handshakeThreshold)
            {
                log.Debug("<- ACCEPT FROM " + s.Client.RemoteEndPoint);
            }

            // get his info
            recvName(peer);

            // now find highest common dist value
            if (peer.Proto != self.Proto || self.DistHigh < peer.DistLow || self.DistLow > peer.DistHigh)
            {
                close();
                throw new IOException("No common protocol found - cannot accept connection");
            }
            // highest common version: min(peer.distHigh, self.distHigh)
            peer.DistChoose = peer.DistHigh > self.DistHigh ? self.DistHigh : peer.DistHigh;

            doAccept();
            name = peer.Node;
        }

        /**
         * Intiate and open a connection to a remote node.
         * 
         * @exception java.io.IOException if it was not possible to connect to the
         * peer.
         * 
         * @exception OtpAuthException if handshake resulted in an authentication
         * error.
         */
        protected AbstractConnection(OtpLocalNode self, OtpPeer other)
            : base("receive", true)
        {
            peer = other;
            this.self = self;
            socket = null;
            int port;

            traceLevel = defaultLevel;

            // now get a connection between the two...
            port = OtpEpmd.lookupPort(peer);

            // now find highest common dist value
            if (peer.Proto != self.Proto || self.DistHigh < peer.DistLow || self.DistLow > peer.DistHigh)
            {
                throw new IOException("No common protocol found - cannot connect");
            }

            // highest common version: min(peer.distHigh, self.distHigh)
            peer.DistChoose = peer.DistHigh > self.DistHigh ? self.DistHigh : peer.DistHigh;

            doConnect(port);

            name = peer.Node;
            connected = true;
        }

        /**
         * Deliver communication exceptions to the recipient.
         */
        public abstract void deliver(Exception e);

        /**
         * Deliver messages to the recipient.
         */
        public abstract void deliver(OtpMsg msg);

        /**
         * Send a pre-encoded message to a named process on a remote node.
         * 
         * @param dest
         *            the name of the remote process.
         * @param payload
         *            the encoded message to send.
         * 
         * @exception java.io.IOException
         *                if the connection is not active or a communication error
         *                occurs.
         */
        protected void sendBuf(OtpErlangPid from, String dest, OtpOutputStream payload)
        {
            if (!connected)
            {
                throw new IOException("Not connected");
            }
            OtpOutputStream header = new OtpOutputStream(headerLen);

            // preamble: 4 byte length + "passthrough" tag + version
            header.write4BE(0); // reserve space for length
            header.write1(passThrough);
            header.write1(version);

            // header info
            header.write_tuple_head(4);
            header.write_long(regSendTag);
            header.write_any(from);
            if (sendCookie)
            {
                header.write_atom(self.Cookie);
            }
            else
            {
                header.write_atom("");
            }
            header.write_atom(dest);

            // version for payload
            header.write1(version);

            // fix up length in preamble
            header.poke4BE(0, header.size() + payload.size() - 4);

            do_send(header, payload);
        }

        /**
         * Send a pre-encoded message to a process on a remote node.
         * 
         * @param dest
         *            the Erlang PID of the remote process.
         * @param msg
         *            the encoded message to send.
         * 
         * @exception java.io.IOException
         *                if the connection is not active or a communication error
         *                occurs.
         */
        protected void sendBuf(OtpErlangPid from, OtpErlangPid dest, OtpOutputStream payload)
        {
            if (!connected)
            {
                throw new IOException("Not connected");
            }
            OtpOutputStream header = new OtpOutputStream(headerLen);

            // preamble: 4 byte length + "passthrough" tag + version
            header.write4BE(0); // reserve space for length
            header.write1(passThrough);
            header.write1(version);

            // header info
            header.write_tuple_head(3);
            header.write_long(sendTag);
            if (sendCookie)
            {
                header.write_atom(self.Cookie);
            }
            else
            {
                header.write_atom("");
            }
            header.write_any(dest);

            // version for payload
            header.write1(version);

            // fix up length in preamble
            header.poke4BE(0, header.size() + payload.size() - 4);

            do_send(header, payload);
        }

        /*
         * Send an auth error to peer because he sent a bad cookie. The auth error
         * uses his cookie (not revealing ours). This is just like send_reg
         * otherwise
         */
        private void cookieError(OtpLocalNode local, OtpErlangAtom cookie)
        {
            try
            {
                OtpOutputStream header = new OtpOutputStream(headerLen);

                // preamble: 4 byte length + "passthrough" tag + version
                header.write4BE(0); // reserve space for length
                header.write1(passThrough);
                header.write1(version);

                header.write_tuple_head(4);
                header.write_long(regSendTag);
                header.write_any(local.createPid()); // disposable pid
                header.write_atom(cookie.atomValue()); // important: his cookie,
                // not mine...
                header.write_atom("auth");

                // version for payload
                header.write1(version);

                // the payload

                // the no_auth message (copied from Erlang) Don't change this
                // (Erlang will crash)
                // {$gen_cast, {print, "~n** Unauthorized cookie ~w **~n",
                // [foo@aule]}}
                OtpErlangObject[] msg = new OtpErlangObject[2];
                OtpErlangObject[] msgbody = new OtpErlangObject[3];

                msgbody[0] = new OtpErlangAtom("print");
                msgbody[1] = new OtpErlangString("~n** Bad cookie sent to " + local + " **~n");

                // Erlang will crash and burn if there is no third argument here...
                msgbody[2] = new OtpErlangList(); // empty list

                msg[0] = new OtpErlangAtom("$gen_cast");
                msg[1] = new OtpErlangTuple(msgbody);

                OtpOutputStream payload = new OtpOutputStream(new OtpErlangTuple(msg));

                // fix up length in preamble
                header.poke4BE(0, header.size() + payload.size() - 4);

                try
                {
                    do_send(header, payload);
                }
                catch (IOException)
                {
                } // ignore
            }
            finally
            {
                close();
            }
            throw new OtpAuthException("Remote cookie not authorized: " + cookie.atomValue());
        }

        // link to pid

        /**
         * Create a link between the local node and the specified process on the
         * remote node. If the link is still active when the remote process
         * terminates, an exit signal will be sent to this connection. Use
         * {@link #sendUnlink unlink()} to remove the link.
         * 
         * @param dest
         *            the Erlang PID of the remote process.
         * 
         * @exception java.io.IOException
         *                if the connection is not active or a communication error
         *                occurs.
         */
        protected void sendLink(OtpErlangPid from, OtpErlangPid dest)
        {
            if (!connected)
            {
                throw new IOException("Not connected");
            }
            OtpOutputStream header = new OtpOutputStream(headerLen);

            // preamble: 4 byte length + "passthrough" tag
            header.write4BE(0); // reserve space for length
            header.write1(passThrough);
            header.write1(version);

            // header
            header.write_tuple_head(3);
            header.write_long(linkTag);
            header.write_any(from);
            header.write_any(dest);

            // fix up length in preamble
            header.poke4BE(0, header.size() - 4);

            do_send(header);
        }

        /**
         * Remove a link between the local node and the specified process on the
         * remote node. This method deactivates links created with {@link #sendLink
         * link()}.
         * 
         * @param dest
         *            the Erlang PID of the remote process.
         * 
         * @exception java.io.IOException
         *                if the connection is not active or a communication error
         *                occurs.
         */
        protected void sendUnlink(OtpErlangPid from, OtpErlangPid dest)
        {
            if (!connected)
            {
                throw new IOException("Not connected");
            }
            OtpOutputStream header = new OtpOutputStream(headerLen);

            // preamble: 4 byte length + "passthrough" tag
            header.write4BE(0); // reserve space for length
            header.write1(passThrough);
            header.write1(version);

            // header
            header.write_tuple_head(3);
            header.write_long(unlinkTag);
            header.write_any(from);
            header.write_any(dest);

            // fix up length in preamble
            header.poke4BE(0, header.size() - 4);

            do_send(header);
        }

        /* used internally when "processes" terminate */
        protected void sendExit(OtpErlangPid from, OtpErlangPid dest, OtpErlangObject reason)
        {
            sendExit(exitTag, from, dest, reason);
        }

        /**
         * Send an exit signal to a remote process.
         * 
         * @param dest
         *            the Erlang PID of the remote process.
         * @param reason
         *            an Erlang term describing the exit reason.
         * 
         * @exception java.io.IOException
         *                if the connection is not active or a communication error
         *                occurs.
         */
        protected void sendExit2(OtpErlangPid from, OtpErlangPid dest, OtpErlangObject reason)
        {
            sendExit(exit2Tag, from, dest, reason);
        }

        private void sendExit(int tag, OtpErlangPid from, OtpErlangPid dest, OtpErlangObject reason)
        {
            if (!connected)
            {
                throw new IOException("Not connected");
            }
            OtpOutputStream header = new OtpOutputStream(headerLen);

            // preamble: 4 byte length + "passthrough" tag
            header.write4BE(0); // reserve space for length
            header.write1(passThrough);
            header.write1(version);

            // header
            header.write_tuple_head(4);
            header.write_long(tag);
            header.write_any(from);
            header.write_any(dest);
            header.write_any(reason);

            // fix up length in preamble
            header.poke4BE(0, header.size() - 4);

            do_send(header);
        }

        public override void run()
        {
            if (!connected)
            {
                deliver(new IOException("Not connected"));
                return;
            }

            byte[] lbuf = new byte[4];
            OtpInputStream ibuf;
            OtpErlangObject traceobj;
            int len;
            byte[] tock = { 0, 0, 0, 0 };

            try
            {
            receive_loop:
                while (!done)
                {
                    // don't return until we get a real message
                    // or a failure of some kind (e.g. EXIT)
                    // read length and read buffer must be atomic!
                    do
                    {
                        // read 4 bytes - get length of incoming packet
                        // socket.getInputStream().read(lbuf);
                        readSock(socket, lbuf);
                        ibuf = new OtpInputStream(lbuf, flags);
                        len = ibuf.read4BE();

                        // received tick? send tock!
                        if (len == 0)
                        {
                            lock (this)
                            {
                                socket.GetOutputStream().Write(tock, 0, tock.Length);
                                socket.GetOutputStream().Flush();
                            }
                        }

                    } while (len == 0); // tick_loop

                    // got a real message (maybe) - read len bytes
                    byte[] tmpbuf = new byte[len];
                    // i = socket.getInputStream().read(tmpbuf);
                    readSock(socket, tmpbuf);
                    ibuf = new OtpInputStream(tmpbuf, flags);

                    if (ibuf.read1() != passThrough)
                    {
                        goto receive_loop;
                    }

                    // got a real message (really)
                    OtpErlangObject reason = null;
                    OtpErlangAtom cookie = null;
                    OtpErlangObject tmp = null;
                    OtpErlangTuple head = null;
                    OtpErlangAtom toName;
                    OtpErlangPid to;
                    OtpErlangPid from;
                    int tag;

                    // decode the header
                    tmp = ibuf.read_any();
                    if (!(tmp is OtpErlangTuple))
                    {
                        goto receive_loop;
                    }

                    head = (OtpErlangTuple)tmp;
                    if (!(head.elementAt(0) is OtpErlangLong))
                    {
                        goto receive_loop;
                    }

                    // lets see what kind of message this is
                    tag = (int)((OtpErlangLong)head.elementAt(0)).longValue();

                    switch (tag)
                    {
                        case sendTag: // { SEND, Cookie, ToPid }
                        case sendTTTag: // { SEND, Cookie, ToPid, TraceToken }
                            if (!cookieOk)
                            {
                                // we only check this once, he can send us bad cookies
                                // later if he likes
                                if (!(head.elementAt(1) is OtpErlangAtom))
                                {
                                    goto receive_loop;
                                }
                                cookie = (OtpErlangAtom)head.elementAt(1);
                                if (sendCookie)
                                {
                                    if (!cookie.atomValue().Equals(self.Cookie))
                                    {
                                        cookieError(self, cookie);
                                    }
                                }
                                else
                                {
                                    if (!cookie.atomValue().Equals(""))
                                    {
                                        cookieError(self, cookie);
                                    }
                                }
                                cookieOk = true;
                            }

                            if (traceLevel >= sendThreshold)
                            {
                                log.Debug("<- " + headerType(head) + " " + head);

                                /* show received payload too */
                                ibuf.Mark(0);
                                traceobj = ibuf.read_any();

                                if (traceobj != null)
                                {
                                    log.Debug("   " + traceobj);
                                }
                                else
                                {
                                    log.Debug("   (null)");
                                }
                                ibuf.Reset();
                            }

                            to = (OtpErlangPid)head.elementAt(2);

                            deliver(new OtpMsg(to, ibuf));
                            break;

                        case regSendTag: // { REG_SEND, FromPid, Cookie, ToName }
                        case regSendTTTag: // { REG_SEND, FromPid, Cookie, ToName,
                            // TraceToken }
                            if (!cookieOk)
                            {
                                // we only check this once, he can send us bad cookies
                                // later if he likes
                                if (!(head.elementAt(2) is OtpErlangAtom))
                                {
                                    goto receive_loop;
                                }
                                cookie = (OtpErlangAtom)head.elementAt(2);
                                if (sendCookie)
                                {
                                    if (!cookie.atomValue().Equals(self.Cookie))
                                    {
                                        cookieError(self, cookie);
                                    }
                                }
                                else
                                {
                                    if (!cookie.atomValue().Equals(""))
                                    {
                                        cookieError(self, cookie);
                                    }
                                }
                                cookieOk = true;
                            }

                            if (traceLevel >= sendThreshold)
                            {
                                log.Debug("<- " + headerType(head) + " " + head);

                                /* show received payload too */
                                ibuf.Mark(0);
                                traceobj = ibuf.read_any();

                                if (traceobj != null)
                                {
                                    log.Debug("   " + traceobj);
                                }
                                else
                                {
                                    log.Debug("   (null)");
                                }
                                ibuf.Reset();
                            }

                            from = (OtpErlangPid)head.elementAt(1);
                            toName = (OtpErlangAtom)head.elementAt(3);

                            deliver(new OtpMsg(from, toName.atomValue(), ibuf));
                            break;

                        case exitTag: // { EXIT, FromPid, ToPid, Reason }
                        case exit2Tag: // { EXIT2, FromPid, ToPid, Reason }
                            if (head.elementAt(3) == null)
                            {
                                goto receive_loop;
                            }
                            if (traceLevel >= ctrlThreshold)
                            {
                                log.Debug("<- " + headerType(head) + " " + head);
                            }

                            from = (OtpErlangPid)head.elementAt(1);
                            to = (OtpErlangPid)head.elementAt(2);
                            reason = head.elementAt(3);

                            deliver(new OtpMsg(tag, from, to, reason));
                            break;

                        case exitTTTag: // { EXIT, FromPid, ToPid, TraceToken, Reason }
                        case exit2TTTag: // { EXIT2, FromPid, ToPid, TraceToken,
                            // Reason
                            // }
                            // as above, but bifferent element number
                            if (head.elementAt(4) == null)
                            {
                                goto receive_loop;
                            }
                            if (traceLevel >= ctrlThreshold)
                            {
                                log.Debug("<- " + headerType(head) + " " + head);
                            }

                            from = (OtpErlangPid)head.elementAt(1);
                            to = (OtpErlangPid)head.elementAt(2);
                            reason = head.elementAt(4);

                            deliver(new OtpMsg(tag, from, to, reason));
                            break;

                        case linkTag: // { LINK, FromPid, ToPid}
                        case unlinkTag: // { UNLINK, FromPid, ToPid}
                            if (traceLevel >= ctrlThreshold)
                            {
                                log.Debug("<- " + headerType(head) + " " + head);
                            }

                            from = (OtpErlangPid)head.elementAt(1);
                            to = (OtpErlangPid)head.elementAt(2);

                            deliver(new OtpMsg(tag, from, to));
                            break;

                        // absolutely no idea what to do with these, so we ignore
                        // them...
                        case groupLeaderTag: // { GROUPLEADER, FromPid, ToPid}
                        case nodeLinkTag: // { NODELINK }
                            // (just show trace)
                            if (traceLevel >= ctrlThreshold)
                            {
                                log.Debug("<- " + headerType(head) + " " + head);
                            }
                            break;

                        default:
                            // garbage?
                            goto receive_loop;
                    }
                } // end receive_loop

                // this section reachable only with break
                // we have received garbage from peer
                deliver(new OtpErlangExit("Remote is sending garbage"));

            } // try

            catch (OtpAuthException e)
            {
                deliver(e);
            }
            catch (OtpErlangDecodeException)
            {
                deliver(new OtpErlangExit("Remote is sending garbage"));
            }
            catch (IOException)
            {
                deliver(new OtpErlangExit("Remote has closed connection"));
            }
            finally
            {
                close();
            }
        }

        /**
         * <p>
         * Set the trace level for this connection. Normally tracing is off by
         * default unless System property OtpConnection.trace was set.
         * </p>
         * 
         * <p>
         * The following levels are valid: 0 turns off tracing completely, 1 shows
         * ordinary send and receive messages, 2 shows control messages such as link
         * and unlink, 3 shows handshaking at connection setup, and 4 shows
         * communication with Epmd. Each level includes the information shown by the
         * lower ones.
         * </p>
         * 
         * @param level
         *            the level to set.
         * 
         * @return the previous trace level.
         */
        public int setTraceLevel(int level)
        {
            int oldLevel = traceLevel;

            // pin the value
            if (level < 0)
            {
                level = 0;
            }
            else if (level > 4)
            {
                level = 4;
            }

            traceLevel = level;

            return oldLevel;
        }

        /**
         * Get the trace level for this connection.
         * 
         * @return the current trace level.
         */
        public int getTraceLevel()
        {
            return traceLevel;
        }

        /**
         * Close the connection to the remote node.
         */
        public virtual void close()
        {
            done = true;
            connected = false;
            lock (this)
            {
                try
                {
                    if (socket != null)
                    {
                        if (traceLevel >= ctrlThreshold)
                        {
                            log.Debug("-> CLOSE");
                        }
                        socket.Close();
                    }
                }
                catch (SocketException) /* ignore socket close errors */
                {
                }
                catch (InvalidOperationException)
                {
                }
                finally
                {
                    socket = null;
                }
            }
        }

        /**
         * Determine if the connection is still alive. Note that this method only
         * reports the status of the connection, and that it is possible that there
         * are unread messages waiting in the receive queue.
         * 
         * @return true if the connection is alive.
         */
        public bool isConnected()
        {
            return connected;
        }

        // used by send and send_reg (message types with payload)
        protected void do_send(OtpOutputStream header, OtpOutputStream payload)
        {
            lock (this)
            {
                try
                {
                    if (traceLevel >= sendThreshold)
                    {
                        // Need to decode header and output buffer to show trace
                        // message!
                        // First make OtpInputStream, then decode.
                        try
                        {
                            OtpErlangObject h = header.getOtpInputStream(5).read_any();

                            log.Debug("-> " + headerType(h) + " " + h);

                            OtpErlangObject o = payload.getOtpInputStream(0).read_any();
                            log.Debug("   " + o);
                            o = null;
                        }
                        catch (OtpErlangDecodeException e)
                        {
                            log.Debug("   " + "can't decode output buffer:" + e);
                        }
                    }

                    header.WriteTo(socket.GetOutputStream());
                    payload.WriteTo(socket.GetOutputStream());
                }
                catch (IOException e)
                {
                    close();
                    throw e;
                }
            }
        }

        // used by the other message types
        protected void do_send(OtpOutputStream header)
        {
            lock (this)
            {
                try
                {
                    if (traceLevel >= ctrlThreshold)
                    {
                        try
                        {
                            OtpErlangObject h = header.getOtpInputStream(5).read_any();
                            log.Debug("-> " + headerType(h) + " " + h);
                        }
                        catch (OtpErlangDecodeException e)
                        {
                            log.Debug("   " + "can't decode output buffer: " + e);
                        }
                    }
                    header.WriteTo(socket.GetOutputStream());
                }
                catch (IOException e)
                {
                    close();
                    throw e;
                }
            }
        }

        protected String headerType(OtpErlangObject h)
        {
            int tag = -1;

            if (h is OtpErlangTuple)
            {
                tag = (int)((OtpErlangLong)((OtpErlangTuple)h).elementAt(0)).longValue();
            }

            switch (tag)
            {
                case linkTag:
                    return "LINK";

                case sendTag:
                    return "SEND";

                case exitTag:
                    return "EXIT";

                case unlinkTag:
                    return "UNLINK";

                case nodeLinkTag:
                    return "NODELINK";

                case regSendTag:
                    return "REG_SEND";

                case groupLeaderTag:
                    return "GROUP_LEADER";

                case exit2Tag:
                    return "EXIT2";

                case sendTTTag:
                    return "SEND_TT";

                case exitTTTag:
                    return "EXIT_TT";

                case regSendTTTag:
                    return "REG_SEND_TT";

                case exit2TTTag:
                    return "EXIT2_TT";
            }

            return "(unknown type)";
        }

        /* this method now throws exception if we don't get full read */
        protected int readSock(BufferedTcpClient s, byte[] b)
        {
            int got = 0;
            int len = b.Length;
            int i;
            Stream st = null;

            lock (this)
            {
                if (s == null)
                {
                    throw new IOException("expected " + len + " bytes, socket was closed");
                }
                st = s.GetInputStream();
            }

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
                    /*
                     * This is a corner case. According to
                     * http://java.sun.com/j2se/1.4.2/docs/api/ class InputStream
                     * is.read(,,l) can only return 0 if l==0. In other words it
                     * should not happen, but apparently did.
                     */
                    throw new IOException("Remote connection closed");
                }
                else
                {
                    got += i;
                }
            }
            return got;
        }

        protected void doAccept()
        {
            try
            {
                sendStatus("ok");
                int our_challenge = genChallenge();
                sendChallenge(peer.DistChoose, self.Flags, our_challenge);
                int her_challenge = recvChallengeReply(our_challenge);
                byte[] our_digest = genDigest(her_challenge, self.Cookie);
                sendChallengeAck(our_digest);
                connected = true;
                cookieOk = true;
                sendCookie = false;
            }
            catch (IOException ie)
            {
                close();
                throw ie;
            }
            catch (OtpAuthException ae)
            {
                close();
                throw ae;
            }
            catch (Exception)
            {
                String nn = peer.Node;
                close();
                throw new IOException("Error accepting connection from " + nn);
            }

            if (traceLevel >= handshakeThreshold)
            {
                log.Debug("<- MD5 ACCEPTED " + peer.Host);
            }
        }

        protected void doConnect(int port)
        {
            try
            {
                socket = new BufferedTcpClient(new TcpClient(peer.Host, port));

                if (traceLevel >= handshakeThreshold)
                {
                    log.Debug("-> MD5 CONNECT TO " + peer.Host + ":" + port);
                }
                sendName(peer.DistChoose, self.Flags);
                recvStatus();
                int her_challenge = recvChallenge();
                byte[] our_digest = genDigest(her_challenge, self.Cookie);
                int our_challenge = genChallenge();
                sendChallengeReply(our_challenge, our_digest);
                recvChallengeAck(our_challenge);
                cookieOk = true;
                sendCookie = false;
            }
            catch (IOException e)
            {
                throw e;
            }
            catch (OtpAuthException e)
            {
                close();
                throw e;
            }
            catch (Exception)
            {
                close();
                throw new IOException("Cannot connect to peer node");
            }
        }

        // This is nooo good as a challenge,
        // XXX fix me.
        static protected int genChallenge()
        {
            return random.Next();
        }

        // Used to debug print a message digest
        static String hex0(byte x)
        {
            char[] tab = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
			    'a', 'b', 'c', 'd', 'e', 'f' };
            uint u;
            if (x < 0)
            {
                u = ((uint)x) & 0x7F;
                u |= 1 << 7;
            }
            else
            {
                u = x;
            }
            return "" + tab[u >> 4] + tab[u & 0xF];
        }

        static String hex(byte[] b)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                int i;
                for (i = 0; i < b.Length; ++i)
                {
                    sb.Append(hex0(b[i]));
                }
            }
            catch (Exception)
            {
                // Debug function, ignore errors.
            }
            return sb.ToString();
        }

        protected byte[] genDigest(int challenge, String cookie)
        {
            int i;
            long ch2;

            if (challenge < 0)
            {
                ch2 = 1L << 31;
                ch2 |= challenge & 0x7FFFFFFFL;
            }
            else
            {
                ch2 = challenge;
            }
            OtpMD5 context = new OtpMD5();
            context.update(cookie);
            context.update("" + ch2);

            int[] tmp = context.final_bytes();
            byte[] res = new byte[tmp.Length];
            for (i = 0; i < tmp.Length; ++i)
            {
                res[i] = (byte)(tmp[i] & 0xFF);
            }
            return res;
        }

        protected void sendName(int dist, int flags)
        {
            OtpOutputStream obuf = new OtpOutputStream();
            String str = self.Node;
            obuf.write2BE(str.Length + 7); // 7 bytes + nodename
            obuf.write1(AbstractNode.NTYPE_R6);
            obuf.write2BE(dist);
            obuf.write4BE(flags);
            obuf.write(Encoding.GetEncoding("iso-8859-1").GetBytes(str));

            obuf.WriteTo(socket.GetOutputStream());

            if (traceLevel >= handshakeThreshold)
            {
                log.Debug("-> " + "HANDSHAKE sendName" + " flags=" + flags
                             + " dist=" + dist + " local=" + self);
            }
        }

        protected void sendChallenge(int dist, int flags, int challenge)
        {
            OtpOutputStream obuf = new OtpOutputStream();
            String str = self.Node;
            obuf.write2BE(str.Length + 11); // 11 bytes + nodename
            obuf.write1(AbstractNode.NTYPE_R6);
            obuf.write2BE(dist);
            obuf.write4BE(flags);
            obuf.write4BE(challenge);
            obuf.write(Encoding.GetEncoding("iso-8859-1").GetBytes(str));

            obuf.WriteTo(socket.GetOutputStream());

            if (traceLevel >= handshakeThreshold)
            {
                log.Debug("-> " + "HANDSHAKE sendChallenge" + " flags="
                             + flags + " dist=" + dist + " challenge=" + challenge
                             + " local=" + self);
            }
        }

        protected byte[] read2BytePackage()
        {
            byte[] lbuf = new byte[2];
            byte[] tmpbuf;

            readSock(socket, lbuf);
            OtpInputStream ibuf = new OtpInputStream(lbuf, 0);
            int len = ibuf.read2BE();
            tmpbuf = new byte[len];
            readSock(socket, tmpbuf);
            return tmpbuf;
        }

        protected void recvName(OtpPeer peer)
        {
            String hisname = "";

            try
            {
                byte[] tmpbuf = read2BytePackage();
                OtpInputStream ibuf = new OtpInputStream(tmpbuf, 0);
                byte[] tmpname;
                int len = tmpbuf.Length;
                peer.Type = ibuf.read1();
                if (peer.Type != AbstractNode.NTYPE_R6)
                {
                    throw new IOException("Unknown remote node type");
                }
                peer.DistLow = peer.DistHigh = ibuf.read2BE();
                if (peer.DistLow < 5)
                {
                    throw new IOException("Unknown remote node type");
                }
                peer.Flags = ibuf.read4BE();
                tmpname = new byte[len - 7];
                ibuf.readN(tmpname);
                hisname = OtpErlangString.newString(tmpname);
                // Set the old nodetype parameter to indicate hidden/normal status
                // When the old handshake is removed, the ntype should also be.
                if ((peer.Flags & AbstractNode.dFlagPublished) != 0)
                {
                    peer.Type = AbstractNode.NTYPE_R4_ERLANG;
                }
                else
                {
                    peer.Type = AbstractNode.NTYPE_R4_HIDDEN;
                }

                if ((peer.Flags & AbstractNode.dFlagExtendedReferences) == 0)
                {
                    throw new IOException("Handshake failed - peer cannot handle extended references");
                }

                if ((peer.Flags & AbstractNode.dFlagExtendedPidsPorts) == 0)
                {
                    throw new IOException("Handshake failed - peer cannot handle extended pids and ports");
                }
            }
            catch (OtpErlangDecodeException)
            {
                throw new IOException("Handshake failed - not enough data");
            }

            int i = hisname.IndexOf('@', 0);
            peer.Node = hisname;
            peer.Alive = hisname.Substring(0, i);
            peer.Host = hisname.Substring(i + 1, hisname.Length - (i + 1));

            if (traceLevel >= handshakeThreshold)
            {
                log.Debug("<- " + "HANDSHAKE" + " ntype=" + peer.Type
                             + " dist=" + peer.DistHigh + " remote=" + peer);
            }
        }

        protected int recvChallenge()
        {
            int challenge;

            try
            {
                byte[] buf = read2BytePackage();
                OtpInputStream ibuf = new OtpInputStream(buf, 0);
                peer.Type = ibuf.read1();
                if (peer.Type != AbstractNode.NTYPE_R6)
                {
                    throw new IOException("Unexpected peer type");
                }
                peer.DistLow = peer.DistHigh = ibuf.read2BE();
                peer.Flags = ibuf.read4BE();
                challenge = ibuf.read4BE();
                byte[] tmpname = new byte[buf.Length - 11];
                ibuf.readN(tmpname);
                String hisname = OtpErlangString.newString(tmpname);
                if (!hisname.Equals(peer.Node))
                {
                    throw new IOException("Handshake failed - peer has wrong name: " + hisname);
                }

                if ((peer.Flags & AbstractNode.dFlagExtendedReferences) == 0)
                {
                    throw new IOException("Handshake failed - peer cannot handle extended references");
                }

                if ((peer.Flags & AbstractNode.dFlagExtendedPidsPorts) == 0)
                {
                    throw new IOException("Handshake failed - peer cannot handle extended pids and ports");
                }

            }
            catch (OtpErlangDecodeException)
            {
                throw new IOException("Handshake failed - not enough data");
            }

            if (traceLevel >= handshakeThreshold)
            {
                log.Debug("<- " + "HANDSHAKE recvChallenge" + " from="
                             + peer.Node + " challenge=" + challenge + " local=" + self);
            }

            return challenge;
        }

        protected void sendChallengeReply(int challenge, byte[] digest)
        {
            OtpOutputStream obuf = new OtpOutputStream();
            obuf.write2BE(21);
            obuf.write1(ChallengeReply);
            obuf.write4BE(challenge);
            obuf.write(digest);
            obuf.WriteTo(socket.GetOutputStream());

            if (traceLevel >= handshakeThreshold)
            {
                log.Debug("-> " + "HANDSHAKE sendChallengeReply"
                             + " challenge=" + challenge + " digest=" + hex(digest)
                             + " local=" + self);
            }
        }

        // Would use Array.equals in newer JDK...
        private bool digests_equals(byte[] a, byte[] b)
        {
            int i;
            for (i = 0; i < 16; ++i)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }

        protected int recvChallengeReply(int our_challenge)
        {
            int challenge;
            byte[] her_digest = new byte[16];

            try
            {
                byte[] buf = read2BytePackage();
                OtpInputStream ibuf = new OtpInputStream(buf, 0);
                int tag = ibuf.read1();
                if (tag != ChallengeReply)
                {
                    throw new IOException("Handshake protocol error");
                }
                challenge = ibuf.read4BE();
                ibuf.readN(her_digest);
                byte[] our_digest = genDigest(our_challenge, self.Cookie);
                if (!digests_equals(her_digest, our_digest))
                {
                    throw new OtpAuthException("Peer authentication error.");
                }
            }
            catch (OtpErlangDecodeException)
            {
                throw new IOException("Handshake failed - not enough data");
            }

            if (traceLevel >= handshakeThreshold)
            {
                log.Debug("<- " + "HANDSHAKE recvChallengeReply"
                             + " from=" + peer.Node + " challenge=" + challenge
                             + " digest=" + hex(her_digest) + " local=" + self);
            }

            return challenge;
        }

        protected void sendChallengeAck(byte[] digest)
        {
            OtpOutputStream obuf = new OtpOutputStream();
            obuf.write2BE(17);
            obuf.write1(ChallengeAck);
            obuf.write(digest);

            obuf.WriteTo(socket.GetOutputStream());

            if (traceLevel >= handshakeThreshold)
            {
                log.Debug("-> " + "HANDSHAKE sendChallengeAck"
                             + " digest=" + hex(digest) + " local=" + self);
            }
        }

        protected void recvChallengeAck(int our_challenge)
        {
            byte[] her_digest = new byte[16];
            try
            {
                byte[] buf = read2BytePackage();
                OtpInputStream ibuf = new OtpInputStream(buf, 0);
                int tag = ibuf.read1();
                if (tag != ChallengeAck)
                {
                    throw new IOException("Handshake protocol error");
                }
                ibuf.readN(her_digest);
                byte[] our_digest = genDigest(our_challenge, self.Cookie);
                if (!digests_equals(her_digest, our_digest))
                {
                    throw new OtpAuthException("Peer authentication error.");
                }
            }
            catch (OtpErlangDecodeException)
            {
                throw new IOException("Handshake failed - not enough data");
            }
            catch (Exception)
            {
                throw new OtpAuthException("Peer authentication error.");
            }

            if (traceLevel >= handshakeThreshold)
            {
                log.Debug("<- " + "HANDSHAKE recvChallengeAck" + " from="
                             + peer.Node + " digest=" + hex(her_digest) + " local=" + self);
            }
        }

        protected void sendStatus(String status)
        {
            OtpOutputStream obuf = new OtpOutputStream();
            obuf.write2BE(status.Length + 1);
            obuf.write1(ChallengeStatus);
            obuf.write(Encoding.GetEncoding("iso-8859-1").GetBytes(status));

            obuf.WriteTo(socket.GetOutputStream());

            if (traceLevel >= handshakeThreshold)
            {
                log.Debug("-> " + "HANDSHAKE sendStatus" + " status="
                             + status + " local=" + self);
            }
        }

        protected void recvStatus()
        {
            try
            {
                byte[] buf = read2BytePackage();
                OtpInputStream ibuf = new OtpInputStream(buf, 0);
                int tag = ibuf.read1();
                if (tag != ChallengeStatus)
                {
                    throw new IOException("Handshake protocol error");
                }
                byte[] tmpbuf = new byte[buf.Length - 1];
                ibuf.readN(tmpbuf);
                String status = OtpErlangString.newString(tmpbuf);

                if (status.CompareTo("ok") != 0)
                {
                    throw new IOException("Peer replied with status '" + status + "' instead of 'ok'");
                }
            }
            catch (OtpErlangDecodeException)
            {
                throw new IOException("Handshake failed - not enough data");
            }
            if (traceLevel >= handshakeThreshold)
            {
                log.Debug("<- " + "HANDSHAKE recvStatus (ok)" + " local=" + self);
            }
        }

        public void setFlags(int flags)
        {
            this.flags = flags;
        }

        public int getFlags()
        {
            return flags;
        }
    }
}
