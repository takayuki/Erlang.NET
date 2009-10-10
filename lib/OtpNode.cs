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
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Erlang.NET
{
    /**
     * <p>
     * Represents a local OTP node. This class is used when you do not wish to
     * manage connections yourself - outgoing connections are established as needed,
     * and incoming connections accepted automatically. This class supports the use
     * of a mailbox API for communication, while management of the underlying
     * communication mechanism is automatic and hidden from the application
     * programmer.
     * </p>
     * 
     * <p>
     * Once an instance of this class has been created, obtain one or more mailboxes
     * in order to send or receive messages. The first message sent to a given node
     * will cause a connection to be set up to that node. Any messages received will
     * be delivered to the appropriate mailboxes.
     * </p>
     * 
     * <p>
     * To shut down the node, call {@link #close close()}. This will prevent the
     * node from accepting additional connections and it will cause all existing
     * connections to be closed. Any unread messages in existing mailboxes can still
     * be read, however no new messages will be delivered to the mailboxes.
     * </p>
     * 
     * <p>
     * Note that the use of this class requires that Epmd (Erlang Port Mapper
     * Daemon) is running on each cooperating host. This class does not start Epmd
     * automatically as Erlang does, you must start it manually or through some
     * other means. See the Erlang documentation for more information about this.
     * </p>
     */
    public class OtpNode : OtpLocalNode
    {
        private bool initDone = false;

        // thread to manage incoming connections
        private Acceptor acceptor = null;

        // thread to schedule actors
        private OtpActorSched sched = null;

        // keep track of all connections
        Dictionary<String, OtpCookedConnection> connections = null;

        // keep track of all mailboxes
        Mailboxes mboxes = null;

        // handle status changes
        OtpNodeStatus handler;

        // flags
        private int flags = 0;


        public Dictionary<String, OtpCookedConnection> Connections
        {
            get { return connections; }
        }

        public new int Flags
        {
            get { return flags; }
        }

        /**
         * <p>
         * Create a node using the default cookie. The default cookie is found by
         * reading the first line of the .erlang.cookie file in the user's home
         * directory. The home directory is obtained from the System property
         * "user.home".
         * </p>
         * 
         * <p>
         * If the file does not exist, an empty string is used. This method makes no
         * attempt to create the file.
         * </p>
         * 
         * @param node
         *            the name of this node.
         * 
         * @exception IOException
         *                if communication could not be initialized.
         * 
         */
        public OtpNode(String node)
            : this(node, defaultCookie, 0)
        {
        }

        /**
         * Create a node.
         * 
         * @param node
         *            the name of this node.
         * 
         * @param cookie
         *            the authorization cookie that will be used by this node when
         *            it communicates with other nodes.
         * 
         * @exception IOException
         *                if communication could not be initialized.
         * 
         */
        public OtpNode(String node, String cookie)
            : this(node, cookie, 0)
        {
        }

        /**
         * Create a node.
         * 
         * @param node
         *            the name of this node.
         * 
         * @param cookie
         *            the authorization cookie that will be used by this node when
         *            it communicates with other nodes.
         * 
         * @param port
         *            the port number you wish to use for incoming connections.
         *            Specifying 0 lets the system choose an available port.
         * 
         * @exception IOException
         *                if communication could not be initialized.
         * 
         */
        public OtpNode(String node, String cookie, int port)
            : base(node, cookie)
        {
            init(port);
        }

        private void init(int port)
        {
            lock (this)
            {
                if (!initDone)
                {
                    connections = new Dictionary<String, OtpCookedConnection>();

                    sched = new OtpActorSched();
                    mboxes = new Mailboxes(this, sched);
                    acceptor = new Acceptor(this, port);
                    initDone = true;
                }
            }
        }

        /**
         * Close the node. Unpublish the node from Epmd (preventing new connections)
         * and close all existing connections.
         */
        public void close()
        {
            lock (this)
            {
                acceptor.quit();

                mboxes.clear();

                lock (connections)
                {
                    OtpCookedConnection[] conns = new OtpCookedConnection[connections.Count];
                    int i = 0;
                    foreach (OtpCookedConnection conn in connections.Values)
                    {
                        conns[i++] = conn;
                    }
                    connections.Clear();
                    foreach (OtpCookedConnection conn in conns)
                    {
                        conn.close();
                    }
                    initDone = false;
                }
            }
        }

        /**
         * Create an unnamed {@link OtpMbox mailbox} that can be used to send and
         * receive messages with other, similar mailboxes and with Erlang processes.
         * Messages can be sent to this mailbox by using its associated
         * {@link OtpMbox#self pid}.
         * 
         * @return a mailbox.
         */
        public OtpMbox createMbox(bool sync)
        {
            return mboxes.create(sync);
        }

        /**
         * Close the specified mailbox with reason 'normal'.
         * 
         * @param mbox
         *            the mailbox to close.
         * 
         *            <p>
         *            After this operation, the mailbox will no longer be able to
         *            receive messages. Any delivered but as yet unretrieved
         *            messages can still be retrieved however.
         *            </p>
         * 
         *            <p>
         *            If there are links from the mailbox to other
         *            {@link OtpErlangPid pids}, they will be broken when this
         *            method is called and exit signals with reason 'normal' will be
         *            sent.
         *            </p>
         * 
         */
        public void closeMbox(OtpMbox mbox)
        {
            closeMbox(mbox, new OtpErlangAtom("normal"));
        }

        /**
         * Close the specified mailbox with the given reason.
         * 
         * @param mbox
         *            the mailbox to close.
         * @param reason
         *            an Erlang term describing the reason for the termination.
         * 
         *            <p>
         *            After this operation, the mailbox will no longer be able to
         *            receive messages. Any delivered but as yet unretrieved
         *            messages can still be retrieved however.
         *            </p>
         * 
         *            <p>
         *            If there are links from the mailbox to other
         *            {@link OtpErlangPid pids}, they will be broken when this
         *            method is called and exit signals with the given reason will
         *            be sent.
         *            </p>
         * 
         */
        public void closeMbox(OtpMbox mbox, OtpErlangObject reason)
        {
            if (mbox != null)
            {
                mboxes.remove(mbox);
                mbox.Name = null;
                mbox.breakLinks(reason);
            }
        }

        /**
         * Create an named mailbox that can be used to send and receive messages
         * with other, similar mailboxes and with Erlang processes. Messages can be
         * sent to this mailbox by using its registered name or the associated
         * {@link OtpMbox#self pid}.
         * 
         * @param name
         *            a name to register for this mailbox. The name must be unique
         *            within this OtpNode.
         * 
         * @return a mailbox, or null if the name was already in use.
         * 
         */
        public OtpMbox createMbox(String name, bool sync)
        {
            return mboxes.create(name, sync);
        }

        /**
         * <p>
         * Register or remove a name for the given mailbox. Registering a name for a
         * mailbox enables others to send messages without knowing the
         * {@link OtpErlangPid pid} of the mailbox. A mailbox can have at most one
         * name; if the mailbox already had a name, calling this method will
         * supercede that name.
         * </p>
         * 
         * @param name
         *            the name to register for the mailbox. Specify null to
         *            unregister the existing name from this mailbox.
         * 
         * @param mbox
         *            the mailbox to associate with the name.
         * 
         * @return true if the name was available, or false otherwise.
         */
        public bool registerName(String name, OtpMbox mbox)
        {
            return mboxes.register(name, mbox);
        }

        /**
         * Get a list of all known registered names on this node.
         * 
         * @return an array of Strings, containins all known registered names on
         *         this node.
         */
        public String[] getNames()
        {
            return mboxes.names();
        }

        /**
         * Determine the {@link OtpErlangPid pid} corresponding to a registered name
         * on this node.
         * 
         * @return the {@link OtpErlangPid pid} corresponding to the registered
         *         name, or null if the name is not known on this node.
         */
        public OtpErlangPid whereis(String name)
        {
            OtpMbox m = mboxes.get(name);
            if (m != null)
            {
                return m.Self;
            }
            return null;
        }

        /**
         * Register interest in certain system events. The {@link OtpNodeStatus
         * OtpNodeStatus} handler object contains callback methods, that will be
         * called when certain events occur.
         * 
         * @param handler
         *            the callback object to register. To clear the handler, specify
         *            null as the handler to use.
         * 
         */
        public void registerStatusHandler(OtpNodeStatus handler)
        {
            lock (this)
            {
                this.handler = handler;
            }
        }

        /**
         * <p>
         * Determine if another node is alive. This method has the side effect of
         * setting up a connection to the remote node (if possible). Only a single
         * outgoing message is sent; the timeout is how long to wait for a response.
         * </p>
         * 
         * <p>
         * Only a single attempt is made to connect to the remote node, so for
         * example it is not possible to specify an extremely long timeout and
         * expect to be notified when the node eventually comes up. If you wish to
         * wait for a remote node to be started, the following construction may be
         * useful:
         * </p>
         * 
         * <pre>
         * // ping every 2 seconds until positive response
         * while (!me.ping(him, 2000))
         *     ;
         * </pre>
         * 
         * @param node
         *            the name of the node to ping.
         * 
         * @param timeout
         *            the time, in milliseconds, to wait for response before
         *            returning false.
         * 
         * @return true if the node was alive and the correct ping response was
         *         returned. false if the correct response was not returned on time.
         */
        /*
         * internal info about the message formats...
         * 
         * the request: -> REG_SEND {6,#Pid<bingo@aule.1.0>,'',net_kernel}
         * {'$gen_call',{#Pid<bingo@aule.1.0>,#Ref<bingo@aule.2>},{is_auth,bingo@aule}}
         * 
         * the reply: <- SEND {2,'',#Pid<bingo@aule.1.0>} {#Ref<bingo@aule.2>,yes}
         */
        public bool ping(String node, long timeout)
        {
            if (node.Equals(this.Node))
            {
                return true;
            }
            else if (node.IndexOf('@', 0) < 0 && node.Equals(this.Node.Substring(0, this.Node.IndexOf('@', 0))))
            {
                return true;
            }

            // other node
            OtpMbox mbox = null;
            try
            {
                mbox = createMbox(true);
                mbox.send("net_kernel", node, getPingTuple(mbox));
                OtpErlangObject reply = mbox.receive(timeout);
                OtpErlangTuple t = (OtpErlangTuple)reply;
                OtpErlangAtom a = (OtpErlangAtom)t.elementAt(1);
                return "yes".Equals(a.atomValue());
            }
            catch (Exception)
            {
            }
            finally
            {
                closeMbox(mbox);
            }
            return false;
        }

        /* create the outgoing ping message */
        private OtpErlangTuple getPingTuple(OtpMbox mbox)
        {
            OtpErlangObject[] ping = new OtpErlangObject[3];
            OtpErlangObject[] pid = new OtpErlangObject[2];
            OtpErlangObject[] node = new OtpErlangObject[2];

            pid[0] = mbox.Self;
            pid[1] = createRef();

            node[0] = new OtpErlangAtom("is_auth");
            node[1] = new OtpErlangAtom(Node);

            ping[0] = new OtpErlangAtom("$gen_call");
            ping[1] = new OtpErlangTuple(pid);
            ping[2] = new OtpErlangTuple(node);

            return new OtpErlangTuple(ping);
        }

        /*
         * this method simulates net_kernel only for the purpose of replying to
         * pings.
         */
        private bool netKernel(OtpMsg m)
        {
            OtpMbox mbox = null;
            try
            {
                OtpErlangTuple t = (OtpErlangTuple)m.getMsg();
                OtpErlangTuple req = (OtpErlangTuple)t.elementAt(1); // actual
                // request

                OtpErlangPid pid = (OtpErlangPid)req.elementAt(0); // originating
                // pid

                OtpErlangObject[] pong = new OtpErlangObject[2];
                pong[0] = req.elementAt(1); // his #Ref
                pong[1] = new OtpErlangAtom("yes");

                mbox = createMbox(true);
                mbox.send(pid, new OtpErlangTuple(pong));
                return true;
            }
            catch (Exception)
            {
            }
            finally
            {
                closeMbox(mbox);
            }
            return false;
        }

        /*
         * OtpCookedConnection delivers messages here return true if message was
         * delivered successfully, or false otherwise.
         */
        public bool deliver(OtpMsg m)
        {
            OtpMbox mbox = null;

            try
            {
                int t = m.type();

                if (t == OtpMsg.regSendTag)
                {
                    String name = m.getRecipientName();
                    /* special case for netKernel requests */
                    if (name.Equals("net_kernel"))
                    {
                        return netKernel(m);
                    }
                    else
                    {
                        mbox = mboxes.get(name);
                    }
                }
                else
                {
                    mbox = mboxes.get(m.getRecipientPid());
                }

                if (mbox == null)
                {
                    return false;
                }
                mbox.deliver(m);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        /*
         * OtpCookedConnection delivers errors here, we send them on to the handler
         * specified by the application
         */
        public void deliverError(OtpCookedConnection conn, Exception e)
        {
            removeConnection(conn);
            remoteStatus(conn.Name, false, e);
        }

        public void react(OtpActor actor)
        {
            sched.react(actor);
        }

        /*
         * find or create a connection to the given node
         */
        public OtpCookedConnection getConnection(String node)
        {
            OtpPeer peer = null;
            OtpCookedConnection conn = null;

            lock (connections)
            {
                // first just try looking up the name as-is

                if (connections.ContainsKey(node))
                {
                    conn = connections[node];
                }
                else
                {
                    // in case node had no '@' add localhost info and try again
                    peer = new OtpPeer(node);

                    if (connections.ContainsKey(peer.Node))
                    {
                        conn = connections[peer.Node];
                    }
                    else
                    {
                        try
                        {
                            conn = new OtpCookedConnection(this, peer);
                            conn.setFlags(flags);
                            addConnection(conn);
                        }
                        catch (Exception e)
                        {
                            /* false = outgoing */
                            connAttempt(peer.Node, false, e);
                        }
                    }
                }
                return conn;
            }
        }

        void addConnection(OtpCookedConnection conn)
        {
            if (conn != null && conn.Name != null)
            {
                connections.Add(conn.Name, conn);
                remoteStatus(conn.Name, true, null);
            }
        }

        private void removeConnection(OtpCookedConnection conn)
        {
            if (conn != null && conn.Name != null)
            {
                connections.Remove(conn.Name);
            }
        }

        /* use these wrappers to call handler functions */
        private void remoteStatus(String node, bool up, Object info)
        {
            lock (this)
            {
                if (handler == null)
                {
                    return;
                }
                try
                {
                    handler.remoteStatus(node, up, info);
                }
                catch (Exception)
                {
                }
            }
        }

        internal void localStatus(String node, bool up, Object info)
        {
            lock (this)
            {
                if (handler == null)
                {
                    return;
                }
                try
                {
                    handler.localStatus(node, up, info);
                }
                catch (Exception)
                {
                }
            }
        }

        internal void connAttempt(String node, bool incoming, Object info)
        {
            lock (this)
            {
                if (handler == null)
                {
                    return;
                }
                try
                {
                    handler.connAttempt(node, incoming, info);
                }
                catch (Exception)
                {
                }
            }
        }

        /*
         * this class used to wrap the mailbox hashtables so we can use weak
         * references
         */
        public class Mailboxes
        {
            private readonly OtpNode node;
            private readonly OtpActorSched sched;

            // mbox pids here
            private Dictionary<OtpErlangPid, WeakReference> byPid = null;
            // mbox names here
            private Dictionary<String, WeakReference> byName = null;

            public Mailboxes(OtpNode node, OtpActorSched sched)
            {
                this.node = node;
                this.sched = sched;
                byPid = new Dictionary<OtpErlangPid, WeakReference>();
                byName = new Dictionary<String, WeakReference>();
            }

            public OtpMbox create(String name, bool sync)
            {
                OtpMbox m = null;

                lock (this)
                {
                    if (get(name) != null)
                    {
                        return null;
                    }
                    OtpErlangPid pid = node.createPid();
                    m = sync ? new OtpMbox(node, pid, name) : new OtpActorMbox(sched, node, pid, name);
                    byPid.Add(pid, new WeakReference(m));
                    byName.Add(name, new WeakReference(m));
                }
                return m;
            }

            public OtpMbox create(bool sync)
            {
                OtpErlangPid pid = node.createPid();
                OtpMbox m = sync ? new OtpMbox(node, pid) : new OtpActorMbox(sched, node, pid);
                lock (this)
                {
                    byPid.Add(pid, new WeakReference(m));
                }
                return m;
            }

            public void clear()
            {
                lock (this)
                {
                    byPid.Clear();
                    byName.Clear();
                }
            }

            public String[] names()
            {
                String[] allnames = null;

                lock (this)
                {
                    int n = byName.Count;
                    allnames = new String[n];

                    int i = 0;
                    foreach (string key in byName.Keys)
                    {
                        allnames[i++] = key;
                    }
                }
                return allnames;
            }

            public bool register(String name, OtpMbox mbox)
            {
                lock (this)
                {
                    if (name == null)
                    {
                        if (mbox.Name != null)
                        {
                            byName.Remove(mbox.Name);
                            mbox.Name = null;
                        }
                    }
                    else
                    {
                        if (get(name) != null)
                        {
                            return false;
                        }
                        byName.Add(name, new WeakReference(mbox));
                        mbox.Name = name;
                    }
                }
                return true;
            }

            /*
             * look up a mailbox based on its name. If the mailbox has gone out of
             * scope we also remove the reference from the hashtable so we don't
             * find it again.
             */
            public OtpMbox get(String name)
            {
                lock (this)
                {
                    if (byName.ContainsKey(name))
                    {
                        WeakReference wr = byName[name];
                        OtpMbox m = (OtpMbox)wr.Target;

                        if (m != null)
                        {
                            return m;
                        }
                        byName.Remove(name);
                    }
                    return null;
                }
            }

            /*
             * look up a mailbox based on its pid. If the mailbox has gone out of
             * scope we also remove the reference from the hashtable so we don't
             * find it again.
             */
            public OtpMbox get(OtpErlangPid pid)
            {
                lock (this)
                {
                    if (byPid.ContainsKey(pid))
                    {
                        WeakReference wr = byPid[pid];
                        OtpMbox m = (OtpMbox)wr.Target;

                        if (m != null)
                        {
                            return m;
                        }
                        byPid.Remove(pid);
                    }
                    return null;
                }
            }

            public void remove(OtpMbox mbox)
            {
                lock (this)
                {
                    byPid.Remove(mbox.Self);
                    if (mbox.Name != null)
                    {
                        byName.Remove(mbox.Name);
                    }
                }
            }
        }

        /*
         * this thread simply listens for incoming connections
         */
        public class Acceptor : ThreadBase
        {
            private readonly OtpNode node;
            private readonly TcpListener sock;
            private readonly int port;
            private volatile bool done = false;

            public Acceptor(OtpNode node, int port)
                : base("OtpNode.Acceptor", true)
            {
                this.node = node;

                sock = new TcpListener(new IPEndPoint(IPAddress.Any, port));
                sock.Start();
                this.port = ((IPEndPoint)sock.LocalEndpoint).Port;
                node.port = this.port;
                publishPort();
                base.start();
            }

            private bool publishPort()
            {
                if (node.getEpmd() != null)
                {
                    return false; // already published
                }
                OtpEpmd.publishPort(node);
                return true;
            }

            private void unPublishPort()
            {
                // unregister with epmd
                OtpEpmd.unPublishPort(node);

                // close the local descriptor (if we have one)
                closeSock(node.getEpmd());
                node.setEpmd(null);
            }

            public void quit()
            {
                unPublishPort();
                done = true;
                closeSock(sock);
                node.localStatus(node.Node, false, null);
            }

            private void closeSock(TcpClient s)
            {
                try
                {
                    if (s != null)
                    {
                        s.Close();
                    }
                }
                catch (Exception)
                {
                }
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

            public int Port
            {
                get { return port; }
            }

            public override void run()
            {
                TcpClient newsock = null;
                OtpCookedConnection conn = null;

                node.localStatus(node.Node, true, null);

            accept_loop: while (!done)
                {
                    conn = null;

                    try
                    {
                        newsock = sock.AcceptTcpClient();
                    }
                    catch (Exception e)
                    {
                        // Problem in java1.2.2: accept throws SocketException
                        // when socket is closed. This will happen when
                        // acceptor.quit()
                        // is called. acceptor.quit() will call localStatus(...), so
                        // we have to check if that's where we come from.
                        if (!done)
                        {
                            node.localStatus(node.Node, false, e);
                        }
                        goto accept_loop;
                    }

                    try
                    {
                        lock (node.Connections)
                        {
                            conn = new OtpCookedConnection(node, new BufferedTcpClient(newsock));
                            conn.setFlags(node.Flags);
                            node.addConnection(conn);
                        }
                    }
                    catch (OtpAuthException e)
                    {
                        if (conn != null && conn.Name != null)
                        {
                            node.connAttempt(conn.Name, true, e);
                        }
                        else
                        {
                            node.connAttempt("unknown", true, e);
                        }
                        closeSock(newsock);
                    }
                    catch (IOException e)
                    {
                        if (conn != null && conn.Name != null)
                        {
                            node.connAttempt(conn.Name, true, e);
                        }
                        else
                        {
                            node.connAttempt("unknown", true, e);
                        }
                        closeSock(newsock);
                    }
                    catch (Exception e)
                    {
                        closeSock(newsock);
                        closeSock(sock);
                        node.localStatus(node.Node, false, e);
                        goto accept_loop;
                    }
                } // while

                // if we have exited loop we must do this too
                unPublishPort();
            }
        }

        public void setFlags(int flags)
        {
            this.flags = flags;
        }
    }
}
