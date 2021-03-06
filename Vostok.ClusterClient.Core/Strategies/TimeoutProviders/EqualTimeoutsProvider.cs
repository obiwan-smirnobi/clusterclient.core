﻿using System;
using JetBrains.Annotations;
using Vostok.Clusterclient.Core.Model;
using Vostok.Commons.Time;

namespace Vostok.Clusterclient.Core.Strategies.TimeoutProviders
{
    /// <summary>
    /// <para>Represents a timeout provider which divides time budget equally between several replicas (their count is called division factor).</para>
    /// <para>However, if any of the replicas does not fully use its time quanta, redistribution occurs for remaining replicas.</para>
    /// </summary>
    /// <example>
    /// Let's say we have a division factor = 3 and a time budget = 12 sec. Then we might observe following distribution patterns:
    /// <para>4 sec --> 4 sec --> 4 sec (all replicas use full timeout).</para>
    /// <para>3 sec --> 4.5 sec --> 4.5 sec (first replica failed prematurely, redistribution occured).</para>
    /// <para>1 sec --> 1 sec --> 10 sec (first two replicas failed prematurely, redistribution occured).</para>
    /// </example>
    [PublicAPI]
    public class EqualTimeoutsProvider : ISequentialTimeoutsProvider
    {
        private readonly int divisionFactor;

        /// <param name="divisionFactor">A division factor. See more in <see cref="EqualTimeoutsProvider"/> doc. Must be > 0.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="divisionFactor"/> is not a positive number.</exception>
        public EqualTimeoutsProvider(int divisionFactor)
        {
            if (divisionFactor <= 0)
                throw new ArgumentOutOfRangeException(nameof(divisionFactor), "Division factor must be a positive number.");

            this.divisionFactor = divisionFactor;
        }

        /// <inheritdoc />
        public TimeSpan GetTimeout(Request request, IRequestTimeBudget budget, int currentReplicaIndex, int totalReplicas)
        {
            if (currentReplicaIndex >= divisionFactor)
                return budget.Remaining;

            var effectiveDivisionFactor = Math.Min(divisionFactor, totalReplicas) - currentReplicaIndex;

            return TimeSpanArithmetics.Max(TimeSpan.Zero, budget.Remaining.Divide(effectiveDivisionFactor));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString() => "equal-" + divisionFactor;
    }
}