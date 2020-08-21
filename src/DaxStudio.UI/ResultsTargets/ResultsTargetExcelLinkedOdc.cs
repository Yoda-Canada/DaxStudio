﻿using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using System.Diagnostics;
using DaxStudio.UI.Interfaces;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Utils;

namespace DaxStudio.UI.ResultsTargets
{
    // This is the target which writes the static results out to
    // a range in Excel
    [Export(typeof(IResultsTarget))]
    public class ResultsTargetExcelLinkedOdc: PropertyChangedBase, 
        IResultsTarget, 
        IActivateResults, 
        IHandle<ConnectionChangedEvent>,
        IHandle<ActivateDocumentEvent>
    {
        private IDaxStudioHost _host;
        private IEventAggregator _eventAggregator;
        private bool _isPowerBIOrSSDTConnection = false;

        [ImportingConstructor]
        public ResultsTargetExcelLinkedOdc(IDaxStudioHost host, IEventAggregator eventAggregator)
        {
            _host = host;
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);
        }

        #region Standard Properties
        public string Name => "Linked";
        public string Group => "Excel";
        public bool IsDefault => false;
        public bool IsAvailable => !_host.IsExcel;
        public int DisplayOrder => 100;
        public string Message => "Query will be sent to Excel for execution";
        public OutputTarget Icon => OutputTarget.Linked;
        public string Tooltip => "Sends the Query text to Excel for execution";
        public bool IsEnabled => !_isPowerBIOrSSDTConnection;

        public string DisabledReason => "Linked Excel output is not supported against Power BI Desktop or SSDT based connections";

        public void Handle(ConnectionChangedEvent message)
        {
            _isPowerBIOrSSDTConnection = message.Connection?.IsPowerBIorSSDT ?? false;
            NotifyOfPropertyChange(() => IsEnabled);
            _eventAggregator.PublishOnUIThread(new RefreshOutputTargetsEvent());
        }

        public void Handle(ActivateDocumentEvent message)
        {
            _isPowerBIOrSSDTConnection = message.Document.Connection?.IsPowerBIorSSDT ?? false;
            NotifyOfPropertyChange(() => IsEnabled);
            _eventAggregator.PublishOnUIThread(new RefreshOutputTargetsEvent());
        }
        #endregion

        public async Task OutputResultsAsync(IQueryRunner runner, IQueryTextProvider textProvider)
        {
            await Task.Run(() =>
                {
                    try
                    {
                        runner.OutputMessage("Opening .odc file in Excel");
                        var sw = Stopwatch.StartNew();
                        var dq = textProvider.QueryText;

                        // odc queries require 'mdx compatibility=1'
                        var fixedConnStr = runner.ConnectionStringWithInitialCatalog.Replace("mdx compatibility=3", "mdx compatibility=1");

                        // create odc file
                        var odcFile = OdcHelper.CreateOdcQueryFile(fixedConnStr, runner.QueryText );


                        System.Diagnostics.Process.Start(odcFile);
                        //  write results to Excel
                 

                        sw.Stop();
                        var durationMs = sw.ElapsedMilliseconds;
                     
                        runner.OutputMessage(
                            string.Format("Query Completed - Query sent to Excel for execution"), durationMs);
                        runner.OutputMessage("Note: odc files can only handle a query that returns a single result set. If you see an error try using one of the other output types to ensure your query is valid.");
                        
                        runner.ActivateOutput();
                        runner.SetResultsMessage("Query sent to Excel for execution", OutputTarget.Linked);

                    }
                    catch (Exception ex)
                    {
                        runner.ActivateOutput();
                        runner.OutputError(ex.Message);
                    }
                    finally
                    {
                        runner.QueryCompleted();
                    }
                });
        }

    }


}
