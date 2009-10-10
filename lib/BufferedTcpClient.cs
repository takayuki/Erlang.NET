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
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Erlang.NET
{
    public class BufferedTcpClient : IDisposable
    {
        private readonly TcpClient client;
        private readonly Stream inputStream;
        private readonly Stream outputStream;

        public BufferedTcpClient(TcpClient client)
        {
            this.client = client;
            this.inputStream = new BufferedStream(this.client.GetStream());
            this.outputStream = this.client.GetStream();

            KeepAlive = true;
        }

        public bool NoDelay
        {
            get
            {
                return (bool)client.Client.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay);
            }
            set
            {
                client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, value);
            }
        }

        public bool KeepAlive
        {
            get
            {
                return (bool)client.Client.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive);
            }
            set
            {
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, value);
            }
        }

        public Socket Client
        {
            get { return client.Client; }
        }

        public Stream GetInputStream()
        {
            return inputStream;
        }

        public Stream GetOutputStream()
        {
            return outputStream;
        }

        public void Close()
        {
            client.GetStream().Close();
            client.Close();
        }

        public void Dispose()
        {
        }
    }
}
