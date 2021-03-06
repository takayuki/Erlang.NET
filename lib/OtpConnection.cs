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
using System.Net.Sockets;

namespace Erlang.NET
{
    /**
     * Maintains a connection between a Java process and a remote Erlang, Java or C
     * node. The object maintains connection state and allows data to be sent to and
     * received from the peer.
     * 
     * <p>
     * Once a connection is established between the local node and a remote node,
     * the connection object can be used to send and receive messages between the
     * nodes and make rpc calls (assuming that the remote node is a real Erlang
     * node).
     * 
     * <p>
     * The various receive methods are all blocking and will return only when a
     * valid message has been received or an exception is raised.
     * 
     * <p>
     * If an exception occurs in any of the methods in this class, the connection
     * will be closed and must be explicitely reopened in order to resume
     * communication with the peer.
     * 
     * <p>
     * It is not possible to create an instance of this class directly.
     * OtpConnection objects are returned by {@link OtpSelf#connect(OtpPeer)
     * OtpSelf.connect()} and {@link OtpSelf#accept() OtpSelf.accept()}.
     */
    public class OtpConnection : AbstractConnection
    {
        protected new OtpSelf self;
        protected GenericQueue queue; // messages get delivered here

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
        internal OtpConnection(OtpSelf self, BufferedTcpClient s)
            : base(self, s)
        {
            this.self = self;
            queue = new GenericQueue();
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
        internal OtpConnection(OtpSelf self, OtpPeer other)
            : base(self, other)
        {
            this.self = self;
            queue = new GenericQueue();
            start();
        }

        public override void deliver(Exception e)
        {
            queue.put(e);
        }

        public override void deliver(OtpMsg msg)
        {
            queue.put(msg);
        }

        /**
         * Get information about the node at the peer end of this connection.
         * 
         * @return the {@link OtpPeer Node} representing the peer node.
         */
        public OtpPeer Peer
        {
            get { return peer; }
        }

        /**
         * Get information about the node at the local end of this connection.
         * 
         * @return the {@link OtpSelf Node} representing the local node.
         */
        public OtpSelf Self
        {
            get { return self; }
        }

        /**
         * Return the number of messages currently waiting in the receive queue for
         * this connection.
         */
        public int msgCount()
        {
            return queue.getCount();
        }

        /**
         * Receive a message from a remote process. This method blocks until a valid
         * message is received or an exception is raised.
         * 
         * <p>
         * If the remote node sends a message that cannot be decoded properly, the
         * connection is closed and the method throws an exception.
         * 
         * @return an object containing a single Erlang term.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         * 
         * @exception OtpErlangExit
         *                    if an exit signal is received from a process on the
         *                    peer node.
         * 
         * @exception OtpAuthException
         *                    if the remote node sends a message containing an
         *                    invalid cookie.
         */
        public OtpErlangObject receive()
        {
            try
            {
                return receiveMsg().getMsg();
            }
            catch (OtpErlangDecodeException e)
            {
                close();
                throw new IOException(e.Message);
            }
        }

        /**
         * Receive a message from a remote process. This method blocks at most for
         * the specified time, until a valid message is received or an exception is
         * raised.
         * 
         * <p>
         * If the remote node sends a message that cannot be decoded properly, the
         * connection is closed and the method throws an exception.
         * 
         * @param timeout
         *                the time in milliseconds that this operation will block.
         *                Specify 0 to poll the queue.
         * 
         * @return an object containing a single Erlang term.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         * 
         * @exception OtpErlangExit
         *                    if an exit signal is received from a process on the
         *                    peer node.
         * 
         * @exception OtpAuthException
         *                    if the remote node sends a message containing an
         *                    invalid cookie.
         * 
         * @exception InterruptedException
         *                    if no message if the method times out before a message
         *                    becomes available.
         */
        public OtpErlangObject receive(long timeout)
        {
            try
            {
                return receiveMsg(timeout).getMsg();
            }
            catch (OtpErlangDecodeException e)
            {
                close();
                throw new IOException(e.Message);
            }
        }

        /**
         * Receive a raw (still encoded) message from a remote process. This message
         * blocks until a valid message is received or an exception is raised.
         * 
         * <p>
         * If the remote node sends a message that cannot be decoded properly, the
         * connection is closed and the method throws an exception.
         * 
         * @return an object containing a raw (still encoded) Erlang term.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         * 
         * @exception OtpErlangExit
         *                    if an exit signal is received from a process on the
         *                    peer node, or if the connection is lost for any
         *                    reason.
         * 
         * @exception OtpAuthException
         *                    if the remote node sends a message containing an
         *                    invalid cookie.
         */
        public OtpInputStream receiveBuf()
        {
            return receiveMsg().getMsgBuf();
        }

        /**
         * Receive a raw (still encoded) message from a remote process. This message
         * blocks at most for the specified time until a valid message is received
         * or an exception is raised.
         * 
         * <p>
         * If the remote node sends a message that cannot be decoded properly, the
         * connection is closed and the method throws an exception.
         * 
         * @param timeout
         *                the time in milliseconds that this operation will block.
         *                Specify 0 to poll the queue.
         * 
         * @return an object containing a raw (still encoded) Erlang term.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         * 
         * @exception OtpErlangExit
         *                    if an exit signal is received from a process on the
         *                    peer node, or if the connection is lost for any
         *                    reason.
         * 
         * @exception OtpAuthException
         *                    if the remote node sends a message containing an
         *                    invalid cookie.
         * 
         * @exception InterruptedException
         *                    if no message if the method times out before a message
         *                    becomes available.
         */
        public OtpInputStream receiveBuf(long timeout)
        {
            return receiveMsg(timeout).getMsgBuf();
        }

        /**
         * Receive a messge complete with sender and recipient information.
         * 
         * @return an {@link OtpMsg OtpMsg} containing the header information about
         *         the sender and recipient, as well as the actual message contents.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         * 
         * @exception OtpErlangExit
         *                    if an exit signal is received from a process on the
         *                    peer node, or if the connection is lost for any
         *                    reason.
         * 
         * @exception OtpAuthException
         *                    if the remote node sends a message containing an
         *                    invalid cookie.
         */
        public OtpMsg receiveMsg()
        {
            Object o = queue.get();

            if (o is OtpMsg)
            {
                return (OtpMsg)o;
            }
            else if (o is IOException)
            {
                throw (IOException)o;
            }
            else if (o is OtpErlangExit)
            {
                throw (OtpErlangExit)o;
            }
            else if (o is OtpAuthException)
            {
                throw (OtpAuthException)o;
            }

            return null;
        }

        /**
         * Receive a messge complete with sender and recipient information. This
         * method blocks at most for the specified time.
         * 
         * @param timeout
         *                the time in milliseconds that this operation will block.
         *                Specify 0 to poll the queue.
         * 
         * @return an {@link OtpMsg OtpMsg} containing the header information about
         *         the sender and recipient, as well as the actual message contents.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         * 
         * @exception OtpErlangExit
         *                    if an exit signal is received from a process on the
         *                    peer node, or if the connection is lost for any
         *                    reason.
         * 
         * @exception OtpAuthException
         *                    if the remote node sends a message containing an
         *                    invalid cookie.
         * 
         * @exception InterruptedException
         *                    if no message if the method times out before a message
         *                    becomes available.
         */
        public OtpMsg receiveMsg(long timeout)
        {
            Object o = queue.get(timeout);

            if (o is OtpMsg)
            {
                return (OtpMsg)o;
            }
            else if (o is IOException)
            {
                throw (IOException)o;
            }
            else if (o is OtpErlangExit)
            {
                throw (OtpErlangExit)o;
            }
            else if (o is OtpAuthException)
            {
                throw (OtpAuthException)o;
            }

            return null;
        }

        /**
         * Send a message to a process on a remote node.
         * 
         * @param dest
         *                the Erlang PID of the remote process.
         * @param msg
         *                the message to send.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         */
        public void send(OtpErlangPid dest, OtpErlangObject msg)
        {
            // encode and send the message
            base.sendBuf(self.Pid, dest, new OtpOutputStream(msg));
        }

        /**
         * Send a message to a named process on a remote node.
         * 
         * @param dest
         *                the name of the remote process.
         * @param msg
         *                the message to send.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         */
        public void send(String dest, OtpErlangObject msg)
        {
            // encode and send the message
            base.sendBuf(self.Pid, dest, new OtpOutputStream(msg));
        }

        /**
         * Send a pre-encoded message to a named process on a remote node.
         * 
         * @param dest
         *                the name of the remote process.
         * @param payload
         *                the encoded message to send.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         */
        public void sendBuf(String dest, OtpOutputStream payload)
        {
            base.sendBuf(self.Pid, dest, payload);
        }

        /**
         * Send a pre-encoded message to a process on a remote node.
         * 
         * @param dest
         *                the Erlang PID of the remote process.
         * @param msg
         *                the encoded message to send.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         */
        public void sendBuf(OtpErlangPid dest, OtpOutputStream payload)
        {
            base.sendBuf(self.Pid, dest, payload);
        }

        /**
         * Send an RPC request to the remote Erlang node. This convenience function
         * creates the following message and sends it to 'rex' on the remote node:
         * 
         * <pre>
         * { self, { call, Mod, Fun, Args, user } }
         * </pre>
         * 
         * <p>
         * Note that this method has unpredicatble results if the remote node is not
         * an Erlang node.
         * </p>
         * 
         * @param mod
         *                the name of the Erlang module containing the function to
         *                be called.
         * @param fun
         *                the name of the function to call.
         * @param args
         *                an array of Erlang terms, to be used as arguments to the
         *                function.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         */
        public void sendRPC(String mod, String fun, OtpErlangObject[] args)
        {
            sendRPC(mod, fun, new OtpErlangList(args));
        }

        /**
         * Send an RPC request to the remote Erlang node. This convenience function
         * creates the following message and sends it to 'rex' on the remote node:
         * 
         * <pre>
         * { self, { call, Mod, Fun, Args, user } }
         * </pre>
         * 
         * <p>
         * Note that this method has unpredicatble results if the remote node is not
         * an Erlang node.
         * </p>
         * 
         * @param mod
         *                the name of the Erlang module containing the function to
         *                be called.
         * @param fun
         *                the name of the function to call.
         * @param args
         *                a list of Erlang terms, to be used as arguments to the
         *                function.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         */
        public void sendRPC(String mod, String fun, OtpErlangList args)
        {
            OtpErlangObject[] rpc = new OtpErlangObject[2];
            OtpErlangObject[] call = new OtpErlangObject[5];

            /* {self, { call, Mod, Fun, Args, user}} */

            call[0] = new OtpErlangAtom("call");
            call[1] = new OtpErlangAtom(mod);
            call[2] = new OtpErlangAtom(fun);
            call[3] = args;
            call[4] = new OtpErlangAtom("user");

            rpc[0] = self.Pid;
            rpc[1] = new OtpErlangTuple(call);

            send("rex", new OtpErlangTuple(rpc));
        }

        /**
         * Receive an RPC reply from the remote Erlang node. This convenience
         * function receives a message from the remote node, and expects it to have
         * the following format:
         * 
         * <pre>
         * { rex, Term }
         * </pre>
         * 
         * @return the second element of the tuple if the received message is a
         *         two-tuple, otherwise null. No further error checking is
         *         performed.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         * 
         * @exception OtpErlangExit
         *                    if an exit signal is received from a process on the
         *                    peer node.
         * 
         * @exception OtpAuthException
         *                    if the remote node sends a message containing an
         *                    invalid cookie.
         */
        public OtpErlangObject receiveRPC()
        {
            OtpErlangObject msg = receive();

            if (msg is OtpErlangTuple)
            {
                OtpErlangTuple t = (OtpErlangTuple)msg;
                if (t.arity() == 2)
                {
                    return t.elementAt(1); // obs: second element
                }
            }

            return null;
        }

        /**
         * Create a link between the local node and the specified process on the
         * remote node. If the link is still active when the remote process
         * terminates, an exit signal will be sent to this connection. Use
         * {@link #unlink unlink()} to remove the link.
         * 
         * @param dest
         *                the Erlang PID of the remote process.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         */
        public void link(OtpErlangPid dest)
        {
            base.sendLink(self.Pid, dest);
        }

        /**
         * Remove a link between the local node and the specified process on the
         * remote node. This method deactivates links created with
         * {@link #link link()}.
         * 
         * @param dest
         *                the Erlang PID of the remote process.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         */
        public void unlink(OtpErlangPid dest)
        {
            base.sendUnlink(self.Pid, dest);
        }

        /**
         * Send an exit signal to a remote process.
         * 
         * @param dest
         *                the Erlang PID of the remote process.
         * @param reason
         *                an Erlang term describing the exit reason.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         */
        public void exit(OtpErlangPid dest, OtpErlangObject reason)
        {
            base.sendExit2(self.Pid, dest, reason);
        }
    }
}
