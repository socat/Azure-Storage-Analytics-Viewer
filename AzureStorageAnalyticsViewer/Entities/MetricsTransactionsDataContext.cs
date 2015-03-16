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

        public MetricsTransactionsDataContext(string baseAddress, Microsoft.WindowsAzure.StorageCredentials credentials,
            bool useMinutes = false)
            : base(baseAddress, credentials)
        {
            this.useMinutesMetrics = useMinutes;
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

        public IQueryable<MetricsTransactionsEntity> MetricsTransactionsBlob
        {
            get
            {
                return this.CreateQuery<MetricsTransactionsEntity>(this.MetricsBlobNamespace);
            }
        }

        public IQueryable<MetricsTransactionsEntity> MetricsTransactionsTable
        {
            get
            {
                return this.CreateQuery<MetricsTransactionsEntity>(this.MetricsTableNamespace);
            }
        }

        public IQueryable<MetricsTransactionsEntity> MetricsTransactionsQueue
        {
            get
            {
                return this.CreateQuery<MetricsTransactionsEntity>(this.MetricsQueueNamespace);
            }
        }
    }
}
