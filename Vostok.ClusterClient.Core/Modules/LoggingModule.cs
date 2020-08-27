using System;
using System.Threading.Tasks;
using Vostok.Clusterclient.Core.Criteria;
using Vostok.Clusterclient.Core.Misc;
using Vostok.Clusterclient.Core.Model;
using Vostok.Commons.Time;
using Vostok.Logging.Abstractions;

namespace Vostok.Clusterclient.Core.Modules
{
    internal class LoggingModule : IRequestModule
    {
        private readonly IResponseClassifier responseClassifier;
        private readonly LoggingOptions options;
        private readonly string targetService;

        public LoggingModule(IResponseClassifier responseClassifier, LoggingOptions options, string targetService)
        {
            this.responseClassifier = responseClassifier;
            this.options = options;
            this.targetService = targetService;
        }

        public async Task<ClusterResult> ExecuteAsync(IRequestContext context, Func<IRequestContext, Task<ClusterResult>> next)
        {
            if (options.LogReplicaRequests)
                LogRequestDetails(context);

            var result = await next(context).ConfigureAwait(false);

            if (options.LogReplicaResults)
            {
                if (result.Status == ClusterResultStatus.Success)
                    LogSuccessfulResult(context, result);
                else
                    LogFailedResult(context, result);
            }

            return result;
        }

        #region Logging

        private void LogRequestDetails(IRequestContext context) =>
            context.Log.Info("Sending request '{Request}' to '{TargetService}'. Timeout = {Timeout}. Strategy = '{Strategy}'.",
                new
                {
                    Request = context.Request.ToString(false, false),
                    TargetService = targetService ?? "somewhere",
                    Timeout = context.Budget.Total.ToPrettyString(),
                    TimeoutMs = context.Budget.Total.TotalMilliseconds,
                    Strategy = context.Parameters.Strategy?.ToString()
                });

        private void LogSuccessfulResult(IRequestContext context, ClusterResult result) =>
            context.Log.Info(
                "Success. Response code = {ResponseCode:D} ('{ResponseCode}'). Time = {ElapsedTime}.",
                new
                {
                    ResponseCode = result.Response.Code,
                    ElapsedTime = context.Budget.Elapsed.ToPrettyString(),
                    ElapsedTimeMs = context.Budget.Elapsed.TotalMilliseconds
                });

        private void LogFailedResult(IRequestContext context, ClusterResult result)
        {
            var message = "Request '{Request}' to '{TargetService}' has failed with status '{Status}'. Response code = {ResponseCode:D} ('{ResponseCode}'). Time = {ElapsedTime}.";
            var properties = new
            {
                Request = context.Request.ToString(false, false),
                TargetService = targetService ?? "somewhere",
                result.Status,
                ResponseCode = result.Response.Code,
                ElapsedTime = context.Budget.Elapsed.ToPrettyString(),
                ElapsedTimeMs = context.Budget.Elapsed.TotalMilliseconds
            };

            if (result.Status == ClusterResultStatus.Canceled)
            {
                context.Log.Warn(message, properties);
            }
            else
            {
                var verdict = responseClassifier.Decide(result.Response, options.ErrorResponseCriteria);
                if (verdict == ResponseVerdict.Accept)
                    context.Log.Warn(message, properties);
                else
                    context.Log.Error(message, properties);
            }
        }

        #endregion
    }
}