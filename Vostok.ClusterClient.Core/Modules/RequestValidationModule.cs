﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Vostok.Clusterclient.Core.Misc;
using Vostok.Clusterclient.Core.Model;
using Vostok.Clusterclient.Core.Strategies;
using Vostok.Clusterclient.Core.Transport;
using Vostok.Logging.Abstractions;

namespace Vostok.Clusterclient.Core.Modules
{
    internal class RequestValidationModule : IRequestModule
    {
        public Task<ClusterResult> ExecuteAsync(IRequestContext context, Func<IRequestContext, Task<ClusterResult>> next)
        {
            if (!RequestValidator.IsValid(context.Request))
                return OnInvalidRequest(context, RequestValidator.Validate(context.Request));

            if (HasStreamUnsupportedByTransport(context))
                return OnInvalidRequest(context, "Request has a body stream, which is not supported by transport implementation.");

            if (HasCompositeContentUnsupportedByTransport(context))
                return OnInvalidRequest(context, "Request has a composite body content, which is not supported by transport implementation.");

            if (HasStreamWithParallelStrategy(context))
                return OnInvalidRequest(context, "Request has a body stream, which can't be used concurrently, and uses a parallel execution strategy.");

            return next(context);
        }

        private static Task<ClusterResult> OnInvalidRequest(IRequestContext context, string error) =>
            OnInvalidRequest(context, new[] {error});

        private static Task<ClusterResult> OnInvalidRequest(IRequestContext context, IEnumerable<string> errors)
        {
            LogValidationErrors(context, errors);

            return Task.FromResult(ClusterResult.IncorrectArguments(context.Request));
        }

        private static bool HasStreamUnsupportedByTransport(IRequestContext context) 
            => context.Request.StreamContent != null && !context.Transport.Supports(TransportCapabilities.RequestStreaming);

        private static bool HasCompositeContentUnsupportedByTransport(IRequestContext context)
            => context.Request.CompositeContent != null && !context.Transport.Supports(TransportCapabilities.RequestCompositeBody);

        private static bool HasStreamWithParallelStrategy(IRequestContext context)
        {
            if (context.Request.StreamContent == null)
                return false;

            var parallelStrategy = context.Parameters.Strategy as ParallelRequestStrategy;

            return parallelStrategy?.ParallelismLevel > 1;
        }

        #region Logging

        private static void LogValidationErrors(IRequestContext context, IEnumerable<string> errors)
        {
            var builder = new StringBuilder();

            builder.AppendLine("Request is not valid:");

            foreach (var errorMessage in errors)
            {
                builder.Append("\t");
                builder.Append("--> ");
                builder.AppendLine(errorMessage);
            }

            context.Log.Error(builder.ToString());
        }

        #endregion
    }
}