﻿using System;
using JetBrains.Annotations;
using Vostok.Commons.Time;
using Vostok.Logging.Abstractions;

namespace Vostok.Clusterclient.Core.Ordering.Weighed.Adaptive
{
    /// <summary>
    /// <para>An implementation of adaptive health which uses numbers in <c>(0; 1]</c> range as health values. Default health value is equal to 1.</para>
    /// <para>Upon increase, health value is multiplied by an up multiplier in <c>(1; +infinity)</c> range.</para>
    /// <para>Upon decrease, health value is multiplied by a down multiplier in <c>(0; 1)</c> range.</para>
    /// <para>Health values have a customizable lower bound in <c>(0; 1)</c> range.</para>
    /// <para>Health damage also decays linearly during a configurable time period since last health decrease. Subsequent decreases reset decay duration.</para>
    /// <para>For instance, let's assume that we've reduced a replica's health to 0.5 just a moment ago and decay duration is 10 minutes. Then, assuming there are no other changes, health will have following values in the future:</para>
    /// <list type="bullet">
    /// <item><description>0.55 after 1 minute</description></item>
    /// <item><description>0.625 after 2.5 minutes</description></item>
    /// <item><description>0.75 after 5 minutes</description></item>
    /// <item><description>0.9 after 8 minutes</description></item>
    /// <item><description>1.0 after 10 minutes</description></item>
    /// <item><description>1.0 after 11 minutes</description></item>
    /// </list>
    /// <para>This decay mechanism helps to avoid situations where replicas which had temporary problems are still avoided when the problems resolve.</para>
    /// <para>Health application is just a multiplication of health value and current weight (health = 0.5 causes weight = 2 to turn into 1).</para>
    /// <para>This health implementation can only decrease replica weights as it's aim is to avoid misbehaving replicas.</para>
    /// </summary>
    [PublicAPI]
    public class AdaptiveHealthWithLinearDecay : IAdaptiveHealthImplementation<HealthWithDecay>
    {
        private const double MaximumHealthValue = 1.0;

        private readonly Func<DateTime> getCurrentTime;

        /// <param name="decayDuration">A duration during which health damage fully decays.</param>
        /// <param name="upMultiplier">A multiplier used to increase health. Must be in <c>(1; +infinity)</c> range.</param>
        /// <param name="downMultiplier">A multiplier used to decrease health. Must be in <c>(0; 1)</c> range.</param>
        /// <param name="minimumHealthValue">Minimum possible health value. Must be in <c>(0; 1)</c> range.</param>
        public AdaptiveHealthWithLinearDecay(
            TimeSpan decayDuration,
            double upMultiplier = ClusterClientDefaults.AdaptiveHealthUpMultiplier,
            double downMultiplier = ClusterClientDefaults.AdaptiveHealthDownMultiplier,
            double minimumHealthValue = ClusterClientDefaults.AdaptiveHealthMinimumValue)
            : this(() => DateTime.UtcNow, decayDuration, upMultiplier, downMultiplier, minimumHealthValue)
        {
        }

        internal AdaptiveHealthWithLinearDecay(
            Func<DateTime> getCurrentTime,
            TimeSpan decayDuration,
            double upMultiplier = ClusterClientDefaults.AdaptiveHealthUpMultiplier,
            double downMultiplier = ClusterClientDefaults.AdaptiveHealthDownMultiplier,
            double minimumHealthValue = ClusterClientDefaults.AdaptiveHealthMinimumValue)
        {
            if (decayDuration <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(decayDuration), $"Decay duration must be positive. Given value = '{decayDuration}'.");

            if (upMultiplier <= 1.0)
                throw new ArgumentOutOfRangeException(nameof(upMultiplier), $"Up multiplier must be > 1. Given value = '{upMultiplier}'.");

            if (downMultiplier <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(downMultiplier), $"Down multiplier must be positive. Given value = '{downMultiplier}'.");

            if (downMultiplier >= 1.0)
                throw new ArgumentOutOfRangeException(nameof(downMultiplier), $"Down multiplier must be < 1. Given value = '{downMultiplier}'.");

            if (minimumHealthValue <= 0)
                throw new ArgumentOutOfRangeException(nameof(minimumHealthValue), $"Minimum health must be positive. Given value = '{minimumHealthValue}'.");

            if (minimumHealthValue >= 1)
                throw new ArgumentOutOfRangeException(nameof(minimumHealthValue), $"Minimum health must be < 1. Given value = '{minimumHealthValue}'.");

            this.getCurrentTime = getCurrentTime;

            DecayDuration = decayDuration;
            UpMultiplier = upMultiplier;
            DownMultiplier = downMultiplier;
            MinimumHealthValue = minimumHealthValue;
        }

        /// <summary>
        /// A duration during which health damage fully decays.
        /// </summary>
        public TimeSpan DecayDuration { get; }

        /// <summary>
        /// A multiplier used to increase health. Must be in <c>(1; +infinity)</c> range.
        /// </summary>
        public double UpMultiplier { get; }

        /// <summary>
        /// A multiplier used to decrease health. Must be in <c>(0; 1)</c> range.
        /// </summary>
        public double DownMultiplier { get; }

        /// <summary>
        /// Minimum possible health value. Must be in <c>(0; 1)</c> range.
        /// </summary>
        public double MinimumHealthValue { get; }

        /// <inheritdoc />
        public void ModifyWeight(HealthWithDecay health, ref double weight)
        {
            var healthDamage = MaximumHealthValue - health.Value;
            if (healthDamage <= 0.0)
                return;

            var timeSinceDecayPivot = TimeSpanArithmetics.Max(getCurrentTime() - health.DecayPivot, TimeSpan.Zero);
            if (timeSinceDecayPivot >= DecayDuration)
                return;

            var effectiveHealth = health.Value + healthDamage * ((double) timeSinceDecayPivot.Ticks / DecayDuration.Ticks);

            weight *= effectiveHealth;
        }

        /// <inheritdoc />
        public HealthWithDecay CreateDefaultHealth() =>
            new HealthWithDecay(MaximumHealthValue, DateTime.MinValue);

        /// <inheritdoc />
        public HealthWithDecay IncreaseHealth(HealthWithDecay current) =>
            new HealthWithDecay(Math.Min(MaximumHealthValue, current.Value * UpMultiplier), current.DecayPivot);

        /// <inheritdoc />
        public HealthWithDecay DecreaseHealth(HealthWithDecay current) =>
            new HealthWithDecay(Math.Max(MinimumHealthValue, current.Value * DownMultiplier), getCurrentTime());

        /// <inheritdoc />
        public bool AreEqual(HealthWithDecay x, HealthWithDecay y) =>
            x.Value.Equals(y.Value) && x.DecayPivot == y.DecayPivot;

        /// <inheritdoc />
        public void LogHealthChange(Uri replica, HealthWithDecay oldHealth, HealthWithDecay newHealth, ILog log) =>
            log.Debug("Local base health for replica '{Replica}' has changed from {OldHealth:N4} to {NewHealth:N4}.", replica, oldHealth.Value, newHealth.Value);
    }
}