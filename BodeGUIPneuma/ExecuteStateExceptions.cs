using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BodeGUIPneuma
{
    public class ExecuteStateException : Exception
    {
        public string MeasurementType { get; }
        public ExecuteStateException() { }
        public ExecuteStateException(string message) : base(message) { }
        public ExecuteStateException(string message, Exception innerException) : base(message, innerException) { }
        public ExecuteStateException(string message, string measurementType) : this(message)
        {
            MeasurementType = measurementType;
        }
    }
    public class BodeConnectionException : Exception
    {
        public BodeConnectionException() { }
        public BodeConnectionException(string message) : base("Bode Device Not Found; check connection") { }
    }
}

