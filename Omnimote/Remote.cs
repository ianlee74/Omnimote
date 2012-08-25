using System;
using System.IO.Ports;
using System.Threading;
using GHIElectronics.NETMF.FEZ;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using NETMFx.Joystick;
using NETMFx.Math;
using NETMFx.Wireless;

namespace Omnimote
{
    public class Remote : IDisposable
    {
        private readonly InterruptPort _button0;
        private readonly InterruptPort _button1;
        private readonly InterruptPort _button2;
        private readonly InterruptPort _button3;
        private readonly AnalogJoystick _leftJoystick;
        private readonly AnalogJoystick _rightJoystick;
        private readonly RCRadio _radio;

        public Remote()
        {
            _button0 = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di12, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeHigh);
            _button1 = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di13, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeHigh);
            _button2 = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di2, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeHigh);
            _button3 = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di3, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeHigh);
            _button0.OnInterrupt += OnButtonPush;
            _button1.OnInterrupt += OnButtonPush;
            _button2.OnInterrupt += OnButtonPush;
            _button3.OnInterrupt += OnButtonPush;
            Cpu.GlitchFilterTime = new TimeSpan(TimeSpan.TicksPerSecond / 2);

            _leftJoystick = new AnalogJoystick((Cpu.Pin)FEZ_Pin.AnalogIn.An1, (Cpu.Pin)FEZ_Pin.AnalogIn.An0, -100, 100);
            _leftJoystick.YCalibration.EndPoints.Low = -4;
            _leftJoystick.YCalibration.EndPoints.High = 7;
            _leftJoystick.XCalibration.EndPoints.Low = 0;
            _leftJoystick.XCalibration.EndPoints.High = 3;
            _leftJoystick.AngularCalibration = new Angle(Angle.ConvertDegreesToRadians(-90));
            _rightJoystick = new AnalogJoystick((Cpu.Pin)FEZ_Pin.AnalogIn.An3, (Cpu.Pin)FEZ_Pin.AnalogIn.An2, -100, 100);
            _rightJoystick.YCalibration.EndPoints.Low = -6;
            _rightJoystick.YCalibration.EndPoints.High = 6;
            _rightJoystick.XCalibration.EndPoints.Low = 0;
            _rightJoystick.XCalibration.EndPoints.High = 9;
            _rightJoystick.AngularCalibration = new Angle(Angle.ConvertDegreesToRadians(-90));

// ReSharper disable RedundantArgumentDefaultValue
            _radio = new RCRadio("OM1", "COM1", 115200, Parity.None, 8, StopBits.One)
                         {Id = "OM1", PartnerId = "OC1", SendFrequency = 200};
// ReSharper restore RedundantArgumentDefaultValue
            _radio.DataReceived += OnRadioDataReceived;
        }

        public void Activate()
        {
            _radio.Activate(false);
            var transmitThread = new Thread(Transmit);
            transmitThread.Start();    
        }

        private static void OnRadioDataReceived(object sender, RadioDataReceivedEventArgs e)
        {
#if DEBUG
            var data = "RECEIVED:  ";
            foreach (var d in e.Data)
            {
                data += (data.Length > 11 ? "|" : "") + d;
            }
            Debug.Print(data);
#endif
        }

        private static void OnButtonPush(uint pinId, uint state, DateTime time)
        {
            Debug.Print("Button #" + pinId + " pushed!" + time.ToString());
        }

        private void Transmit()
        {
            while (true)
            {
                var leftX = _leftJoystick.Vector.End.X;
                var leftY = _leftJoystick.Vector.End.Y;
                var rightX = _rightJoystick.Vector.End.X;
                var rightY = _rightJoystick.Vector.End.Y;

                Debug.Print("LEFT  X=" + leftX + ", Y=" + leftY + ", Quadrant=" + _leftJoystick.Vector.RelativeQuadrant
                            + "  Angle=" + _leftJoystick.Vector.Direction.Radians + "  Magnitude=" +
                            _leftJoystick.Vector.Magnitude
                            + "    "
                            + "RIGHT  X=" + rightX + ", Y=" + rightY + ", Quadrant=" +
                            _rightJoystick.Vector.RelativeQuadrant
                            + "  Angle=" + _rightJoystick.Vector.Direction.Radians + "  Magnitude=" +
                            _rightJoystick.Vector.Magnitude
                            + "  QUEUE = " + _radio.QueueSize);

                _radio.Send("D|" + _rightJoystick.Vector.Direction.Radians + "|" + (int)_rightJoystick.Vector.Magnitude
                    + "|" + (_leftJoystick.Vector.RelativeQuadrant < 2 ? (-1) : 1) * _leftJoystick.Vector.Magnitude);
                
                Thread.Sleep(_radio.SendFrequency);
            }
        }

        public void Dispose()
        {
            _radio.Dispose();
        }
    }
}
