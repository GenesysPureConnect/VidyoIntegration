using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using ININ.IceLib.Connection;
using ININ.IceLib.Statistics;
using VidyoIntegration.CommonLib;
using VidyoIntegration.CommonLib.CicTypes;

namespace VidyoIntegration.CicManagerLib
{
    internal class StatisticsWrapper : IDisposable
    {
        #region Private Fields

        private readonly Session _session;
        private StatisticsManager _statisticsManager;
        private StatisticCatalog _statisticCatalog;
        private readonly StatisticListener _statisticListener;
        private readonly List<StatisticKey> _statisticKeys = new List<StatisticKey>();
        private ReadOnlyCollection<StatisticDefinition> _statisticDefinitions;
        private StatisticDefinition NumberAvailableForAcdInteractionsDefinition
        {
            get
            {
                return
                    _statisticDefinitions.FirstOrDefault(
                        stat =>
                            stat.Id.Uri.Equals("inin.workgroup:NumberAvailableForACDInteractions",
                                StringComparison.InvariantCultureIgnoreCase));
            }
        }
        private StatisticDefinition InteractionCountDefinition
        {
            get
            {
                return
                    _statisticDefinitions.FirstOrDefault(
                        stat =>
                            stat.Id.Uri.Equals("inin.queue:InteractionCount",
                                StringComparison.InvariantCultureIgnoreCase));
            }
        }
        private StatisticDefinition PercentAvailableDefinition
        {
            get
            {
                return
                    _statisticDefinitions.FirstOrDefault(
                        stat =>
                            stat.Id.Uri.Equals("inin.workgroup:PercentAvailable",
                                StringComparison.InvariantCultureIgnoreCase));
            }
        }
        private StatisticDefinition AverageWaitTimeDefinition
        {
            get
            {
                return
                    _statisticDefinitions.FirstOrDefault(
                        stat =>
                            stat.Id.Uri.Equals("inin.workgroup:AverageWaitTime",
                                StringComparison.InvariantCultureIgnoreCase));
            }
        }
        private StatisticDefinition InteractionsWaitingDefinition
        {
            get
            {
                return
                    _statisticDefinitions.FirstOrDefault(
                        stat =>
                            stat.Id.Uri.Equals("inin.workgroup:InteractionsWaiting",
                                StringComparison.InvariantCultureIgnoreCase));
            }
        }

        private const int RetryAllowance = 10;

        #endregion



        #region Internal Properties

        internal static string NumberAvailableForAcdInteractions = "inin.workgroup:NumberAvailableForACDInteractions";
        internal static string PercentAvailable = "inin.workgroup:PercentAvailable";
        internal static string AverageWaitTime = "inin.workgroup:AverageWaitTime";
        internal static string InteractionsWaiting = "inin.workgroup:InteractionsWaiting";

        #endregion



        internal StatisticsWrapper(Session session)
        {
            _session = session;

            _statisticsManager = StatisticsManager.GetInstance(_session);

            _statisticCatalog = new StatisticCatalog(_statisticsManager);
            _statisticCatalog.StartWatching();
            _statisticDefinitions = _statisticCatalog.GetStatisticDefinitions();
            _statisticCatalog.StopWatching();

            _statisticListener = new StatisticListener(_statisticsManager);

        }

        

        #region Private Methods

        private StatisticValue GetStatistic(StatisticKey key, int retryCount = 0)
        {
            // Watch key
            if (!_statisticListener.IsWatching(key))
            {
                if (_statisticListener.IsWatching())
                    _statisticListener.ChangeWatchedKeys(new[] { key }, new StatisticKey[] { }, false);
                else
                    _statisticListener.StartWatching(new[] { key });
            }

            // Get value
            var statValue = _statisticListener[key];

            // Error checking
            if (statValue.IsError)
                throw new StatisticErrorException(statValue.ErrorReason.ToString());
            if (statValue.IsNull)
            {
                // If this isn't already a retry, try again and return whatever we get back
                if (retryCount >= RetryAllowance) throw new NullStatisticValueException(key.UriString);
                Thread.Sleep(500);
                return GetStatistic(key, retryCount + 1);
            }

            return statValue;
        }

        private int GetStatisticInt(StatisticKey key, bool waitForData)
        {
            var statValue = GetStatistic(key, waitForData ? 0 : RetryAllowance);

            // Return stat value
            if (statValue.Definition.ValueType == StatisticValueType.Int)
            {
                return ((StatisticIntValue)statValue).Value;
            }

            // This should never happen
            Trace.Cic.error("Statistic was of unexpected type: {}", statValue.Definition.ValueType);
            return -1;
        }

        private double GetStatisticPercent(StatisticKey key, bool waitForData)
        {
            var statValue = GetStatistic(key, waitForData ? 0 : RetryAllowance);

            // Return stat value
            if (statValue.Definition.ValueType == StatisticValueType.Percent)
            {
                return ((StatisticPercentValue)statValue).Value;
            }

            // This should never happen
            Trace.Cic.error("Statistic was of unexpected type: {}", statValue.Definition.ValueType);
            return -1;
        }

        private TimeSpan GetStatisticDuration(StatisticKey key, bool waitForData)
        {
            var statValue = GetStatistic(key, waitForData ? 0 : RetryAllowance);

            // Return stat value
            if (statValue.Definition.ValueType == StatisticValueType.Duration)
            {
                return ((StatisticDurationValue)statValue).Value;
            }

            // This should never happen
            Trace.Cic.error("Statistic was of unexpected type: {}", statValue.Definition.ValueType);
            return TimeSpan.MinValue;
        }

        #endregion



        #region Internal Methods

        internal int GetNumberAvailableForAcdInteractions(string workgroupName, bool waitForData)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    var key = new StatisticKey(NumberAvailableForAcdInteractionsDefinition.Id,
                        new ParameterValueKeyedCollection
                        {
                            new ParameterValue(new ParameterTypeId("ININ.People.WorkgroupStats:Workgroup"),
                                workgroupName)
                        });

                    return GetStatisticInt(key, waitForData);
                }
                catch (NullStatisticValueException ex)
                {
                    Trace.Cic.warning(ex.Message);
                    return -1;
                }
                catch (StatisticErrorException ex)
                {
                    Trace.Cic.exception(ex);
                    return -1;
                }
                catch (Exception ex)
                {
                    Trace.Cic.exception(ex);
                    return -1;
                }
            }
        }

        internal int GetInteractionCount(string workgroupName, bool waitForData)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    var key = new StatisticKey(InteractionCountDefinition.Id,
                        new ParameterValueKeyedCollection
                        {
                            new ParameterValue(new ParameterTypeId("ININ.Queue:Type"), "workgroup"),
                            new ParameterValue(new ParameterTypeId("ININ.Queue:Name"), workgroupName)
                        });

                    return GetStatisticInt(key, waitForData);
                }
                catch (NullStatisticValueException ex)
                {
                    Trace.Cic.warning(ex.Message);
                    return -1;
                }
                catch (StatisticErrorException ex)
                {
                    Trace.Cic.exception(ex);
                    return -1;
                }
                catch (Exception ex)
                {
                    Trace.Cic.exception(ex);
                    return -1;
                }
            }
        }

        internal double GetPercentAvailable(string workgroupName, bool waitForData)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    var key = new StatisticKey(PercentAvailableDefinition.Id,
                        new ParameterValueKeyedCollection
                        {
                            new ParameterValue(new ParameterTypeId("ININ.People.WorkgroupStats:Workgroup"), workgroupName)
                        });

                    return GetStatisticPercent(key, waitForData);
                }
                catch (NullStatisticValueException ex)
                {
                    Trace.Cic.warning(ex.Message);
                    return -1;
                }
                catch (StatisticErrorException ex)
                {
                    Trace.Cic.exception(ex);
                    return -1;
                }
                catch (Exception ex)
                {
                    Trace.Cic.exception(ex);
                    return -1;
                }
            }
        }

        internal TimeSpan GetAverageWaitTime(string workgroupName, IntervalTypes intervalType, bool waitForData)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    var key = new StatisticKey(AverageWaitTimeDefinition.Id,
                        new ParameterValueKeyedCollection
                        {
                            new ParameterValue(new ParameterTypeId("ININ.People.WorkgroupStats:Workgroup"), workgroupName),
                            new ParameterValue(new ParameterTypeId("ININ.Queue:Interval"), intervalType.ToString())
                        });

                    return GetStatisticDuration(key, waitForData);
                }
                catch (NullStatisticValueException ex)
                {
                    Trace.Cic.warning(ex.Message);
                    return TimeSpan.MinValue;
                }
                catch (StatisticErrorException ex)
                {
                    Trace.Cic.exception(ex);
                    return TimeSpan.MinValue;
                }
                catch (Exception ex)
                {
                    Trace.Cic.exception(ex);
                    return TimeSpan.MinValue;
                }
            }
        }

        internal int GetInteractionsWaiting(string workgroupName, bool waitForData)
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    var key = new StatisticKey(InteractionsWaitingDefinition.Id,
                        new ParameterValueKeyedCollection
                        {
                            new ParameterValue(new ParameterTypeId("ININ.People.WorkgroupStats:Workgroup"), workgroupName)
                        });

                    return GetStatisticInt(key, waitForData);
                }
                catch (NullStatisticValueException ex)
                {
                    Trace.Cic.warning(ex.Message);
                    return -1;
                }
                catch (StatisticErrorException ex)
                {
                    Trace.Cic.exception(ex);
                    return -1;
                }
                catch (Exception ex)
                {
                    Trace.Cic.exception(ex);
                    return -1;
                }
            }
        }

        public void Dispose()
        {
            using (Trace.Cic.scope())
            {
                try
                {
                    if (_statisticListener.IsWatching())
                        _statisticListener.StopWatching();
                }
                catch (Exception ex)
                {
                    Trace.Cic.exception(ex);
                }
            }
        }

        #endregion
    }
}