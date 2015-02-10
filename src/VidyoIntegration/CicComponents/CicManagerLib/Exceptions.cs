using System;

namespace VidyoIntegration.CicManagerLib
{
    public class NullStatisticValueException : Exception
    {
        public NullStatisticValueException(string statName) : base("The statistic was null: " + statName)
        {

        }
    }

    public class StatisticErrorException : Exception
    {
        public StatisticErrorException(string reason) : base("The statistic was in error because: " + reason)
        {

        }
    }
}
