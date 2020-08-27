using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NSubstitute;
using NUnit.Framework;
using Vostok.Clusterclient.Core.Criteria;
using Vostok.Clusterclient.Core.Misc;
using Vostok.Clusterclient.Core.Model;
using Vostok.Clusterclient.Core.Modules;
using Vostok.Clusterclient.Core.Tests.Helpers;
using Vostok.Logging.Abstractions;

namespace Vostok.Clusterclient.Core.Tests.Modules
{
    [TestFixture]
    internal class LoggingModule_Tests
    {
        [TestCase(false, false, ExpectedResult = 0)]
        [TestCase(true, false, ExpectedResult = 1)]
        [TestCase(false, true, ExpectedResult = 1)]
        [TestCase(true, true, ExpectedResult = 2)]
        public async Task<int> Should_log_as_many_times_as_stated_in_options(bool logRequest, bool logResult)
        {
            var loggingOptions = new LoggingOptions
            {
                LogReplicaRequests = logRequest,
                LogReplicaResults = logResult,
            };
            var spyLog = new SpyLog();
            var context = Substitute.For<IRequestContext>();
            context.Log.Returns(_ => new SilentLog());
            context.Request.Returns(_ => Request.Get("/foo/bar/baz"));
            context.Budget.Returns(_ => Budget.WithRemaining(5.Seconds()));
            context.Parameters.Returns(_ => RequestParameters.Empty);

            await CreateLoggingModule(loggingOptions).ExecuteAsync(context, _ => Task.FromResult(CreateClusterResult(ClusterResultStatus.Canceled)));


            return spyLog.LoggedEvents.Count;
        }

        [Test]
        public async Task Should_log_cancelled_cluster_result_as_a_warning()
        {
            var spyLog = new SpyLog();
            var responseCriterion = Substitute.For<IResponseCriterion>();
            var options = new LoggingOptions
            {
                ErrorResponseCriteria = new List<IResponseCriterion>()
                {
                    responseCriterion
                }
            };

            var module = new LoggingModule(new ResponseClassifier(), options, null);


            var context = Substitute.For<IRequestContext>();
            context.Log.Returns(_ => spyLog);
            context.Request.Returns(_ => Request.Get("/foo/bar/baz"));
            context.Budget.Returns(_ => Budget.WithRemaining(5.Seconds()));
            context.Parameters.Returns(_ => RequestParameters.Empty);

            await module.ExecuteAsync(context, _ => Task.FromResult(CreateClusterResult(ClusterResultStatus.Canceled)));

            spyLog.LoggedEvents.Last().Level.Should().Be(LogLevel.Warn);
            responseCriterion.DidNotReceiveWithAnyArgs().Decide(default);
        }

        private ClusterResult CreateClusterResult(
            ClusterResultStatus clusterStatus = ClusterResultStatus.Success,
            ResponseCode responseCode = ResponseCode.Ok)
        {
            var request = Request.Get("/foo/bar/baz");
            var response = new Response(responseCode);
            return new ClusterResult(clusterStatus, new List<ReplicaResult>(), response, request);
        }

        private LoggingModule CreateLoggingModule(LoggingOptions options = null)
        {
            return new LoggingModule(new ResponseClassifier(), options ?? new LoggingOptions(), null);
        }
    }

    internal class SpyLog : ILog
    {
        public List<LogEvent> LoggedEvents { get; private set; } = new List<LogEvent>();

        private Dictionary<string, string> injectedProps = new Dictionary<string, string>();

        public void Log(LogEvent @event)
        {
            var e = @event;

            if (injectedProps.Any() && e != null)
            {
                foreach (var kvp in injectedProps)
                {
                    if (e.Properties == null || !e.Properties.ContainsKey(kvp.Key))
                        e = e.WithProperty(kvp.Key, kvp.Value);
                }
            }

            LoggedEvents.Add(e);
        }

        public SpyLog WithProperty(string name, string value)
        {
            injectedProps[name] = value;
            return this;
        }

        public bool IsEnabledFor(LogLevel level) => true;
        public ILog ForContext(string context) => this;
    }

}