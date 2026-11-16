using System;

namespace WingSinger.UI {
    // A visual measurement of the same voltage thresholds used by the production input.
    // BLE defaults: resting center 1300 mV, target deviation 100 mV, five-sample mean.
    public readonly struct BreathMeterReading {
        public readonly bool Available;
        public readonly double ValueMv, CenterMv, TargetDeltaMv, Strength;
        public readonly BreathLane Lane;
        public bool TargetReached => Available && Lane != BreathLane.Middle;
        BreathMeterReading(bool available,double value,double center,double target,double strength,BreathLane lane) {
            Available=available;ValueMv=value;CenterMv=center;TargetDeltaMv=target;Strength=strength;Lane=lane;
        }
        public static BreathMeterReading Evaluate(BreathSensorSnapshot sample,BreathSensorConfiguration config,bool csv,bool activeInput=true) {
            if(config==null)return default;
            double center=csv?config.bluetoothCenterMv:(config.inhaleMaximum+config.exhaleMinimum)*.5;
            double target=csv?config.bluetoothDeviationMv:(config.exhaleMinimum-config.inhaleMaximum)*.5;
            bool valid=csv?BreathSensorProtocol.AreCenteredThresholdsValid(center,target):BreathSensorProtocol.AreThresholdsValid(config.inhaleMaximum,config.exhaleMinimum);
            double value=csv?sample.FilteredValue:sample.Value;
            bool available=activeInput && valid && !double.IsNaN(value) && !double.IsInfinity(value) && sample.IsFresh(config.sampleTimeoutSeconds);
            var lane=!available?BreathLane.Middle:csv?BreathSensorProtocol.ResolveCentered(value,center,target):BreathSensorProtocol.Resolve(value,config.inhaleMaximum,config.exhaleMinimum);
            return new BreathMeterReading(available,value,center,valid?target:0,available?Math.Abs(value-center)/target:0,lane);
        }
    }
}
