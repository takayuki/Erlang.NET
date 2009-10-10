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

namespace Erlang.NET
{
    /**
     * Represents a remote OTP node. It acts only as a container for the nodename
     * and other node-specific information that is needed by the
     * {@link OtpConnection} class.
     */
    public class OtpPeer : AbstractNode
    {
        private int distChoose = 0;

        public int DistChoose
        {
            get { return distChoose; }
            set { distChoose = value; }
        }

        /*
         * this is set by OtpConnection and is the highest
         * common protocol version we both support
         */

        public OtpPeer()
            : base()
        {
        }

        /**
         * Create a peer node.
         * 
         * @param node
         *                the name of the node.
         */
        public OtpPeer(String node)
            : base(node)
        {
        }

        /**
         * Create a connection to a remote node.
         * 
         * @param self
         *                the local node from which you wish to connect.
         * 
         * @return a connection to the remote node.
         * 
         * @exception java.net.UnknownHostException
         *                    if the remote host could not be found.
         * 
         * @exception java.io.IOException
         *                    if it was not possible to connect to the remote node.
         * 
         * @exception OtpAuthException
         *                    if the connection was refused by the remote node.
         * 
         * @deprecated Use the corresponding method in {@link OtpSelf} instead.
         */
        [Obsolete]
        public OtpConnection connect(OtpSelf self)
        {
            return new OtpConnection(self, this);
        }

        // package
        /*
         * Get the port number used by the remote node.
         * 
         * @return the port number used by the remote node, or 0 if the node was not
         * registered with the port mapper.
         * 
         * @exception java.io.IOException if the port mapper could not be contacted.
         */
        internal int port()
        {
            return OtpEpmd.lookupPort(this);
        }
    }
}
