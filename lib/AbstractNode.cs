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
     * Represents an OTP node.
     * </p>
     * 
     * <p>
     * About nodenames: Erlang nodenames consist of two components, an alivename and
     * a hostname separated by '@'. Additionally, there are two nodename formats:
     * short and long. Short names are of the form "alive@hostname", while long
     * names are of the form "alive@host.fully.qualified.domainname". Erlang has
     * special requirements regarding the use of the short and long formats, in
     * particular they cannot be mixed freely in a network of communicating nodes,
     * however Jinterface makes no distinction. See the Erlang documentation for
     * more information about nodenames.
     * </p>
     * 
     * <p>
     * The constructors for the AbstractNode classes will create names exactly as
     * you provide them as long as the name contains '@'. If the string you provide
     * contains no '@', it will be treated as an alivename and the name of the local
     * host will be appended, resulting in a shortname. Nodenames longer than 255
     * characters will be truncated without warning.
     * </p>
     * 
     * <p>
     * Upon initialization, this class attempts to read the file .erlang.cookie in
     * the user's home directory, and uses the trimmed first line of the file as the
     * default cookie by those constructors lacking a cookie argument. If for any
     * reason the file cannot be found or read, the default cookie will be set to
     * the empty string (""). The location of a user's home directory is determined
     * using the system property "user.home", which may not be automatically set on
     * all platforms.
     * </p>
     * 
     * <p>
     * Instances of this class cannot be created directly, use one of the subclasses
     * instead.
     * </p>
     */
    public class AbstractNode
    {
        static readonly String localHost;
        String node;
        String host;
        String alive;
        String cookie;
        protected static readonly String defaultCookie;

        // Node types
        public const int NTYPE_R6 = 110; // 'n' post-r5, all nodes
        public const int NTYPE_R4_ERLANG = 109; // 'm' Only for source compatibility
        public const int NTYPE_R4_HIDDEN = 104; // 'h' Only for source compatibility

        // Node capability flags
        public const int dFlagPublished = 1;
        public const int dFlagAtomCache = 2;
        public const int dFlagExtendedReferences = 4;
        public const int dFlagDistMonitor = 8;
        public const int dFlagFunTags = 0x10;
        public const int dFlagDistMonitorName = 0x20; // NOT USED
        public const int dFlagHiddenAtomCache = 0x40; // NOT SUPPORTED
        public const int dflagNewFunTags = 0x80;
        public const int dFlagExtendedPidsPorts = 0x100;
        public const int dFlagExportPtrTag = 0x200; // NOT SUPPORTED
        public const int dFlagBitBinaries = 0x400;
        public const int dFlagNewFloats = 0x800;

        int ntype = NTYPE_R6;
        int proto = 0; // tcp/ip
        int distHigh = 5; // Cannot talk to nodes before R6
        int distLow = 5; // Cannot talk to nodes before R6
        int creation = 0;
        int flags = dFlagExtendedReferences | dFlagExtendedPidsPorts
            | dFlagBitBinaries | dFlagNewFloats | dFlagFunTags | dflagNewFunTags;

        /* initialize hostname and default cookie */
        static AbstractNode()
        {
            try
            {
                localHost = Dns.GetHostName();
                /*
                 * Make sure it's a short name, i.e. strip of everything after first
                 * '.'
                 */
                int dot = localHost.IndexOf(".");
                if (dot != -1)
                {
                    localHost = localHost.Substring(0, dot);
                }
            }
            catch (SocketException)
            {
                localHost = "localhost";
            }

            String userHome;
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                    userHome = Environment.GetEnvironmentVariable("HOME");
                    break;
                default:
                    userHome = Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
                    break;
            }
            String dotCookieFilename = Path.Combine(userHome, ".erlang.cookie");

            try
            {
                using (StreamReader sr = new StreamReader(dotCookieFilename))
                {
                    defaultCookie = sr.ReadLine().Trim();
                }
            }
            catch (DirectoryNotFoundException)
            {
                defaultCookie = "";
            }
            catch (FileNotFoundException)
            {
                defaultCookie = "";
            }
        }

        protected AbstractNode()
        {
        }

        /**
         * Create a node with the given name and the default cookie.
         */
        protected AbstractNode(String node)
            : this(node, defaultCookie)
        {
        }

        /**
         * Create a node with the given name and cookie.
         */
        protected AbstractNode(String name, String cookie)
        {
            this.cookie = cookie;

            int i = name.IndexOf('@', 0);
            if (i < 0)
            {
                alive = name;
                host = localHost;
            }
            else
            {
                alive = name.Substring(0, i);
                host = name.Substring(i + 1, name.Length - (i + 1));
            }

            if (alive.Length > 0xff)
            {
                alive = alive.Substring(0, 0xff);
            }

            node = alive + "@" + host;
        }

        public int Flags
        {
            get { return flags; }
            set { flags = value; }
        }

        /**
         * Get the name of this node.
         * 
         * @return the name of the node represented by this object.
         */
        public String Node
        {
            get { return node; }
            set { node = value; }
        }

        /**
         * Get the hostname part of the nodename. Nodenames are composed of two
         * parts, an alivename and a hostname, separated by '@'. This method returns
         * the part of the nodename following the '@'.
         * 
         * @return the hostname component of the nodename.
         */
        public String Host
        {
            get { return host; }
            set { host = value; }
        }

        /**
         * Get the alivename part of the hostname. Nodenames are composed of two
         * parts, an alivename and a hostname, separated by '@'. This method returns
         * the part of the nodename preceding the '@'.
         * 
         * @return the alivename component of the nodename.
         */
        public String Alive
        {
            get { return alive; }
            set { alive = value; }
        }

        /**
         * Get the authorization cookie used by this node.
         * 
         * @return the authorization cookie used by this node.
         */
        public String Cookie
        {
            get { return cookie; }
        }

        // package scope
        internal int Type
        {
            get { return ntype; }
            set { ntype = value; }
        }

        // package scope
        internal int DistHigh
        {
            get { return distHigh; }
            set { distHigh = value; }
        }

        // package scope
        internal int DistLow
        {
            get { return distLow; }
            set { distLow = value; }
        }

        // package scope: useless information?
        internal int Proto
        {
            get { return proto; }
            set { proto = value; }
        }

        // package scope
        internal int Creation
        {
            get { return creation; }
            set { creation = value; }
        }

        /**
         * Set the authorization cookie used by this node.
         * 
         * @return the previous authorization cookie used by this node.
         */
        public String setCookie(String cookie)
        {
            String prev = this.cookie;
            this.cookie = cookie;
            return prev;
        }

        public override String ToString()
        {
            return node;
        }
    }
}
