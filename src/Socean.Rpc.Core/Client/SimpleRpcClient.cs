﻿using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Socean.Rpc.Core.Message;

namespace Socean.Rpc.Core.Client
{
    public sealed class SimpleRpcClient: TcpTransportHostBase,IClient
    {
        public IPAddress ServerIP { get; }
        public int ServerPort { get; }

        private volatile int _messageId = 0;
        private readonly object _queryKey = new object();
        private volatile TcpTransport _transport;
        private readonly QueryContext _queryContext;

        public SimpleRpcClient(IPAddress ip, int port)
        {
            ServerIP = ip;
            ServerPort = port;

            _transport = new TcpTransport(this, ServerIP, ServerPort);
            _queryContext = new QueryContext();
        }

        public async Task<FrameData> QueryAsync(string title, byte[] contentBytes, byte[] extentionBytes = null, bool throwIfErrorResponseCode = false)
        {
            if (_transport == null)
                throw new Exception("QueryAsync failed,connection has been closed");

            if (string.IsNullOrEmpty(title))
                throw new Exception("QueryAsync failed,title is null");

            if (title.Length > 65535)
                throw new Exception("QueryAsync failed,title length error");

            var messageId = Interlocked.Increment(ref _messageId);

            lock (_queryKey)
            {
                CheckConnection();

                _queryContext.Reset(messageId);

                //if (NetworkSettings.ServerTcpSendMode == TcpSendMode.Async)
                //    _transport.AsyncSend(extentionBytes, title, contentBytes, 0, messageId);
                //else
                    _transport.Send(extentionBytes,title, contentBytes, 0, messageId);
            }

            var receiveData = await _queryContext.WaitForResultAsync(messageId);
            if (receiveData == null)
            {
                _transport.Close();
                throw new Exception("QueryAsync failed, time is out");
            }

            if (throwIfErrorResponseCode)
            {
                var stateCode = receiveData.StateCode;
                if (stateCode != ResponseCode.OK)
                    throw new Exception("QueryAsync failed,error code:" + stateCode);
            }

            return receiveData;
        }

        public FrameData Query(string title, byte[] contentBytes, byte[] extentionBytes = null, bool throwIfErrorResponseCode = false)
        {
            if (_transport == null)
                throw new Exception("query failed,connection has been closed");

            return QueryInternal(title, contentBytes, extentionBytes, throwIfErrorResponseCode);
        }

        private void CheckConnection()
        {
            if ( _transport.IsSocketConnected == false)
            {
                try
                {
                    _transport.Close();
                }
                catch
                {

                }
            }

            if (_transport.State == -1)
            {
                _transport = new TcpTransport(this, ServerIP,ServerPort);
                _transport.Init();
            }

            if (_transport.State == 0)
            {
                _transport.Init();
            }
        }

        private FrameData QueryInternal(string title, byte[] contentBytes, byte[] extentionBytes, bool throwIfErrorResponseCode)
        {
            if (string.IsNullOrEmpty(title))
                throw new Exception("query failed,title is null");

            if (title.Length > 65535)
                throw new Exception("query failed,title length error");

            var messageId = Interlocked.Increment(ref _messageId);

            lock (_queryKey)
            {
                CheckConnection();

                _queryContext.Reset(messageId);

                //if (NetworkSettings.TcpRequestSendMode == TcpSendMode.Async)
                //    _transport.AsyncSend(extentionBytes,title, contentBytes, 0, messageId);
                //else
                _transport.Send(extentionBytes,title, contentBytes, 0, messageId);
            }

            var receiveData = _queryContext.WaitForResult(messageId);
            if (receiveData == null)
            {
                _transport.Close();
                throw new Exception("query failed,time is out");
            }

            if (throwIfErrorResponseCode)
            {
                var stateCode = receiveData.StateCode;
                if (stateCode != ResponseCode.OK)
                    throw new Exception("query failed,error code:" + stateCode);
            }

            return receiveData;
        }

        public void Close()
        {
            try
            {
                _transport.Close();
            }
            catch
            {

            }

            _transport = null;
        }

        public void Dispose()
        {
            Close();
        }

        internal override void ReceiveMessage(TcpTransport tcpTransport, FrameData frameData)
        {
            _queryContext.OnReceive(frameData);
        }

        internal override void CloseTransport(TcpTransport tcpTransport)
        {

        }
    }
}
