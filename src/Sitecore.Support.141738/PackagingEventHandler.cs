using Sitecore.Diagnostics;

namespace Sitecore.Support.ContentSearch.Events
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Sitecore.ContentSearch.Diagnostics;
    using Sitecore.ContentSearch.Maintenance;
    using Sitecore.Data;
    using Sitecore.Events;
    using Sitecore.Install.Events;
    using Sitecore.Jobs;
    using Sitecore.ContentSearch;

    /// <summary>
    /// Defines event handlers related to Packaging.
    /// </summary>
    [Serializable]
    public class PackagingEventHandler
    {
        Sitecore.ContentSearch.Events.PackagingEventHandler handler = new Sitecore.ContentSearch.Events.PackagingEventHandler();
        /// <summary>Called when the package install starting has handler.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        public void OnPackageInstallStartingHandler(object sender, EventArgs e)
        {
            this.handler.OnPackageInstallStartingHandler(sender,e);
        }

        /// <summary>Called when the package install starting has handler.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        public void OnPackagePostStepInstallStartingHandler(object sender, EventArgs e)
        {
            this.handler.OnPackagePostStepInstallStartingHandler(sender, e);
        }

        /// <summary>Called when the package installer end has handler.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        public void OnPackageInstallerEndHandler(object sender, EventArgs e)
        {
            this.handler.OnPackageInstallerEndHandler(sender, e);
        }

        /// <summary>Called when the package install items end has handler.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        public void OnPackageInstallItemsEndHandler(object sender, EventArgs e)
        {
            if (e == null)
                return;

            var sitecoreEventArgs = e as SitecoreEventArgs;

            if (sitecoreEventArgs == null || sitecoreEventArgs.Parameters == null || sitecoreEventArgs.Parameters.Length != 1)
                return;

            var installArgs = sitecoreEventArgs.Parameters[0] as InstallationEventArgs;

            if (installArgs == null || installArgs.ItemsToInstall == null)
                return;

            var installedItems = installArgs.ItemsToInstall.ToList();

            Sitecore.Support.ContentSearch.Events.PackagingEventHandler.HandleInstalledItems(installedItems);
        }

        /// <summary>
        /// Invoked on 'packageinstall:starting:remote' event.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="args">
        /// The args.
        /// </param>
        public void OnPackageInstallStartingRemoteHandler(object sender, EventArgs args)
        {
            this.handler.OnPackageInstallStartingRemoteHandler(sender, args);
        }

        /// <summary>
        /// Invoked on 'packageinstall:poststep:starting:remote' event.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="args">
        /// The args.
        /// </param>
        public void OnPackagePostStepInstallStartingRemoteHandler(object sender, EventArgs args)
        {
            CrawlingLog.Log.Warn("Resuming indexing while executing package post step action on a remote instance.");
            this.SafeExecute(IndexCustodian.ResumeIndexing);
        }

        /// <summary>
        /// Invoked on 'packageinstall:items:ended:remote' event.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="args">
        /// The args.
        /// </param>
        public void OnPackageInstallItemsEndRemoteHandler(object sender, EventArgs args)
        {
            var remoteEventArgs = args as InstallationRemoteEventArgs;
            if (remoteEventArgs == null)
            {
                return;
            }

            var installedItems = remoteEventArgs.ItemsToInstall.ToList();
            if (installedItems.Count == 0)
            {
                return;
            }

            Sitecore.Support.ContentSearch.Events.PackagingEventHandler.HandleInstalledItems(installedItems);
        }

        /// <summary>
        /// Invoked on 'packageinstall:ended:remote' event.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="args">
        /// The args.
        /// </param>
        public void OnPackageInstallerEndRemoteHandler(object sender, EventArgs args)
        {
            this.handler.OnPackageInstallerEndRemoteHandler(sender, args);
        }

        /// <summary>
        /// Handles installed items.
        /// </summary>
        /// <param name="installedItems">
        /// The installed items.
        /// </param>
        private static void HandleInstalledItems(List<ItemUri> installedItems)
        {
            CrawlingLog.Log.Info(string.Format("Updating '{0}' items from installed items.", installedItems.Count));

            var groups =
              installedItems.Where(uri=>Configuration.Factory.GetDatabaseNames().Contains(uri.DatabaseName)).Select(Database.GetItem)
                .Where(item => item != null)
                .GroupBy(item => ContentSearchManager.GetContextIndexName(new SitecoreIndexableItem(item)));

            var jobs = new List<Job>();

            foreach (var batch in groups)
            {
                if (batch == null || string.IsNullOrEmpty(batch.Key))
                {
                    //do not process items excluded from indexing, e.g. ExcludeTemplate, etc.
                    continue;
                }

                CrawlingLog.Log.Info(string.Format("[Index={0}] Updating '{1}' items from installed items.", batch.Key, batch.Count()));
                var index = ContentSearchManager.GetIndex(batch.Key);
                var job = IndexCustodian.ForcedIncrementalUpdate(index, batch.Select(item => new SitecoreItemUniqueId(item.Uri)));

                jobs.Add(job);
            }

            foreach (var job in jobs)
            {
                while (!job.IsDone)
                {
                    Thread.Sleep(100);
                }
            }

            CrawlingLog.Log.Info("Items from installed items have been indexed.");
        }

        /// <summary>
        /// Executes action and log exception in case action failed.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        private void SafeExecute(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                CrawlingLog.Log.Error("Unable to stop or resume indexing.", ex);
            }
        }
    }
}
