using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.StorageClient;

namespace AzureStorageAnalyticsViewer.Entities
{
    public class MetricsTransactionsDataContext:TableServiceContext
    {

        private bool useMinutesMetrics = false;
        private StorageType storageType = StorageType.Table;

        public MetricsTransactionsDataContext(string baseAddress, Microsoft.WindowsAzure.StorageCredentials credentials,
            bool useMinutes = false, StorageType storage = StorageType.Table)
            : base(baseAddress, credentials)
        {
            this.useMinutesMetrics = useMinutes;
            this.storageType = storage;
        }


        /* 
         https:// technet.microsoft.com/en-us/subscriptions/downloads/hh343258.aspx
   
         
        */


        internal string metricsBlobBaseFormatString = "$Metrics{0}PrimaryTransactionsBlob";
        internal string metricsTableBaseFormatString = "$Metrics{0}PrimaryTransactionsTable";
        internal string metricsQueueBaseFormatString = "$Metrics{0}PrimaryTransactionsQueue";
        internal string MetricsBlobNamespace { get { return string.Format(metricsBlobBaseFormatString, this.useMinutesMetrics ? "Minute" : "Hour"); } }
        internal string MetricsTableNamespace { get { return string.Format(metricsTableBaseFormatString, this.useMinutesMetrics ? "Minute" : "Hour"); } }
        internal string MetricsQueueNamespace { get { return string.Format(metricsQueueBaseFormatString, this.useMinutesMetrics ? "Minute" : "Hour"); } }

        public IQueryable<MetricsTransactionsEntity> MetricsTransaction
        {
            get
            {
                string metricsNamespace = this.MetricsTableNamespace;
                if (storageType == StorageType.Blob)
                {
                    metricsNamespace = this.MetricsBlobNamespace;
                }
                else if (storageType == StorageType.Queue)
                {
                    metricsNamespace = this.MetricsQueueNamespace;
                }
                return this.CreateQuery<MetricsTransactionsEntity>(metricsNamespace);
            }
        }
        
    }
}
