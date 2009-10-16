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
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Erlang.NET
{
    /**
     * <p>
     * Maintains a connection between a Java process and a remote Erlang, Java or C
     * node. The object maintains connection state and allows data to be sent to and
     * received from the peer.
     * </p>
     * 
     * <p>
     * Once a connection is established between the local node and a remote node,
     * the connection object can be used to send and receive messages between the
     * nodes.
     * </p>
     * 
     * <p>
     * The various receive methods are all blocking and will return only when a
     * valid message has been received or an exception is raised.
     * </p>
     * 
     * <p>
     * If an exception occurs in any of the methods in this class, the connection
     * will be closed and must be reopened in order to resume communication with the
     * peer.
     * </p>
     * 
     * <p>
     * The message delivery methods in this class deliver directly to
     * {@link OtpMbox mailboxes} in the {@link OtpNode OtpNode} class.
     * </p>
     * 
     * <p>
     * It is not possible to create an instance of this class directly.
     * OtpCookedConnection objects are created as needed by the underlying mailbox
     * mechanism.
     * </p>
     */
    public class OtpCookedConnection : AbstractConnection
    {
        protected new OtpNode self;

        /*
         * The connection needs to know which local pids have links that pass
         * through here, so that they can be notified in case of connection failure
         */
        protected Links links = null;

        /*
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
        // package scope
        internal OtpCookedConnection(OtpNode self, BufferedTcpClient s)
            : base(self, s)
        {
            this.self = self;
            links = new Links(25);
            start();
        }

        /*
         * Intiate and open a connection to a remote node.
         * 
         * @exception java.io.IOException if it was not possible to connect to the
         * peer.
         * 
         * @exception OtpAuthException if handshake resulted in an authentication
         * error.
         */
        // package scope
        internal OtpCookedConnection(OtpNode self, OtpPeer other)
            : base(self, other)
        {
            this.self = self;
            links = new Links(25);
            start();
        }

        // pass the error to the node
        public override void deliver(Exception e)
        {
            self.deliverError(this, e);
            return;
        }

        /*
         * pass the message to the node for final delivery. Note that the connection
         * itself needs to know about links (in case of connection failure), so we
         * snoop for link/unlink too here.
         */
        public override void deliver(OtpMsg msg)
        {
            bool delivered = self.deliver(msg);

            switch (msg.type())
            {
                case OtpMsg.linkTag:
                    if (delivered)
                    {
                        links.addLink(msg.getRecipientPid(), msg.getSenderPid());
                    }
                    else
                    {
                        try
                        {
                            // no such pid - send exit to sender
                            base.sendExit(msg.getRecipientPid(), msg.getSenderPid(), new OtpErlangAtom("noproc"));
                        }
                        catch (IOException)
                        {
                        }
                    }
                    break;

                case OtpMsg.unlinkTag:
                case OtpMsg.exitTag:
                    links.removeLink(msg.getRecipientPid(), msg.getSenderPid());
                    break;

                case OtpMsg.exit2Tag:
                    break;
            }

            return;
        }

        /*
         * send to pid
         */
        public void send(OtpErlangPid from, OtpErlangPid dest, OtpErlangObject msg)
        {
            // encode and send the message
            sendBuf(from, dest, new OtpOutputStream(msg));
        }

        /*
         * send to remote name dest is recipient's registered name, the nodename is
         * implied by the choice of connection.
         */
        public void send(OtpErlangPid from, String dest, OtpErlangObject msg)
        {
            // encode and send the message
            sendBuf(from, dest, new OtpOutputStream(msg));
        }

        public override void close()
        {
            try
            {
                base.close();
            }
            finally
            {
                breakLinks();
            }
        }

        /*
         * this one called by dying/killed process
         */
        public void exit(OtpErlangPid from, OtpErlangPid to, OtpErlangObject reason)
        {
            try
            {
                base.sendExit(from, to, reason);
            }
            catch (Exception)
            {
            }
        }

        /*
         * this one called explicitely by user code => use exit2
         */
        public void exit2(OtpErlangPid from, OtpErlangPid to, OtpErlangObject reason)
        {
            try
            {
                base.sendExit2(from, to, reason);
            }
            catch (Exception)
            {
            }
        }

        /*
         * snoop for outgoing links and update own table
         */
        public void link(OtpErlangPid from, OtpErlangPid to)
        {
            lock (this)
            {
                try
                {
                    base.sendLink(from, to);
                    links.addLink(from, to);
                }
                catch (IOException)
                {
                    throw new OtpErlangExit("noproc", to);
                }
            }
        }

        /*
         * snoop for outgoing unlinks and update own table
         */
        public void unlink(OtpErlangPid from, OtpErlangPid to)
        {
            lock (this)
            {
                links.removeLink(from, to);
                try
                {
                    base.sendUnlink(from, to);
                }
                catch (IOException)
                {
                }
            }
        }

        /*
         * When the connection fails - send exit to all local pids with links
         * through this connection
         */
        void breakLinks()
        {
            lock (this)
            {
                if (links != null)
                {
                    Link[] l = links.clearLinks();

                    if (l != null)
                    {
                        int len = l.Length;

                        for (int i = 0; i < len; i++)
                        {
                            // send exit "from" remote pids to local ones
                            self.deliver(new OtpMsg(OtpMsg.exitTag, l[i].Remote, l[i].Local, new OtpErlangAtom("noconnection")));
                        }
                    }
                }
            }
        }
    }
}
