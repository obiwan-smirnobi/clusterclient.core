﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Vostok.Clusterclient.Core.Misc;
using Vostok.Clusterclient.Core.Model;
using Vostok.Clusterclient.Core.Ordering;
using Vostok.Clusterclient.Core.Ordering.Storage;
using Vostok.Clusterclient.Core.Sending;
using Vostok.Clusterclient.Core.Topology;
using Vostok.Logging.Abstractions;

namespace Vostok.Clusterclient.Core.Modules
{
    internal class RequestExecutionModule : IRequestModule
    {
        private readonly IClusterProvider clusterProvider;
        private readonly IReplicaOrdering replicaOrdering;
        private readonly IResponseSelector responseSelector;
        private readonly IReplicaStorageProvider storageProvider;
        private readonly IRequestSenderInternal requestSender;
        private readonly IClusterResultStatusSelector resultStatusSelector;

        public RequestExecutionModule(
            IClusterProvider clusterProvider,
            IReplicaOrdering replicaOrdering,
            IResponseSelector responseSelector,
            IReplicaStorageProvider storageProvider,
            IRequestSenderInternal requestSender,
            IClusterResultStatusSelector resultStatusSelector)
        {
            this.clusterProvider = clusterProvider;
            this.replicaOrdering = replicaOrdering;
            this.responseSelector = responseSelector;
            this.storageProvider = storageProvider;
            this.requestSender = requestSender;
            this.resultStatusSelector = resultStatusSelector;
        }

        public async Task<ClusterResult> ExecuteAsync(IRequestContext context, Func<IRequestContext, Task<ClusterResult>> next)
        {
            var replicas = clusterProvider.GetCluster();
            if (replicas == null || replicas.Count == 0)
            {
                LogReplicasNotFound(context);
                return ClusterResult.ReplicasNotFound(context.Request);
            }

            var contextImpl = (RequestContext) context;
            var contextualSender = new ContextualRequestSender(requestSender, contextImpl);

            var maxReplicasToUse = context.MaximumReplicasToUse;
            var orderedReplicas = replicaOrdering.Order(replicas, storageProvider, contextImpl.Request, contextImpl.Parameters);
            var limitedReplicas = orderedReplicas.Take(maxReplicasToUse);

            await contextImpl.Parameters.Strategy.SendAsync(
                    contextImpl.Request,
                    contextImpl.Parameters,
                    contextualSender,
                    contextImpl.Budget,
                    limitedReplicas,
                    Math.Min(replicas.Count, maxReplicasToUse),
                    context.CancellationToken)
                .ConfigureAwait(false);

            context.CancellationToken.ThrowIfCancellationRequested();

            var replicaResults = contextImpl.FreezeReplicaResults();

            var selectedResponse = responseSelector.Select(contextImpl.Request, context.Parameters, replicaResults);

            var resultStatus = resultStatusSelector.Select(replicaResults, contextImpl.Budget);

            return new ClusterResult(resultStatus, replicaResults, selectedResponse, context.Request);
        }

        #region Logging

        private void LogReplicasNotFound(IRequestContext context) =>
            context.Log.Warn("No replicas were resolved: can't send request anywhere.");

        #endregion
    }
}