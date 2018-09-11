﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vostok.ClusterClient.Abstractions;
using Vostok.ClusterClient.Abstractions.Criteria;
using Vostok.ClusterClient.Abstractions.Logging;
using Vostok.ClusterClient.Abstractions.Misc;
using Vostok.ClusterClient.Abstractions.Model;
using Vostok.ClusterClient.Abstractions.Modules;
using Vostok.ClusterClient.Abstractions.Ordering;
using Vostok.ClusterClient.Abstractions.Ordering.Storage;
using Vostok.ClusterClient.Abstractions.Retry;
using Vostok.ClusterClient.Abstractions.Strategies;
using Vostok.ClusterClient.Abstractions.Topology;
using Vostok.ClusterClient.Abstractions.Transforms;
using Vostok.ClusterClient.Abstractions.Transport;
using Vostok.ClusterClient.Core.Criteria;
using Vostok.ClusterClient.Core.Modules;
using Vostok.Logging.Abstractions;

namespace Vostok.ClusterClient.Core
{
    internal class ClusterClientConfiguration : IClusterClientConfiguration
    {
        public ClusterClientConfiguration(ILog log)
        {
            Log = log;
            RequestTransforms = new List<IRequestTransform>();
            ResponseTransforms = new List<IResponseTransform>();
            ResponseCriteria = new List<IResponseCriterion>();
            Modules = new Dictionary<RequestModule, List<IRequestModule>>();
            foreach (var value in (RequestModule[])Enum.GetValues(typeof(RequestModule)))
                Modules[value] = new List<IRequestModule>();
            ReplicaStorageScope = ClusterClientDefaults.ReplicaStorageScope;
            DefaultTimeout = ClusterClientDefaults.Timeout;
            Logging = new LoggingOptions
            {
                LogRequestDetails = ClusterClientDefaults.LogRequestDetails,
                LogResultDetails = ClusterClientDefaults.LogResultDetails,
                LogReplicaRequests = ClusterClientDefaults.LogReplicaRequests,
                LogReplicaResults = ClusterClientDefaults.LogReplicaResults,
                LogPrefixEnabled = ClusterClientDefaults.LogPrefixEnabled
            };
            MaxReplicasUsedPerRequest = ClusterClientDefaults.MaxReplicasUsedPerRequest;
            DeduplicateRequestUrl = ClusterClientDefaults.DeduplicateRequestUrl;
            ValidateHttpMethod = ClusterClientDefaults.ValidateHttpMethod;
        }

        public ILog Log { get; }

        public ITransport Transport { get; set; }

        public IClusterProvider ClusterProvider { get; set; }

        public IReplicaTransform ReplicaTransform { get; set; }

        public IReplicaOrdering ReplicaOrdering { get; set; }

        public ReplicaStorageScope ReplicaStorageScope { get; set; }

        public List<IRequestTransform> RequestTransforms { get; set; }

        public List<IResponseTransform> ResponseTransforms { get; set; }

        public List<IResponseCriterion> ResponseCriteria { get; set; }

        public Dictionary<RequestModule, List<IRequestModule>> Modules { get; set; }

        public IRetryPolicy RetryPolicy { get; set; }

        public IRetryStrategy RetryStrategy { get; set; }

        public IResponseSelector ResponseSelector { get; set; }

        public IRequestStrategy DefaultRequestStrategy { get; set; }

        public TimeSpan DefaultTimeout { get; set; }

        public RequestPriority? DefaultPriority { get; set; }

        public int MaxReplicasUsedPerRequest { get; set; }
        
        public LoggingOptions Logging { get; set; }

        public AdaptiveThrottlingOptions AdaptiveThrottling { get; set; }

        public ReplicaBudgetingOptions ReplicaBudgeting { get; set; }

        public string ServiceName { get; set; }
        
        public string Environment { get; set; }

        public bool DeduplicateRequestUrl { get; set; }

        public bool ValidateHttpMethod { get; set; }

        public bool IsValid => !Validate().Any();

        public IEnumerable<string> Validate()
        {
            if (Transport == null)
                yield return "Transport implementation is not set. It is a required part of configuration.";

            if (ClusterProvider == null)
                yield return "Cluster provider implementation is not set. It is a required part of configuration.";

            if (ResponseCriteria?.Count > 0)
            {
                var lastCriterion = ResponseCriteria.Last();
                if (!(lastCriterion is AlwaysRejectCriterion) && !(lastCriterion is AlwaysAcceptCriterion))
                    yield return $"Last response criterion must always be either an '{typeof(AlwaysRejectCriterion).Name}' or '{typeof(AlwaysAcceptCriterion).Name}'.";
            }

            if (ResponseCriteria != null && ResponseCriteria.Any(criterion => criterion == null))
                yield return "One of provided response criteria is null";

            if (RequestTransforms != null && RequestTransforms.Any(transform => transform == null))
                yield return "One of provided request transforms is null";

            if (ResponseTransforms != null && ResponseTransforms.Any(transform => transform == null))
                yield return "One of provided response transforms is null";

            if (Modules != null && Modules.Any(module => module.Value == null))
                yield return "One of provided request modules lists is null";

            if (Modules != null && Modules.SelectMany(x => x.Value).Any(module => module == null))
                yield return "One of provided request modules is null";

            if (DefaultTimeout <= TimeSpan.Zero)
                yield return $"Default timeout must be positive, but was '{DefaultTimeout}'";

            if (MaxReplicasUsedPerRequest <= 0)
                yield return $"Maximum replicas per request must be > 0, but was {MaxReplicasUsedPerRequest}";
        }

        public void ValidateOrDie()
        {
            if (IsValid)
                return;

            var builder = new StringBuilder();

            builder.AppendLine("There are some errors in cluster client configuration:");

            foreach (var errorMessage in Validate())
            {
                builder.Append("\t");
                builder.Append("--> ");
                builder.AppendLine(errorMessage);
            }

            throw new ClusterClientException(builder.ToString());
        }

        public void AugmentWithDefaults()
        {
            if (ReplicaOrdering == null)
                ReplicaOrdering = ClusterClientDefaults.ReplicaOrdering(Log);

            if (ResponseCriteria == null || ResponseCriteria.Count == 0)
                ResponseCriteria = ClusterClientDefaults.ResponseCriteria();

            if (RetryPolicy == null)
                RetryPolicy = ClusterClientDefaults.RetryPolicy;

            if (RetryStrategy == null)
                RetryStrategy = ClusterClientDefaults.RetryStrategy;

            if (ResponseSelector == null)
                ResponseSelector = ClusterClientDefaults.ResponseSelector;

            if (DefaultRequestStrategy == null)
                DefaultRequestStrategy = ClusterClientDefaults.RequestStrategy;
        }
    }
}