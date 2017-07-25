namespace TPLink.SmartHome
{
    /// <summary>
    /// Holds information about the energy monitor's power consumption.
    /// </summary>
    public sealed class ConsumptionInfo
    {
        internal ConsumptionInfo(decimal power, decimal voltage, decimal current)
        {
            this.Power = power;
            this.Voltage = voltage;
            this.Current = current;
        }

        public decimal Power { get; }

        public decimal Voltage { get; }

        public decimal Current { get; }
    }
}
