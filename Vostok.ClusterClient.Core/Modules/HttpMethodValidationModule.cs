﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vostok.Clusterclient.Core.Model;
using Vostok.Logging.Abstractions;

namespace Vostok.Clusterclient.Core.Modules
{
    internal class HttpMethodValidationModule : IRequestModule
    {
        private static readonly HashSet<string> All = new HashSet<string>
        {
            RequestMethods.Get,
            RequestMethods.Post,
            RequestMethods.Put,
            RequestMethods.Head,
            RequestMethods.Patch,
            RequestMethods.Delete,
            RequestMethods.Options,
            RequestMethods.Trace
        };

        public Task<ClusterResult> ExecuteAsync(IRequestContext context, Func<IRequestContext, Task<ClusterResult>> next)
        {
            var method = context.Request.Method;

            if (All.Contains(method))
                return next(context);

            context.Log.Error("Request HTTP method '{Method}' is not valid.", method);
            return Task.FromResult(ClusterResult.IncorrectArguments(context.Request));
        }
    }
}