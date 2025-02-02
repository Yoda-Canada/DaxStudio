﻿
using ADOTabular.Enums;
using DaxStudio.Common.Enums;
using DaxStudio.Interfaces;
using DaxStudio.QueryTrace.Interfaces;
using Microsoft.AspNet.SignalR.Client;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DaxStudio.QueryTrace
{
    public class RemoteQueryTraceEngine: IQueryTrace
    {
        HubConnection hubConnection;
        IHubProxy queryTraceHubProxy;
        QueryTraceStatus _status = QueryTraceStatus.Stopped;
        private readonly List<DaxStudioTraceEventClass> _eventsToCapture;
        private readonly string _powerBIFileName = string.Empty;
        private readonly string _suffix = string.Empty;
        public RemoteQueryTraceEngine(IConnectionManager connectionManager, List<DaxStudioTraceEventClass> events, int port, IGlobalOptions globalOptions, bool filterForCurrentSession, string powerBIFileName, string suffix)
        {
            Log.Debug("{{class} {method} {message}","RemoteQueryTraceEngine","constructor", "entered");
            // connect to hub
            hubConnection = new HubConnection(string.Format("http://localhost:{0}/",port));
            queryTraceHubProxy = hubConnection.CreateHubProxy("QueryTrace");
            _eventsToCapture = events;
            _powerBIFileName = powerBIFileName;
            _suffix = suffix;
            // ==== DEBUG LOGGING =====
            //var writer = new System.IO.StreamWriter(@"d:\temp\SignalR_ClientLog.txt");
            //writer.AutoFlush = true;
            //hubConnection.TraceLevel = TraceLevels.All;
            //hubConnection.TraceWriter = writer;
            
            queryTraceHubProxy.On("OnTraceStarted", OnTraceStarted);
            queryTraceHubProxy.On("OnTraceComplete", OnTraceComplete);
            queryTraceHubProxy.On<string>("OnTraceError", (msg) => { OnTraceError(msg); });
            queryTraceHubProxy.On<DaxStudioTraceEventArgs>("OnTraceEvent", (msg) => { OnTraceEvent(msg); });
            queryTraceHubProxy.On<string>("OnTraceWarning", (msg) => { OnTraceWarning(msg); });
            hubConnection.Start().Wait();
            // configure trace
            Log.Debug("{class} {method} {message} connectionType: {connectionType} sessionId: {sessionId} eventCount: {eventCount}", "RemoteQueryTraceEngine", "<constructor>", "about to create remote engine", connectionManager.Type.ToString(), connectionManager.SessionId, events.Count);
            queryTraceHubProxy.Invoke("ConstructQueryTraceEngine", connectionManager.Type, connectionManager.SessionId, events, filterForCurrentSession,_powerBIFileName, _suffix).Wait();
            // wire up hub events

        }

        public event EventHandler TraceCompleted;
        

        public int TraceStartTimeoutSecs { get; private set; }
        public async Task StartAsync(int startTimeoutSecs)
        {

            Log.Debug("{class} {method} {message}", "RemoteQueryTraceEngine", "StartAsync", "entered");
            TraceStartTimeoutSecs = startTimeoutSecs;
            await queryTraceHubProxy.Invoke("StartAsync", startTimeoutSecs);
        }

        public void Stop()
        {
            _status = QueryTraceStatus.Stopping;
            queryTraceHubProxy.Invoke("Stop").Wait(3000); // TODO - do we need to timeout or force here if app is closing
            _status = QueryTraceStatus.Stopped;
        }

        public void OnTraceStarted() {
            _status = QueryTraceStatus.Started;
            if (TraceStarted != null)
            { TraceStarted(this, null); }
        }
        
        public void OnTraceError(string errorMessage) { 
            if (TraceError != null) {
                TraceError(this, errorMessage);
            }
        }

        public void OnTraceEvent(DaxStudioTraceEventArgs arg )
        {
            if (TraceEvent != null)
            {
                TraceEvent(this, arg);
            }
        }

        public void OnTraceWarning(string errorMessage)
        {
            if (TraceWarning != null)
            {
                TraceWarning(this, errorMessage);
            }
        }

        public void OnTraceComplete()
        {
            TraceCompleted?.Invoke(this, null);
        }

        public void OnTraceCompleted()
        {
            TraceCompleted?.Invoke(this, null);
        }

        public List<DaxStudioTraceEventClass> Events => _eventsToCapture;


        public event EventHandler TraceStarted;

        public event EventHandler<string> TraceError;
        public event EventHandler<string> TraceWarning;
        public event EventHandler<DaxStudioTraceEventArgs> TraceEvent;

        public QueryTraceStatus Status
        {
            get
            {
                return _status;
            }
        }

        public void Update()
        {
            queryTraceHubProxy.Invoke("UpdateEvents", _eventsToCapture).Wait();
            queryTraceHubProxy.Invoke("Update");
        }

        public void Dispose()
        {
            queryTraceHubProxy.Invoke("Dispose");
        }

        public void Update(string databaseName, string sessionId)
        {
            // we don't use the databaseName in the Remote query trace engine  (PowerPivot)
            Update();
        }
    }
}
