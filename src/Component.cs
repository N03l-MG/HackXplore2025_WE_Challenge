using System;

namespace HackXplore2025.src;

public abstract class Component
{
	public string? id { get; set; }
	public string? type { get; set; }
	public string? orderCode { get; set; }
	public string? manufacturer { get; set; }
	public string? url { get; set; }
}

public class Resistor : Component
{
	public double resistance { get; set; }  // in ohms
	public double ratedPower { get; set; }  // in watts
	public double ratedCurrent { get; set; } // in amperes
	public double length { get; set; }  // in mm
	public double width { get; set; }   // in mm
	public double height { get; set; }  // in mm
	public string? mount { get; set; }   // SMD/THT
	public string? series { get; set; }
}

public class Inductor : Component
{
	public double inductance { get; set; }  // in henries
	public double ratedCurrent { get; set; }  // in amperes
	public double saturationCurrent { get; set; }  // in amperes
	public double DCResistance { get; set; }  // in ohms
	public double selfResonantFrequency { get; set; }  // in Hz
	public double length { get; set; }  // in mm
	public double height { get; set; }  // in mm
	public double diameter { get; set; }  // in mm
	public string? mount { get; set; }  // SMD/THT
	public string? series { get; set; }
}

public class Capacitor : Component
{
	public double ratedVoltage { get; set; }  // in volts
	public int lifeCycles { get; set; }
	public double dissipationFactor { get; set; }
	public double rippleCurrent { get; set; }  // in amperes
	public double capacitance { get; set; }  // in farads
	public double length { get; set; }  // in mm
	public double pitch { get; set; }   // in mm
	public double diameter { get; set; }  // in mm
	public double leakageCurrent { get; set; }  // in amperes
	public double impedance { get; set; }  // in ohms
	public string? operatingTemperatureRange { get; set; }
	public string? mount { get; set; }  // SMD/THT
	public string? family { get; set; }
}
