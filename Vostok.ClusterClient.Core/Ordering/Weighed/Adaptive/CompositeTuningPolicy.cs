﻿using System;
using JetBrains.Annotations;
using Vostok.Clusterclient.Core.Model;

namespace Vostok.Clusterclient.Core.Ordering.Weighed.Adaptive
{
    /// <summary>
    /// <para>Represents a policy which combines results of several other policies using given priority list:</para>
    /// <list type="number">
    /// <item><description>If any of policies select to <see cref="AdaptiveHealthAction.Decrease"/> replica health, it gets decreased.</description></item>
    /// <item><description>If any of policies select to <see cref="AdaptiveHealthAction.Increase"/> replica health, it gets increased.</description></item>
    /// <item><description>If none of policies select to increase or decrease replica health, it isn't changed.</description></item>
    /// </list>
    /// </summary>
    [PublicAPI]
    public class CompositeTuningPolicy : IAdaptiveHealthTuningPolicy
    {
        private readonly IAdaptiveHealthTuningPolicy[] policies;

        public CompositeTuningPolicy([NotNull] params IAdaptiveHealthTuningPolicy[] policies)
        {
            this.policies = policies ?? throw new ArgumentNullException(nameof(policies));
        }

        /// <inheritdoc />
        public AdaptiveHealthAction SelectAction(ReplicaResult result)
        {
            var seenIncrease = false;
            var seenDecrease = false;

            foreach (var policy in policies)
            {
                var action = policy.SelectAction(result);

                if (action == AdaptiveHealthAction.Increase)
                    seenIncrease = true;
                if (action == AdaptiveHealthAction.Decrease)
                    seenDecrease = true;
            }

            if (seenDecrease)
                return AdaptiveHealthAction.Decrease;
            if (seenIncrease)
                return AdaptiveHealthAction.Increase;

            return AdaptiveHealthAction.DontTouch;
        }
    }
}