namespace TPLink.SmartHome
{
    /// <summary>
    /// Holds information about the energy monitor calibration.
    /// </summary>
    public sealed class CalibrationInfo
    {
        public CalibrationInfo(int voltageGain, int currentGain)
        {
            this.VoltageGain = voltageGain;
            this.CurrentGain = currentGain;
        }

        public decimal VoltageGain { get; }

        public decimal CurrentGain { get; }
    }
}
