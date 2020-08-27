using System.Collections.Generic;
using JetBrains.Annotations;
using Vostok.Clusterclient.Core.Criteria;
using Vostok.Clusterclient.Core.Model;

namespace Vostok.Clusterclient.Core.Misc
{
    /// <summary>
    /// A set of ClusterClient logging settings.
    /// </summary>
    [PublicAPI]
    public class LoggingOptions
    {
        /// <summary>
        /// <para>Gets or sets whether to log request details before execution.</para>
        /// <para>This parameter is optional and has a default value (see <see cref="ClusterClientDefaults.LogRequestDetails"/>).</para>
        /// </summary>
        public bool LogRequestDetails { get; set; } = ClusterClientDefaults.LogRequestDetails;

        /// <summary>
        /// <para>Gets or sets whether to log result details after execution.</para>
        /// <para>This parameter is optional and has a default value (see <see cref="ClusterClientDefaults.LogResultDetails"/>).</para>
        /// </summary>
        public bool LogResultDetails { get; set; } = ClusterClientDefaults.LogResultDetails;

        /// <summary>
        /// <para>Gets or sets whether to log requests to each replica.</para>
        /// <para>This parameter is optional and has a default value (see <see cref="ClusterClientDefaults.LogReplicaRequests"/>).</para>
        /// </summary>
        public bool LogReplicaRequests { get; set; } = ClusterClientDefaults.LogReplicaRequests;

        /// <summary>
        /// <para>Gets or sets whether to log results from each replica.</para>
        /// <para>This parameter is optional and has a default value (see <see cref="ClusterClientDefaults.LogReplicaResults"/>).</para>
        /// </summary>
        public bool LogReplicaResults { get; set; } = ClusterClientDefaults.LogReplicaResults;

        /// <summary>
        /// <para>A list of response criteria. See <see cref="IResponseCriterion"/> and <see cref="ResponseVerdict"/> for more details.</para>
        /// <para>These criteria are used to determine whether unsuccessful cluster result should be logged as an Error or as a Warning. <see cref="ClusterResultStatus.Success"/>.</para>
        /// <para>This parameter is optional and has a default value (see <see cref="Core.ClusterClientDefaults.LoggingResponseCriteria"/>).</para>
        /// </summary>
        public List<IResponseCriterion> ErrorResponseCriteria { get; set; }
    }
}